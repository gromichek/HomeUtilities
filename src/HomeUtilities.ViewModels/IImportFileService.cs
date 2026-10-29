namespace HomeUtilities.ViewModels;

/// <summary>A file the owner picked: its name and a readable stream.</summary>
/// <param name="Name">File name without the folder.</param>
/// <param name="Content">The content; the receiver disposes it.</param>
public sealed record ImportPickedFile(string Name, Stream Content);

/// <summary>Lets the owner pick the files of the Excel import; implemented per platform.</summary>
public interface IImportFileService
{
    /// <summary>Picks an .xlsx workbook; null if cancelled.</summary>
    Task<ImportPickedFile?> PickWorkbookAsync();

    /// <summary>Picks an exported import profile (.json); null if cancelled.</summary>
    Task<ImportPickedFile?> PickProfileAsync();
}
