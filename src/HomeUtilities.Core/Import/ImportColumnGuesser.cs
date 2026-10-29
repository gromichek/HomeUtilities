namespace HomeUtilities.Core.Import;

/// <summary>
/// Suggests which column means what from the header texts (Russian, Latvian and English keywords). It is only a
/// starting point; the owner can change every choice.
/// </summary>
public static class ImportColumnGuesser
{
    private static readonly string[] PaidDateWords = ["дата оплаты", "дата плат", "apmaksas datums", "apmaksāts datums", "paid date", "date paid", "payment date"];
    private static readonly string[] PaidWords = ["оплачено", "оплата", "оплач", "apmaksāts", "apmaksa", "paid"];
    private static readonly string[] VatPercentWords = ["ндс %", "ндс%", "% ндс", "pvn %", "pvn%", "% pvn", "vat %", "vat%", "% vat"];
    private static readonly string[] NetWords = ["без ндс", "bez pvn", "excl", "net", "нетто"];
    private static readonly string[] VatWords = ["ндс", "pvn", "vat"];
    private static readonly string[] TotalWords = ["итого", "всего", "к оплате", "kopā", "kopa", "apmaksai", "total", "gross", "сумма", "summa", "amount"];
    private static readonly string[] ReadingWords = ["показ", "rādījum", "radijum", "reading"];
    private static readonly string[] QuantityWords = ["кол-во по счет", "количество", "daudzums", "quantity", "по счёт", "по счет"];
    private static readonly string[] NoteWords = ["примеч", "коммент", "заметк", "piezīm", "piezim", "komentār", "note", "comment", "remark"];
    private static readonly string[] PeriodWords = ["месяц", "период", "дата", "mēnesis", "menesis", "periods", "datums", "month", "period", "date"];

    /// <summary>
    /// Fills a profile's column mapping from the header row of the sheet. Reading columns are assigned to
    /// <paramref name="registerIds"/> in the order of the columns.
    /// </summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="headerRow">Row number of the headers.</param>
    /// <param name="registerIds">The registers of the contract's meters, in display order.</param>
    public static ImportProfile Guess(SheetData sheet, int headerRow, IReadOnlyList<Guid> registerIds)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(registerIds);

        var profile = new ImportProfile { SheetName = sheet.Name, HeaderRow = headerRow, FirstDataRow = headerRow + 1 };
        var header = sheet.Row(headerRow);
        if (header is null)
        {
            return profile;
        }

        var readingColumns = new List<int>();
        for (var column = 1; column <= sheet.ColumnCount; column++)
        {
            var text = header.Cell(column).Text.Trim().ToLowerInvariant();
            if (text.Length == 0)
            {
                continue;
            }

            if (Has(text, PaidDateWords))
            {
                Set(profile, p => p.PaidDateColumn, (p, c) => p.PaidDateColumn = c, column);
            }
            else if (Has(text, PaidWords))
            {
                Set(profile, p => p.PaidColumn, (p, c) => p.PaidColumn = c, column);
            }
            else if (Has(text, VatPercentWords))
            {
                Set(profile, p => p.VatPercentColumn, (p, c) => p.VatPercentColumn = c, column);
            }
            else if (Has(text, NetWords))
            {
                Set(profile, p => p.NetColumn, (p, c) => p.NetColumn = c, column);
            }
            else if (Has(text, VatWords))
            {
                Set(profile, p => p.VatColumn, (p, c) => p.VatColumn = c, column);
            }
            else if (Has(text, ReadingWords))
            {
                readingColumns.Add(column);
            }
            else if (Has(text, QuantityWords))
            {
                Set(profile, p => p.QuantityColumn, (p, c) => p.QuantityColumn = c, column);
            }
            else if (Has(text, NoteWords))
            {
                profile.NotesColumns.Add(column);
            }
            else if (Has(text, TotalWords))
            {
                Set(profile, p => p.TotalColumn, (p, c) => p.TotalColumn = c, column);
            }
            else if (Has(text, PeriodWords))
            {
                Set(profile, p => p.PeriodColumn, (p, c) => p.PeriodColumn = c, column);
            }
        }

        for (var i = 0; i < Math.Min(readingColumns.Count, registerIds.Count); i++)
        {
            profile.Registers.Add(new RegisterColumnMapping { RegisterId = registerIds[i], ReadingColumn = readingColumns[i] });
        }

        return profile;
    }

    private static bool Has(string text, string[] words) => words.Any(w => text.Contains(w, StringComparison.Ordinal));

    // The first column that matches a field wins.
    private static void Set(ImportProfile profile, Func<ImportProfile, int> get, Action<ImportProfile, int> set, int column)
    {
        if (get(profile) == 0)
        {
            set(profile, column);
        }
    }
}
