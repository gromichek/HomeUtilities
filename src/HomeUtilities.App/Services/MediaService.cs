using HomeUtilities.Core.Attachments;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>
/// Camera, gallery and file picker. The camera is used only on phones and tablets; on Windows the user picks an image
/// file instead (a webcam is rarely what is wanted for a meter photo).
/// </summary>
public sealed class MediaService : IMediaService
{
    public bool IsCaptureSupported => !OperatingSystem.IsWindows() && MediaPicker.Default.IsCaptureSupported;

    public async Task<PickedFile?> CapturePhotoAsync()
    {
        if (!IsCaptureSupported)
        {
            return null;
        }

        try
        {
            if (await Permissions.RequestAsync<Permissions.Camera>() != PermissionStatus.Granted)
            {
                throw new MediaPermissionDeniedException();
            }

            return Wrap(await MediaPicker.Default.CapturePhotoAsync());
        }
        catch (PermissionException)
        {
            throw new MediaPermissionDeniedException();
        }
    }

    public async Task<PickedFile?> PickPhotoAsync()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return Wrap(await FilePicker.Default.PickAsync(new PickOptions { FileTypes = FilePickerFileType.Images }));
            }

            // The picker may allow several photos; one photo belongs to a reading, so the first is taken.
            return Wrap((await MediaPicker.Default.PickPhotosAsync()).FirstOrDefault());
        }
        catch (PermissionException)
        {
            throw new MediaPermissionDeniedException();
        }
    }

    public async Task<PickedFile?> PickFileAsync(string[] contentTypes)
    {
        ArgumentNullException.ThrowIfNull(contentTypes);

        var fileType = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] = contentTypes,
            [DevicePlatform.WinUI] = [.. contentTypes.Select(AttachmentContentTypes.ExtensionFor).OfType<string>()],
            [DevicePlatform.iOS] = [.. contentTypes.Select(UniformTypeOf).OfType<string>()],
        });

        try
        {
            return Wrap(await FilePicker.Default.PickAsync(new PickOptions { FileTypes = fileType }));
        }
        catch (PermissionException)
        {
            throw new MediaPermissionDeniedException();
        }
    }

    private static PickedFile? Wrap(FileResult? result) =>
        result is null ? null : new PickedFile(result.FileName, result.ContentType ?? string.Empty, () => result.OpenReadAsync());

    private static string? UniformTypeOf(string contentType) => contentType switch
    {
        AttachmentContentTypes.Jpeg => "public.jpeg",
        AttachmentContentTypes.Png => "public.png",
        AttachmentContentTypes.Heic => "public.heic",
        AttachmentContentTypes.Pdf => "com.adobe.pdf",
        _ => null,
    };
}
