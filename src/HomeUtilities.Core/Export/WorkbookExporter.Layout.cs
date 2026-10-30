using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Core.Export;

public static partial class WorkbookExporter
{
    // Places the rows of one contract on a sheet: like the import profile (the reverse of the import) or in the standard layout.
    private sealed class Layout(
        ReportData data,
        Contract contract,
        ImportProfile? profile,
        List<Position> positions,
        Dictionary<Guid, Paid> paid,
        WorkbookExportOptions options,
        ExportLabels labels)
    {
        private const int MoneyDecimals = 2;
        private const int ReadingDecimals = 3;

        private readonly List<PlacedCell> _cells = [];

        // ---- Standard layout -------------------------------------------------------------------------------------

        public ExportSheet Standard(string name, IReadOnlyList<ExportRow> rows)
        {
            var column = 1;
            var dateColumn = column++;
            var readingColumns = positions.ToDictionary(p => p, _ => column++);
            var consumption = column++;
            var net = column++;
            var percent = column++;
            var vat = column++;
            var calculated = column++;
            var total = column++;
            var difference = column++;
            var paidColumn = column++;
            var paidDate = column++;
            var notes = column++;
            var currency = column;

            Header(1, dateColumn, labels.Date);
            foreach (var position in positions)
            {
                Header(1, readingColumns[position], labels.PositionHeader(position.Chain[0], position.Head));
            }

            Header(1, consumption, labels.Consumption);
            Header(1, net, labels.Net);
            Header(1, percent, labels.VatPercent);
            Header(1, vat, labels.Vat);
            Header(1, calculated, labels.Calculated);
            Header(1, total, labels.BillTotal);
            Header(1, difference, labels.Difference);
            Header(1, paidColumn, labels.Paid);
            Header(1, paidDate, labels.PaidDate);
            Header(1, notes, labels.Notes);
            Header(1, currency, labels.Currency);

            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var line = index + 2;
                Put(line, dateColumn, ExportCell.FromDate(row.Date));
                PutReadings(line, row, p => readingColumns[p], _ => 0);
                if (ConsumptionOf(row) is { } quantity)
                {
                    Put(line, consumption, ExportCell.FromNumber(quantity, ReadingDecimals));
                }

                var bill = row.Bill;
                if (bill is not null)
                {
                    PutMoney(line, net, bill.Net, bill.Currency);
                    if (bill.VatPercent is { } rate)
                    {
                        Put(line, percent, ExportCell.FromNumber(rate, MoneyDecimals));
                    }

                    PutMoney(line, vat, bill.Vat, bill.Currency);
                    PutMoney(line, total, bill.Gross, bill.Currency);
                    PutPaid(line, bill, paidColumn, paidDate, "***");
                    Put(line, currency, ExportCell.FromText((options.ConvertToEur ? Currency.EUR : bill.Currency).ToString()));
                }

                if (!row.IsExtra && data.Charge(contract, row.Period) is { Lines.Count: > 0 } charge)
                {
                    var target = bill?.Currency ?? charge.Currency;
                    var calc = CalculatedIn(charge.TotalGross, charge.Currency, target);
                    PutMoney(line, calculated, calc, options.ConvertToEur ? Currency.EUR : target, convert: false);
                    if (bill is not null)
                    {
                        PutMoney(line, difference, (options.ConvertToEur ? ReportMoney.Eur(bill.Gross, bill.Currency) : bill.Gross) - calc, target, convert: false);
                    }
                }

                if (NotesOf(row) is { Length: > 0 } text)
                {
                    Put(line, notes, ExportCell.FromText(text));
                }
            }

            return new ExportSheet(name, _cells, 1, 1);
        }

        // ---- Layout of an import profile -------------------------------------------------------------------------

