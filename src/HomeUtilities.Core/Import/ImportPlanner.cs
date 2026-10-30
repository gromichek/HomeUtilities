using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;

namespace HomeUtilities.Core.Import;

/// <summary>
/// Dry run of an import: reads every data row of a sheet through an <see cref="ImportProfile"/> and says what would be
/// created or skipped, without touching the database. An error makes the whole row skipped, a warning is imported with a note.
/// </summary>
public static partial class ImportPlanner
{
    private const decimal AmountTolerance = 0.02m;

    /// <summary>Plans the import of a sheet.</summary>
    public static ImportPlan Plan(ImportProfile profile, SheetData sheet, ImportContext context)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(context);

        var validation = ImportProfileValidator.Validate(profile);
        if (!validation.IsValid)
        {
            return ImportPlan.Invalid(validation.Errors);
        }

        var registers = context.Meters
            .SelectMany(m => m.Registers.Where(r => !r.IsDeleted).Select(r => (Meter: m, Register: r)))
            .ToDictionary(x => x.Register.Id);
        var missing = profile.Registers
            .Where(r => r.ReadingColumn > 0 && !registers.ContainsKey(r.RegisterId))
            .Select(_ => new ValidationError(ErrorCodes.ImportRegisterMissing))
            .ToList();
        if (missing.Count > 0)
        {
            return ImportPlan.Invalid(missing);
        }

        var positions = BuildPositions(profile, context, registers);
        var rows = new List<PlannedRow>();
        var candidates = new List<ReadingCandidate>();
        var existingPeriods = context.ExistingBills
            .Where(b => !b.IsDeleted && !b.IsDraft && b.ContractId == context.Contract.Id && b.Kind is not (BillKind.Settlement or BillKind.Correction))
            .Select(b => b.Period)
            .ToHashSet();
        var sheetBills = new Dictionary<YearMonth, PlannedRow>();

        foreach (var row in DataRows(profile, sheet))
        {
            var planned = PlanRow(profile, sheet, row, context, existingPeriods, sheetBills, candidates, positions);
            if (planned is not null)
            {
                rows.Add(planned);
            }
        }

