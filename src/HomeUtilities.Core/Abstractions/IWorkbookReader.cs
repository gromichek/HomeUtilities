using HomeUtilities.Core.Import;

namespace HomeUtilities.Core.Abstractions;

/// <summary>Reads .xlsx workbooks for the import wizard. Both methods are synchronous and heavy: call them off the UI thread.</summary>
public interface IWorkbookReader
{
    /// <summary>The visible sheets with their sizes.</summary>
    /// <exception cref="InvalidDataException">The stream is not a readable .xlsx file.</exception>
    IReadOnlyList<SheetInfo> ListSheets(Stream workbook);

    /// <summary>Reads all cells of a sheet (merged header cells repeated, formulas as cached values).</summary>
    /// <exception cref="InvalidDataException">The stream is not a readable .xlsx file or the sheet does not exist.</exception>
    SheetData ReadSheet(Stream workbook, string sheetName);
}
