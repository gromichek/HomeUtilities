using HomeUtilities.Core.Attachments;
using HomeUtilities.ViewModels;
#if ANDROID
using Android.Graphics;
#endif

namespace HomeUtilities.App.Services;

/// <summary>
/// Shrinks photos whose long side exceeds <see cref="MaxSide"/> pixels to that size and re-encodes them as JPEG (quality 85).
/// On Android a photo whose EXIF orientation is not "upright" is always rotated and re-encoded without EXIF, also when it is
/// small (T-042a). Other images and images that cannot be decoded are stored unchanged. Android uses <c>BitmapFactory</c>, Windows
/// <c>Windows.Graphics.Imaging</c> (<c>WindowsImageResizer</c>); the size rules are the same (<c>ImageSizing</c>).
/// </summary>
public sealed class ImageProcessor : IImageProcessor
{
    /// <summary>Longest side of a stored photo in pixels.</summary>
    public const int MaxSide = 2560;

    private const int JpegQuality = 85;

    private readonly IErrorLog? _errorLog;

    public ImageProcessor(IErrorLog? errorLog = null) => _errorLog = errorLog;

    public Task<ProcessedImage> ProcessAsync(Stream content, string contentType, CancellationToken ct = default)
    {
#if ANDROID
        return Task.Run(() => Shrink(content, contentType), ct);
#elif WINDOWS
        return ShrinkOnWindowsAsync(content, contentType, ct);
#else
        return Task.FromResult(Unchanged(content, contentType));
#endif
    }

    private static ProcessedImage Unchanged(Stream content, string contentType)
    {
        content.Position = 0;
        return new ProcessedImage(content, contentType);
    }

#if WINDOWS
    private static async Task<ProcessedImage> ShrinkOnWindowsAsync(Stream content, string contentType, CancellationToken ct)
    {
        var shrunk = await WindowsImageResizer.ResizeAsync(content, MaxSide, JpegQuality / 100d, alwaysEncode: false, ct);
        return shrunk is null ? Unchanged(content, contentType) : new ProcessedImage(shrunk, AttachmentContentTypes.Jpeg);
    }
#endif

#if ANDROID
    // Peak for a 4000x3000 JPEG (~5 MB), was ~100 MB: file bytes 5 MB (read once, no ToArray copy) + decoded straight to
    // 2560x1920 by the density scaling (19.7 MB, no full-size 48 MB bitmap, no separate scaled copy) + 19.7 MB for the
    // rotated copy only when the photo needs rotating (the decoded bitmap is recycled right after) = ~45 MB; ~25 MB without rotation.
    private ProcessedImage Shrink(Stream content, string contentType)
    {
        var orientationSeen = 1;
        try
        {
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            if (!buffer.TryGetBuffer(out var segment) || segment.Array is null)
            {
                return Unchanged(content, contentType);
            }

            var bytes = segment.Array;
            var offset = segment.Offset;
            var count = segment.Count;

            using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeByteArray(bytes, offset, count, bounds);
            var sourceWidth = bounds.OutWidth;
            var sourceHeight = bounds.OutHeight;
            var longSide = Math.Max(sourceWidth, sourceHeight);
            var orientation = ExifOrientation.Read(new ReadOnlySpan<byte>(bytes, offset, count));
            if (!ImageSizing.NeedsReencoding(sourceWidth, sourceHeight, MaxSide, orientation))
            {
                return Unchanged(new MemoryStream(bytes, offset, count, writable: false), contentType);
            }

            orientationSeen = orientation;
            var rotation = ImageSizing.RotationFor(orientation);
            var (targetWidth, targetHeight) = ImageSizing.Fit(sourceWidth, sourceHeight, MaxSide);

            // Power-of-two sample size while the result stays >= the target, then the decoder scales the rest (density scaling).
            var sampleSize = 1;
            while (longSide / (sampleSize * 2) >= MaxSide)
            {
                sampleSize *= 2;
            }

            using var decodeOptions = new BitmapFactory.Options
            {
                InSampleSize = sampleSize,
                InScaled = true,
                InDensity = longSide,
                InTargetDensity = Math.Max(targetWidth, targetHeight) * sampleSize,
            };
            var decoded = BitmapFactory.DecodeByteArray(bytes, offset, count, decodeOptions);
            if (decoded is null)
            {
                LogRotationFailed(orientation, null);
                return Unchanged(new MemoryStream(bytes, offset, count, writable: false), contentType);
            }

            var upright = decoded;
            try
            {
                if (rotation != 0)
                {
                    using var matrix = new Matrix();
                    matrix.PostRotate(rotation);
                    var rotated = Bitmap.CreateBitmap(decoded, 0, 0, decoded.Width, decoded.Height, matrix, true);
                    if (rotated is null)
                    {
                        LogRotationFailed(orientation, null);
                        return Unchanged(new MemoryStream(bytes, offset, count, writable: false), contentType);
                    }

                    decoded.Recycle();
                    upright = rotated;
                }

                var output = new MemoryStream();
                if (!upright.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, output))
                {
                    output.Dispose();
                    LogRotationFailed(orientation, null);
                    return Unchanged(new MemoryStream(bytes, offset, count, writable: false), contentType);
                }

                output.Position = 0;
                return new ProcessedImage(output, AttachmentContentTypes.Jpeg);
            }
            finally
            {
                if (!ReferenceEquals(upright, decoded))
                {
                    upright.Recycle();
                    upright.Dispose();
                }

                decoded.Recycle();
                decoded.Dispose();
            }
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or IOException or InvalidOperationException or OutOfMemoryException)
        {
            // Decoding failed (e.g. a HEIC the device cannot read): keep the original.
            LogRotationFailed(orientationSeen, ex);
            return Unchanged(content, contentType);
        }
    }

    // One line per photo, no personal data: only the orientation value and the exception (T-042a).
    private void LogRotationFailed(int orientation, Exception? ex)
    {
        if (orientation == 1)
        {
            return;
        }

        _errorLog?.Append(
            $"ImageProcessor: EXIF orientation {orientation} found but the photo was stored without rotation",
            ex ?? new InvalidOperationException("Bitmap.CreateBitmap returned null"));
    }
#endif
}
