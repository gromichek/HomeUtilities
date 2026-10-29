using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Import;

/// <summary>
/// Dry run of an import: reads every data row of a sheet through an <see cref="ImportProfile"/> and says what would be
/// created or skipped, without touching the database. An error makes the whole row skipped, a warning is imported with a note.
/// </summary>
public static class ImportPlanner
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

        var rows = new List<PlannedRow>();
        var candidates = new List<ReadingCandidate>();
        var seenPeriods = context.ExistingBills
            .Where(b => !b.IsDeleted && !b.IsDraft && b.ContractId == context.Contract.Id && b.Kind is not (BillKind.Settlement or BillKind.Correction))
            .Select(b => b.Period)
            .ToHashSet();

        foreach (var row in DataRows(profile, sheet))
        {
            var planned = PlanRow(profile, sheet, row, context, seenPeriods, candidates);
            if (planned is not null)
            {
                rows.Add(planned);
            }
        }

        ValidateReadingChains(candidates, registers);
        Finalize(rows, registers);
        return new ImportPlan(rows, []);
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
        HashSet<YearMonth> seenPeriods,
        List<ReadingCandidate> candidates)
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

        if (!ImportCellParser.TryParsePeriod(periodCell, out var period))
        {
            Error(planned, ImportIssueCodes.PeriodInvalid, periodCell.Text);
            return planned;
        }

        planned.Period = period;
        ReadReadings(profile, row, period, planned, candidates);
        var bill = ReadBill(profile, sheet, row, period, context, planned, seenPeriods);
        if (planned.HasErrors)
        {
            return planned;
        }

        var notes = BuildNotes(profile, sheet, row);
        if (bill is not null)
        {
            bill.Notes = notes;
            planned.Bill = bill;
            planned.Payment = ReadPayment(profile, row, period, bill, planned);
        }
        else if (notes is not null && candidates.FirstOrDefault(c => c.Row == planned) is { } first)
        {
            first.NoteToAttach = notes;
        }

        if (planned.Bill is null && !candidates.Any(c => c.Row == planned) && !planned.Issues.Any(i => i.Code == ImportIssueCodes.DuplicatePeriod))
        {
            Warning(planned, ImportIssueCodes.NothingToImport);
        }

        return planned;
    }

    private static void ReadReadings(ImportProfile profile, SheetRow row, YearMonth period, PlannedRow planned, List<ReadingCandidate> candidates)
    {
        foreach (var mapping in profile.Registers.Where(r => r.ReadingColumn > 0))
        {
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

            var date = period.LastDay;
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

            candidates.Add(new ReadingCandidate(planned, mapping.RegisterId, letter, date, value));
        }
    }

    private static Bill? ReadBill(
        ImportProfile profile,
        SheetData sheet,
        SheetRow row,
        YearMonth period,
        ImportContext context,
        PlannedRow planned,
        HashSet<YearMonth> seenPeriods)
    {
        var total = ReadAmount(row, profile.TotalColumn, planned);
        var net = ReadAmount(row, profile.NetColumn, planned);
        var vat = ReadAmount(row, profile.VatColumn, planned);
        var percent = ReadAmount(row, profile.VatPercentColumn, planned);
        var quantity = ReadAmount(row, profile.QuantityColumn, planned);
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

        if (!seenPeriods.Add(period))
        {
            Warning(planned, ImportIssueCodes.DuplicatePeriod, period.ToString());
            return null;
        }

        var amounts = Complete(net, percent, vat, gross.Value, planned);
        var contract = context.Contract;
        return new Bill
        {
            PropertyId = contract.PropertyId,
            CategoryId = contract.CategoryId,
            ContractId = contract.Id,
            SupplierId = contract.SupplierId,
            Period = period,
            Kind = contract.BillingMode == BillingMode.Equalized ? BillKind.Equalized : BillKind.Regular,
            Net = amounts.Net,
            VatPercent = amounts.VatPercent,
            Vat = amounts.Vat,
            Gross = amounts.Gross,
            Currency = profile.CurrencyFor(period),
            ConsumptionOnBill = quantity,
        };
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

    private static decimal? ReadAmount(SheetRow row, int column, PlannedRow planned)
    {
        if (column <= 0)
        {
            return null;
        }

        var cell = row.Cell(column);
        if (cell.IsEmpty)
        {
            return null;
        }

        if (ImportCellParser.TryParseDecimal(cell, out var value))
        {
            return value;
        }

        Error(planned, ImportIssueCodes.NumberInvalid, SheetData.ColumnLetter(column), cell.Text);
        return null;
    }

    private static Payment? ReadPayment(ImportProfile profile, SheetRow row, YearMonth period, Bill bill, PlannedRow planned)
    {
        if (profile.PaidColumn <= 0 || !profile.IsPaidMarker(row.Cell(profile.PaidColumn).Text) || bill.Gross == 0)
        {
            return null;
        }

        var paidOn = period.FirstDay;
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

    private static void ValidateReadingChains(
        List<ReadingCandidate> candidates,
        Dictionary<Guid, (Meter Meter, MeterRegister Register)> registers)
    {
        foreach (var group in candidates.Where(c => !c.Row.HasErrors).GroupBy(c => c.RegisterId))
        {
            var (meter, register) = registers[group.Key];
            var existing = register.Readings.Where(r => !r.IsDeleted).OrderBy(r => r.Date).ThenBy(r => r.CreatedAt).ToList();
            var accepted = new List<(DateOnly Date, decimal Value)>(existing.Select(r => (r.Date, r.Value)));

            foreach (var candidate in group.OrderBy(c => c.Date).ThenBy(c => c.Row.RowNumber))
            {
                if (candidate.Row.HasErrors)
                {
                    continue;
                }

                var text = candidate.Value.ToString(CultureInfo.InvariantCulture);
                if (accepted.Any(a => a.Date == candidate.Date))
                {
                    Warning(candidate.Row, ImportIssueCodes.ReadingExists, candidate.Column, candidate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    continue;
                }

                if (candidate.Date < meter.InstalledOn || (meter.RemovedOn is { } removed && candidate.Date > removed))
                {
                    Error(candidate.Row, ImportIssueCodes.ReadingOutsideInstallation, candidate.Column, candidate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    continue;
                }

                var previous = accepted.Where(a => a.Date < candidate.Date).OrderByDescending(a => a.Date).Cast<(DateOnly Date, decimal Value)?>().FirstOrDefault();
                if (previous is { } p && candidate.Value < p.Value)
                {
                    var previousText = p.Value.ToString(CultureInfo.InvariantCulture);
                    if (meter.Digits is null)
                    {
                        Error(candidate.Row, ImportIssueCodes.ReadingLowerThanPrevious, candidate.Column, text, previousText);
                        continue;
                    }

                    Warning(candidate.Row, ImportIssueCodes.ReadingRollover, candidate.Column, text, previousText);
                }

                var next = existing.FirstOrDefault(r => r.Date > candidate.Date);
                if (meter.Digits is null && next is not null && candidate.Value > next.Value)
                {
                    Error(candidate.Row, ImportIssueCodes.ReadingHigherThanNext, candidate.Column, text, next.Value.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                accepted.Add((candidate.Date, candidate.Value));
                candidate.Accepted = true;
            }
        }

        // Readings are attached after all checks so that a row skipped by a later check keeps none of them.
        foreach (var candidate in candidates.Where(c => c.Accepted && !c.Row.HasErrors))
        {
            candidate.Row.Readings.Add(new PlannedReading(
                candidate.RegisterId,
                new MeterReading
                {
                    RegisterId = candidate.RegisterId,
                    Date = candidate.Date,
                    Value = candidate.Value,
                    Kind = ReadingKind.Regular,
                    Note = candidate.NoteToAttach,
                }));
        }
    }

    private static void Finalize(List<PlannedRow> rows, Dictionary<Guid, (Meter Meter, MeterRegister Register)> registers)
    {
        foreach (var row in rows.Where(r => r.HasErrors))
        {
            row.Readings.Clear();
            row.Bill = null;
            row.Payment = null;
        }

        // The first imported reading of a register that has none becomes its Initial reading.
        var firstPerRegister = rows
            .SelectMany(r => r.Readings)
            .GroupBy(r => r.RegisterId)
            .Where(g => !registers[g.Key].Register.Readings.Any(r => !r.IsDeleted));
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

    private sealed class ReadingCandidate(PlannedRow row, Guid registerId, string column, DateOnly date, decimal value)
    {
        public PlannedRow Row { get; } = row;

        public Guid RegisterId { get; } = registerId;

        public string Column { get; } = column;

        public DateOnly Date { get; } = date;

        public decimal Value { get; } = value;

        public bool Accepted { get; set; }

        public string? NoteToAttach { get; set; }
    }
}
