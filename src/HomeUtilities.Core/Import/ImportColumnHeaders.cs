namespace HomeUtilities.Core.Import;

/// <summary>Records which column holds which field and what its header says, so that a later export can use the same layout.</summary>
public static class ImportColumnHeaders
{
    /// <summary>Field name of the readings of a register: <c>Reading:{register id}</c>.</summary>
    public static string ReadingField(Guid registerId) => $"Reading:{registerId}";

    /// <summary>Field name of the reading dates of a register: <c>ReadingDate:{register id}</c>.</summary>
    public static string ReadingDateField(Guid registerId) => $"ReadingDate:{registerId}";

    /// <summary>Fills <see cref="ImportProfile.ColumnHeaders"/> from the header row of the sheet.</summary>
    public static void Record(ImportProfile profile, SheetData sheet)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(sheet);

        var header = sheet.Row(profile.HeaderRow);
        var list = new List<ImportColumnHeader>();

        void Add(string field, int column)
        {
            if (column > 0)
            {
                list.Add(new ImportColumnHeader { Field = field, Column = SheetData.ColumnLetter(column), Header = header?.Cell(column).Text.Trim() ?? string.Empty });
            }
        }

        Add("Period", profile.PeriodColumn);
        Add("Total", profile.TotalColumn);
        Add("TotalFallback", profile.TotalFallbackColumn);
        Add("Net", profile.NetColumn);
        Add("NetFallback", profile.NetFallbackColumn);
        Add("VatPercent", profile.VatPercentColumn);
        Add("Vat", profile.VatColumn);
        Add("VatFallback", profile.VatFallbackColumn);
        Add("Paid", profile.PaidColumn);
        Add("PaidDate", profile.PaidDateColumn);
        Add("Quantity", profile.QuantityColumn);
        foreach (var column in profile.NotesColumns)
        {
            Add("Notes", column);
        }

        foreach (var register in profile.Registers)
        {
            Add(ReadingField(register.RegisterId), register.ReadingColumn);
            Add(ReadingDateField(register.RegisterId), register.DateColumn);
        }

        profile.ColumnHeaders = list;
    }
}
