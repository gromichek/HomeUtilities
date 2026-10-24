namespace HomeUtilities.Core.Attachments;

/// <summary>Shrinks big photos before they are stored; implemented by the platform layer.</summary>
public interface IImageProcessor
{
    /// <summary>
    /// Returns the image to store: the same stream content when no change is needed (or decoding fails), or a smaller
    /// JPEG re-encoding with <see cref="ProcessedImage.ContentType"/> <c>image/jpeg</c>.
    /// </summary>
    /// <param name="content">The image bytes, positioned at the start.</param>
    /// <param name="contentType">Content type of <paramref name="content"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ProcessedImage> ProcessAsync(Stream content, string contentType, CancellationToken ct = default);
}

/// <summary>The result of <see cref="IImageProcessor.ProcessAsync"/>.</summary>
/// <param name="Content">The bytes to store, positioned at the start.</param>
/// <param name="ContentType">Content type of <paramref name="Content"/>.</param>
public sealed record ProcessedImage(Stream Content, string ContentType);