        public ExportSheet Profile(string name, IReadOnlyList<ExportRow> rows)
        {
            var p = profile!;
            var notesColumns = p.NotesColumns.Where(c => c > 0).ToList();
            var used = new[] { p.PeriodColumn, p.TotalColumn, p.NetColumn, p.VatPercentColumn, p.VatColumn, p.PaidColumn, p.PaidDateColumn, p.QuantityColumn }
                .Concat(notesColumns).Concat(positions.SelectMany(x => new[] { x.ReadingColumn, x.DateColumn })).Max();
            var wantsNotes = rows.Any(r => NotesOf(r) is { Length: > 0 });
            var extraNotes = notesColumns.Count == 0 && wantsNotes ? used + 1 : 0;

            if (p.HeaderRow > 0)
            {
                HeaderOf(p.PeriodColumn, "Period", labels.Date);
                HeaderOf(p.TotalColumn, "Total", labels.BillTotal);
                HeaderOf(p.NetColumn, "Net", labels.Net);
                HeaderOf(p.VatPercentColumn, "VatPercent", labels.VatPercent);
                HeaderOf(p.VatColumn, "Vat", labels.Vat);
                HeaderOf(p.PaidColumn, "Paid", labels.Paid);
                HeaderOf(p.PaidDateColumn, "PaidDate", labels.PaidDate);
                HeaderOf(p.QuantityColumn, "Quantity", labels.Consumption);
                foreach (var column in notesColumns)
                {
                    HeaderOf(column, "Notes", labels.Notes);
                }

                foreach (var position in positions)
                {
                    HeaderOf(position.ReadingColumn, ImportColumnHeaders.ReadingField(position.Key), labels.PositionHeader(position.Chain[0], position.Head));
                    HeaderOf(position.DateColumn, ImportColumnHeaders.ReadingDateField(position.Key), labels.Date);
                }

                if (extraNotes > 0)
                {
                    Header(p.HeaderRow, extraNotes, labels.Notes);
                }
            }

            var noteHeaders = notesColumns.Select(c => (Column: c, Header: HeaderText("Notes", c) ?? string.Empty)).ToList();
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var line = p.FirstDataRow + index;
                var bill = row.Bill;
                Put(line, p.PeriodColumn, ExportCell.FromDate(row.Date));
                PutReadings(line, row, x => x.ReadingColumn, x => x.DateColumn);
                if (bill is not null)
                {
                    PutMoney(line, p.TotalColumn, bill.Gross, bill.Currency);
                    PutMoney(line, p.NetColumn, bill.Net, bill.Currency);
                    PutMoney(line, p.VatColumn, bill.Vat, bill.Currency);
                    if (p.VatPercentColumn > 0 && bill.VatPercent is { } rate)
                    {
                        Put(line, p.VatPercentColumn, ExportCell.FromNumber(rate, MoneyDecimals));
                    }

                    if (p.QuantityColumn > 0 && bill.ConsumptionOnBill is { } quantity)
                    {
                        Put(line, p.QuantityColumn, ExportCell.FromNumber(quantity, ReadingDecimals));
                    }

                    PutPaid(line, bill, p.PaidColumn, p.PaidDateColumn, string.IsNullOrWhiteSpace(p.PaidMarker) ? ImportProfile.DefaultPaidMarker : p.PaidMarker.Trim());
                }

                if (NotesOf(row) is { Length: > 0 } text)
                {
                    if (extraNotes > 0)
                    {
                        Put(line, extraNotes, ExportCell.FromText(text));
                    }
                    else
                    {
                        foreach (var (column, value) in SplitNotes(text, noteHeaders))
                        {
                            Put(line, column, ExportCell.FromText(value));
                        }
                    }
                }
            }