        var replacements = PlanReadings(profile, sheet, context, positions, candidates, rows);
        Finalize(rows, registers, replacements);
        return new ImportPlan(rows, [], replacements)
        {
            ContractValidFrom = context.Contract.ValidFrom,
            SuggestedReadingWindow = ImportReadingWindowAdvice.Suggest(rows, context.Contract),
        };
    }

    // A period outside the contract's dates is imported (the bill exists), but charges and reports treat the contract as not valid
    // then, so the owner is told where to fix it.
    private static void WarnIfOutsideContract(PlannedRow row, YearMonth period, Contract contract)
    {
        if (contract.ValidFrom > period.LastDay)
        {
            Warning(row, ImportIssueCodes.PeriodBeforeContractStart, contract.ValidFrom.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        }
        else if (contract.ValidTo is { } end && end < period.FirstDay)
        {
            Warning(row, ImportIssueCodes.PeriodAfterContractEnd, end.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture));
        }
    }

    private static IEnumerable<SheetRow> DataRows(ImportProfile profile, SheetData sheet)
    {
        var last = profile.LastDataRow ?? sheet.RowCount;
        for (var number = profile.FirstDataRow; number <= Math.Min(last, sheet.RowCount); number++)
        {
            var row = sheet.Row(number)!;
            if (profile.LastDataRow is null && row.Cell(profile.PeriodColumn).IsEmpty)
            {
                yield break;
            }

            yield return row;
        }
    }

    private static PlannedRow? PlanRow(
        ImportProfile profile,
        SheetData sheet,
        SheetRow row,
        ImportContext context,
        HashSet<YearMonth> existingPeriods,
        Dictionary<YearMonth, PlannedRow> sheetBills,
        List<ReadingCandidate> candidates,
        List<Position> positions)
    {
        var planned = new PlannedRow { RowNumber = row.Number };
        var periodCell = row.Cell(profile.PeriodColumn);
        if (periodCell.IsEmpty)
        {
            // A completely blank row is not reported; a row with other content but no period is.
            if (row.Cells.All(c => c.IsEmpty))
            {
                return null;
            }

            Error(planned, ImportIssueCodes.PeriodMissing);
            return planned;
        }

        YearMonth period;
        if (ImportCellParser.TryParseDate(periodCell, out var rowDate))
        {
            // A date: the rule says which month it stands for; the reading keeps the real date.
            var (rule, dayLimit) = profile.RuleFor(rowDate);
            period = ImportPeriodResolver.Resolve(rowDate, rule, dayLimit, context.Contract);
            planned.RowDate = rowDate;
        }
        else if (!ImportCellParser.TryParsePeriod(periodCell, out period))
        {
            Error(planned, ImportIssueCodes.PeriodInvalid, periodCell.Text);
            return planned;
        }

        planned.RuleBasedPeriod = period;
        var correction = planned.RowDate is { } date ? profile.OverrideFor(date) : null;
        if (correction is not null)
        {
            planned.Override = correction;
            if (correction.Period is { } corrected)
            {
                period = YearMonth.From(corrected);
            }

            Info(planned, ImportIssueCodes.RowOverridden);
        }

        planned.Period = period;
        WarnIfOutsideContract(planned, period, context.Contract);
        var candidateCount = candidates.Count;
        ReadReadings(profile, row, planned.RowDate, period, planned, candidates, positions);
        var bill = ReadBill(profile, sheet, row, period, context, planned, existingPeriods, sheetBills, correction);
        if (planned.HasErrors)
        {
            return planned;
        }

        var notes = BuildNotes(profile, sheet, row);
        if (bill is not null)
        {
            bill.Notes = JoinNotes(notes, correction?.Remark);
            planned.Bill = bill;
            planned.Payment = ReadPayment(profile, row, planned.RowDate, period, bill, planned);
        }
        else if (notes is not null && candidates.Count > candidateCount)
        {
            candidates[candidateCount].NoteToAttach = notes;
        }

        if (planned.Bill is null && candidates.Count == candidateCount && !planned.Issues.Any(i => i.Code == ImportIssueCodes.DuplicatePeriod))
        {
            Warning(planned, ImportIssueCodes.NothingToImport);
        }

        return planned;
    }

    private static void ReadReadings(
        ImportProfile profile,
        SheetRow row,
        DateOnly? rowDate,
        YearMonth period,
        PlannedRow planned,
        List<ReadingCandidate> candidates,
        List<Position> positions)
    {
        foreach (var position in positions)
        {
            var mapping = position.Mapping;
            var cell = row.Cell(mapping.ReadingColumn);
            if (cell.IsEmpty)
            {
                continue;
            }

            var letter = SheetData.ColumnLetter(mapping.ReadingColumn);
            if (!ImportCellParser.TryParseDecimal(cell, out var value))
            {
                Error(planned, ImportIssueCodes.NumberInvalid, letter, cell.Text);
                continue;
            }

            if (value < 0)
            {
                Error(planned, ImportIssueCodes.ReadingNegative, letter, cell.Text);
                continue;
            }

            var date = rowDate ?? period.LastDay;
            if (mapping.DateColumn > 0)
            {
                var dateCell = row.Cell(mapping.DateColumn);
                if (!dateCell.IsEmpty)
                {
                    if (ImportCellParser.TryParseDate(dateCell, out var parsed))
                    {
                        date = parsed;
                    }
                    else
                    {
                        Error(planned, ImportIssueCodes.DateInvalid, SheetData.ColumnLetter(mapping.DateColumn), dateCell.Text);
                        continue;
                    }
                }
            }

            var candidate = new ReadingCandidate(planned, position, letter, date, value);
            position.Candidates.Add(candidate);
            candidates.Add(candidate);
        }
    }

    private static Bill? ReadBill(
        ImportProfile profile,
        SheetData sheet,
        SheetRow row,
        YearMonth period,
        ImportContext context,
        PlannedRow planned,
        HashSet<YearMonth> existingPeriods,
        Dictionary<YearMonth, PlannedRow> sheetBills,
        ImportRowOverride? correction)
    {
        var total = ReadAmount(row, profile.TotalColumn, profile.TotalFallbackColumn, planned);
        var net = ReadAmount(row, profile.NetColumn, profile.NetFallbackColumn, planned);
        var vat = ReadAmount(row, profile.VatColumn, profile.VatFallbackColumn, planned);
        var percent = ReadAmount(row, profile.VatPercentColumn, 0, planned);
        var quantity = ReadAmount(row, profile.QuantityColumn, 0, planned);
        if (planned.HasErrors)
        {
            return null;
        }

        var gross = total ?? (net is { } n && vat is { } v ? n + v : null);
        var hasReadings = profile.Registers.Any(r => r.ReadingColumn > 0 && !row.Cell(r.ReadingColumn).IsEmpty);
        if (gross is null)
        {
            if (profile.TotalColumn > 0 && hasReadings)
            {
                Warning(planned, ImportIssueCodes.TotalMissing);
            }

            if (profile.PaidColumn > 0 && profile.IsPaidMarker(row.Cell(profile.PaidColumn).Text))
            {
                Warning(planned, ImportIssueCodes.PaidWithoutBill);
            }

            return null;
        }

        if (existingPeriods.Contains(period))
        {
            Warning(planned, ImportIssueCodes.DuplicatePeriod, period.ToString());
            return null;
        }

        if (sheetBills.TryGetValue(period, out var earlier))
        {
            // Two rows of one period: the later row (the corrected one) supplies the bill and the payment.
            earlier.Bill = null;
            earlier.Payment = null;
            Warning(earlier, ImportIssueCodes.PeriodSuperseded, period.ToString(), planned.RowNumber.ToString(CultureInfo.InvariantCulture));
            Warning(planned, ImportIssueCodes.PeriodSupersedes, period.ToString(), earlier.RowNumber.ToString(CultureInfo.InvariantCulture));
        }

        sheetBills[period] = planned;
        var amounts = Complete(net, percent, vat, gross.Value, planned);
        var contract = context.Contract;
        var bill = new Bill
        {
            PropertyId = contract.PropertyId,
            CategoryId = contract.CategoryId,
            ContractId = contract.Id,
            SupplierId = contract.SupplierId,
            Period = period,
            Kind = EqualizedPlans.AmountFor(context.Plans ?? [], period) is not null ? BillKind.Equalized : BillKind.Regular,
            Net = amounts.Net,
            VatPercent = amounts.VatPercent,
            Vat = amounts.Vat,
            Gross = amounts.Gross,
            Currency = profile.CurrencyFor(period),
            ConsumptionOnBill = quantity,
        };

        if (correction?.CoversUntil is { } until && YearMonth.From(until) > period)
        {
            bill.ServiceFrom = period.FirstDay;
            bill.ServiceTo = YearMonth.From(until).LastDay;
        }

        return bill;
    }

    private static AmountSet Complete(decimal? net, decimal? percent, decimal? vat, decimal gross, PlannedRow planned)
    {
        if (percent is < 0 or > 100)
        {
            Warning(planned, ImportIssueCodes.VatPercentInvalid);
            percent = null;
        }

        if (net is { } n)
        {
            var expectedVat = vat ?? (percent is { } p ? MoneyMath.Round(n * p / 100) : (decimal?)null);
            if (expectedVat is { } v && Math.Abs(n + v - gross) > AmountTolerance)
            {
                Warning(planned, ImportIssueCodes.AmountsInconsistent);
                return TotalOnly(gross);
            }

            var derived = Amounts.Complete(n, null, gross).Result;
            return derived is null ? TotalOnly(gross) : percent is null ? derived : derived with { VatPercent = percent };
        }

        if (percent is { } onlyPercent)
        {
            var (result, _) = Amounts.Complete(null, onlyPercent, gross);
            if (result is not null)
            {
                return result;
            }
        }

        return TotalOnly(gross);
    }

    private static AmountSet TotalOnly(decimal gross) => Amounts.Complete(null, null, gross).Result!;

    private static decimal? ReadAmount(SheetRow row, int column, int fallbackColumn, PlannedRow planned)
    {
        var used = column;
        var cell = column > 0 ? row.Cell(column) : SheetCell.Empty;
        if (cell.IsEmpty && fallbackColumn > 0)
        {
            cell = row.Cell(fallbackColumn);
            used = fallbackColumn;
            if (!cell.IsEmpty)
            {
                Info(planned, ImportIssueCodes.FallbackUsed, SheetData.ColumnLetter(fallbackColumn));
            }
        }

        if (used <= 0 || cell.IsEmpty)
        {
            return null;
        }

        if (ImportCellParser.TryParseDecimal(cell, out var value))
        {
            return value;
        }

        Error(planned, ImportIssueCodes.NumberInvalid, SheetData.ColumnLetter(used), cell.Text);
        return null;
    }

    private static Payment? ReadPayment(ImportProfile profile, SheetRow row, DateOnly? rowDate, YearMonth period, Bill bill, PlannedRow planned)
    {
        if (profile.PaidColumn <= 0 || !profile.IsPaidMarker(row.Cell(profile.PaidColumn).Text) || bill.Gross == 0)
        {
            return null;
        }

        var paidOn = profile.DateIsPaymentDate && rowDate is { } rowDay ? rowDay : period.FirstDay;
        if (profile.PaidDateColumn > 0)
        {
            var cell = row.Cell(profile.PaidDateColumn);
            if (!cell.IsEmpty)
            {
                if (ImportCellParser.TryParseDate(cell, out var date))
                {
                    paidOn = date;
                }
                else
                {
                    Warning(planned, ImportIssueCodes.PaidDateInvalid, SheetData.ColumnLetter(profile.PaidDateColumn), cell.Text);
                }
            }
        }

        var payment = new Payment
        {
            ContractId = bill.ContractId,
            PaidOn = paidOn,
            Amount = bill.Gross,
            Currency = bill.Currency,
            Method = PaymentMethod.Other,
            Reference = "imported",
            Notes = string.Create(CultureInfo.InvariantCulture, $"Imported from sheet '{profile.SheetName}', row {row.Number}"),
        };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = bill.Gross });
        return payment;
    }

    private static string? BuildNotes(ImportProfile profile, SheetData sheet, SheetRow row)
    {
        var parts = new List<string>();
        foreach (var column in profile.NotesColumns.Where(c => c > 0))
        {
            var cell = row.Cell(column);
            if (cell.IsEmpty)
            {
                continue;
            }

            var text = cell.Text.Trim();
            var header = sheet.Row(profile.HeaderRow)?.Cell(column).Text.Trim();
            parts.Add(string.IsNullOrEmpty(header) ? text : $"{header}: {text}");
        }

        if (parts.Count == 0)
        {
            return null;
        }

        var joined = string.Join("; ", parts);
        return joined.Length <= InputLimits.Notes ? joined : joined[..(InputLimits.Notes - 1)] + "…";
    }

    private static string? JoinNotes(string? notes, string? remark)
    {
        var text = string.Join("; ", new[] { notes, remark?.Trim() }.Where(t => !string.IsNullOrWhiteSpace(t)));
        if (text.Length == 0)
        {
            return null;
        }

        return text.Length <= InputLimits.Notes ? text : text[..(InputLimits.Notes - 1)] + "…";
    }

    private static void Finalize(List<PlannedRow> rows, Dictionary<Guid, (Meter Meter, MeterRegister Register)> registers, List<PlannedReplacement> replacements)
    {
        foreach (var row in rows.Where(r => r.HasErrors))
        {
            // The initial reading of a new meter that was the reading of a skipped row is stored with the replacement instead.
            foreach (var reading in row.Readings)
            {
                foreach (var line in replacements.SelectMany(r => r.Registers).Where(l => l.InitialIsRowReading && l.InitialReading == reading.Reading))
                {
                    line.InitialIsRowReading = false;
                }
            }

            row.Readings.Clear();
            row.Bill = null;
            row.Payment = null;
        }

        // The first imported reading of a register that has none becomes its Initial reading.
        var firstPerRegister = rows
            .SelectMany(r => r.Readings)
            .Where(r => r.Reading.Kind == ReadingKind.Regular)
            .GroupBy(r => r.RegisterId)
            .Where(g => registers.TryGetValue(g.Key, out var entry) && !entry.Register.Readings.Any(r => !r.IsDeleted));
        foreach (var group in firstPerRegister)
        {
            group.OrderBy(r => r.Reading.Date).First().Reading.Kind = ReadingKind.Initial;
        }

        // A row whose bill was dropped by an error elsewhere must not leave a payment behind.
        foreach (var row in rows.Where(r => r.Payment is not null && r.Bill is null))
        {
            row.Payment = null;
        }
    }

    private static void Error(PlannedRow row, string code, params string[] args) =>
        row.Issues.Add(new(ImportIssueSeverity.Error, code, args));

    private static void Warning(PlannedRow row, string code, params string[] args) =>
        row.Issues.Add(new(ImportIssueSeverity.Warning, code, args));

    private static void Info(PlannedRow row, string code, params string[] args) =>
        row.Issues.Add(new(ImportIssueSeverity.Info, code, args));
}
