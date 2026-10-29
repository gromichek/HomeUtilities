using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>Picks the .xlsx workbook and the exported profile (.json) with the system file picker.</summary>
public sealed class ImportFileService : IImportFileService
{
    public Task<ImportPickedFile?> PickWorkbookAsync()
    {
        var excel = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/octet-stream"],
            [DevicePlatform.WinUI] = [".xlsx"],
            [DevicePlatform.iOS] = ["org.openxmlformats.spreadsheetml.sheet"],
            [DevicePlatform.MacCatalyst] = ["org.openxmlformats.spreadsheetml.sheet"],
        });
        return PickAsync(excel);
    }

    public Task<ImportPickedFile?> PickProfileAsync()
    {
        var json = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = ["application/json", "text/plain", "application/octet-stream"],
            [DevicePlatform.WinUI] = [".json"],
            [DevicePlatform.iOS] = ["public.json"],
            [DevicePlatform.MacCatalyst] = ["public.json"],
        });
        return PickAsync(json);
    }

    private static async Task<ImportPickedFile?> PickAsync(FilePickerFileType type)
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions { FileTypes = type });
        if (picked is null)
        {
            return null;
        }

        // The stream of a content URI (Android) is not always seekable or reusable; hand over a memory copy.
        var copy = new MemoryStream();
        await using (var source = await picked.OpenReadAsync())
        {
            await source.CopyToAsync(copy);
        }

        copy.Position = 0;
        return new ImportPickedFile(picked.FileName, copy);
    }
}