            return new ExportSheet(name, _cells, Math.Max(0, p.HeaderRow));
        }

        // Notes of a bill are stored as "Header: text; Header: text"; every part goes back to the column it came from.
        private static List<(int Column, string Text)> SplitNotes(string notes, List<(int Column, string Header)> columns)
        {
            var values = columns.ToDictionary(c => c.Column, _ => new List<string>());
            var rest = new List<string>();
            foreach (var part in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
            {
                var match = columns.FirstOrDefault(c => c.Header.Length > 0 && part.StartsWith(c.Header + ": ", StringComparison.OrdinalIgnoreCase));
                if (match.Column > 0)
                {
                    values[match.Column].Add(part[(match.Header.Length + 2)..]);
                }
                else
                {
                    rest.Add(part);
                }
            }

            if (rest.Count > 0)
            {
                values[columns[0].Column].AddRange(rest);
            }

            return [.. values.Where(v => v.Value.Count > 0).Select(v => (v.Key, string.Join("; ", v.Value)))];
        }

        // ---- Shared ----------------------------------------------------------------------------------------------

        private string? HeaderText(string field, int column)
        {
            var letter = SheetData.ColumnLetter(column);
            var text = profile!.ColumnHeaders.FirstOrDefault(h => h.Field == field && h.Column == letter)?.Header;
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        private void HeaderOf(int column, string field, string fallback)
        {
            if (column > 0)
            {
                Header(profile!.HeaderRow, column, HeaderText(field, column) ?? fallback);
            }
        }

        private void Header(int row, int column, string text) => Put(row, column, ExportCell.FromText(text, bold: true));

        private void Put(int row, int column, ExportCell cell)
        {
            if (row > 0 && column > 0)
            {
                _cells.Add(new PlacedCell(row, column, cell));
            }
        }

        private void PutReadings(int line, ExportRow row, Func<Position, int> columnOf, Func<Position, int> dateColumnOf)
        {
            foreach (var (position, reading) in row.Readings)
            {
                Put(line, columnOf(position), ExportCell.FromNumber(reading.Value, ReadingDecimals));
                Put(line, dateColumnOf(position), ExportCell.FromDate(reading.Date));
            }
        }

        private void PutMoney(int line, int column, decimal? amount, Currency currency, bool convert = true)
        {
            if (amount is { } value && column > 0)
            {
                Put(line, column, ExportCell.FromNumber(convert && options.ConvertToEur ? ReportMoney.Eur(value, currency) : value, MoneyDecimals));
            }
        }

        private void PutPaid(int line, Bill bill, int paidColumn, int paidDateColumn, string marker)
        {
            if (!IsPaid(bill, paid))
            {
                return;
            }

            if (paidColumn > 0)
            {
                Put(line, paidColumn, ExportCell.FromText(marker));
            }

            if (paidDateColumn > 0 && paid[bill.Id].LastDate is { } date)
            {
                Put(line, paidDateColumn, ExportCell.FromDate(date));
            }
        }

        // The calculated total (a charge is in EUR or the contract's currency) in the currency the row shows.
        private static decimal CalculatedIn(decimal gross, Currency from, Currency target)
        {
            var eur = ReportMoney.Eur(gross, from);
            return target == Currency.LVL ? MoneyMath.Round(eur * CurrencyConverter.LvlPerEur) : eur;
        }

        private string? NotesOf(ExportRow row)
        {
            var text = row.Bill?.Notes;
            if (string.IsNullOrWhiteSpace(text))
            {
                var fromReadings = row.Readings.Values.Select(r => r.Note).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct();
                text = string.Join("; ", fromReadings);
            }

            return string.Join("; ", new[] { text, row.ReplacementNote }.Where(t => !string.IsNullOrWhiteSpace(t)));
        }

        private decimal? ConsumptionOf(ExportRow row)
        {
            if (row.IsExtra)
            {
                return null;
            }

            if (row.Bill?.ConsumptionOnBill is { } billed)
            {
                return billed;
            }

            (int From, int To)? window = contract.ReadingDayFrom is { } from && contract.ReadingDayTo is { } to ? (from, to) : null;
            var total = 0m;
            var any = false;
            foreach (var position in positions)
            {
                var result = ConsumptionCalculator.CalculateForPeriod(new MeterChain(position.Chain), position.Head.Kind, row.Period, window);
                if (result.StartReading is not null && result.EndReading is not null)
                {
                    total += result.Quantity;
                    any = true;
                }
            }

            return any ? total : null;
        }
    }
}
