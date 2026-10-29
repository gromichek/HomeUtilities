namespace HomeUtilities.Core.Import;

/// <summary>
/// One cell as read from the workbook.
/// </summary>
/// <param name="Value">The typed value: <see cref="decimal"/>, <see cref="DateOnly"/>, <see cref="string"/> or null for an empty cell.</param>
/// <param name="Text">The text as Excel shows it (empty for an empty cell).</param>
public readonly record struct SheetCell(object? Value, string Text)
{
    /// <summary>An empty cell.</summary>
    public static SheetCell Empty => new(null, string.Empty);

    /// <summary>True when the cell holds nothing but spaces.</summary>
    public bool IsEmpty => Value is null && string.IsNullOrWhiteSpace(Text);

    /// <summary>A cell with a number.</summary>
    public static SheetCell Number(decimal value, string? text = null) =>
        new(value, text ?? value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>A cell with a date.</summary>
    public static SheetCell Date(DateOnly value, string? text = null) =>
        new(value, text ?? value.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>A cell with text.</summary>
    public static SheetCell String(string value) => string.IsNullOrEmpty(value) ? Empty : new(value, value);
}

/// <summary>One sheet row; <see cref="Cells"/> index 0 is column A.</summary>
public sealed record SheetRow(int Number, IReadOnlyList<SheetCell> Cells)
{
    /// <summary>The cell of a 1-based column; an empty cell beyond the row's end or for column 0.</summary>
    public SheetCell Cell(int column) =>
        column >= 1 && column <= Cells.Count ? Cells[column - 1] : SheetCell.Empty;
}

/// <summary>Name and size of a sheet, for the first wizard step.</summary>
public sealed record SheetInfo(string Name, int RowCount, int ColumnCount);

/// <summary>The cells of one sheet: rows 1…n, dense (empty rows included).</summary>
public sealed record SheetData(string Name, IReadOnlyList<SheetRow> Rows, int ColumnCount)
{
    /// <summary>Number of rows (the last used row).</summary>
    public int RowCount => Rows.Count;

    /// <summary>The row with a 1-based number, or null beyond the sheet.</summary>
    public SheetRow? Row(int number) => number >= 1 && number <= Rows.Count ? Rows[number - 1] : null;

    /// <summary>Column letters: 1 = A, 26 = Z, 27 = AA.</summary>
    public static string ColumnLetter(int column)
    {
        var letters = string.Empty;
        for (var n = column; n > 0; n = (n - 1) / 26)
        {
            letters = (char)('A' + ((n - 1) % 26)) + letters;
        }

        return letters;
    }
}
