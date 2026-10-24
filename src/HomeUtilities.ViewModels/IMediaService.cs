namespace HomeUtilities.ViewModels;

/// <summary>A file the user took or picked; the bytes are read on demand.</summary>
/// <param name="FileName">Original file name.</param>
/// <param name="ContentType">MIME type, e.g. <c>image/jpeg</c>.</param>
/// <param name="OpenAsync">Opens the content for reading; may be called more than once (thumbnail, then save).</param>
public sealed record PickedFile(string FileName, string ContentType, Func<Task<Stream>> OpenAsync);

/// <summary>Camera, gallery and file picker; implemented by the platform layer. A cancelled dialog returns null.</summary>
public interface IMediaService
{
    /// <summary>True where a camera can be used (Android phone / tablet); on Windows the user picks an image file instead.</summary>
    bool IsCaptureSupported { get; }

    /// <summary>Takes a photo with the camera.</summary>
    /// <exception cref="MediaPermissionDeniedException">The user did not allow camera access.</exception>
    Task<PickedFile?> CapturePhotoAsync();

    /// <summary>Picks an image from the gallery (Android) or the file system (Windows).</summary>
    Task<PickedFile?> PickPhotoAsync();

    /// <summary>Picks a file of one of the given MIME types.</summary>
    Task<PickedFile?> PickFileAsync(string[] contentTypes);
}

/// <summary>The user declined the permission a media action needs (e.g. the camera).</summary>
public sealed class MediaPermissionDeniedException : Exception
{
    public MediaPermissionDeniedException()
        : base("The permission for the requested media action was denied.")
    {
    }
}
