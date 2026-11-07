using HomeUtilities.Core.Attachments;
#if ANDROID
using Android.Graphics;
#endif

namespace HomeUtilities.App.Services;

/// <summary>
/// Shrinks photos whose long side exceeds <see cref="MaxSide"/> pixels to that size and re-encodes them as JPEG (quality 85).
/// Smaller images and images that cannot be decoded are stored unchanged. Only implemented on Android, where the
/// big camera photos come from; elsewhere every image is stored as it is.
/// </summary>
public sealed class ImageProcessor : IImageProcessor
{
    /// <summary>Longest side of a stored photo in pixels.</summary>
    public const int MaxSide = 2560;

    private const int JpegQuality = 85;

    public Task<ProcessedImage> ProcessAsync(Stream content, string contentType, CancellationToken ct = default)
    {
#if ANDROID
        return Task.Run(() => Shrink(content, contentType), ct);
#else
        return Task.FromResult(Unchanged(content, contentType));
#endif
    }

    private static ProcessedImage Unchanged(Stream content, string contentType)
    {
        content.Position = 0;
        return new ProcessedImage(content, contentType);
    }

#if ANDROID
    private static ProcessedImage Shrink(Stream content, string contentType)
    {
        try
        {
            using var buffer = new MemoryStream();
            content.CopyTo(buffer);
            var bytes = buffer.ToArray();

            using var bounds = new BitmapFactory.Options { InJustDecodeBounds = true };
            BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, bounds);
            var longSide = Math.Max(bounds.OutWidth, bounds.OutHeight);
            if (longSide <= MaxSide)
            {
                return Unchanged(new MemoryStream(bytes), contentType);
            }

            var sampleSize = 1;
            while (longSide / (sampleSize * 2) >= MaxSide)
            {
                sampleSize *= 2;
            }

            using var decodeOptions = new BitmapFactory.Options { InSampleSize = sampleSize };
            using var decoded = BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length, decodeOptions);
            if (decoded is null)
            {
                return Unchanged(new MemoryStream(bytes), contentType);
            }

            var scale = (double)MaxSide / Math.Max(decoded.Width, decoded.Height);
            var width = Math.Max(1, (int)Math.Round(decoded.Width * Math.Min(1d, scale)));
            var height = Math.Max(1, (int)Math.Round(decoded.Height * Math.Min(1d, scale)));
            using var scaled = Bitmap.CreateScaledBitmap(decoded, width, height, true);

            // Decoding drops the EXIF orientation; rotate the pixels so that a portrait photo stays upright.
            using var matrix = new Matrix();
            matrix.PostRotate(RotationDegrees(bytes));
            using var upright = scaled is null ? null : Bitmap.CreateBitmap(scaled, 0, 0, scaled.Width, scaled.Height, matrix, true);
            if (upright is null)
            {
                return Unchanged(new MemoryStream(bytes), contentType);
            }

            var output = new MemoryStream();
            if (!upright.Compress(Bitmap.CompressFormat.Jpeg!, JpegQuality, output))
            {
                output.Dispose();
                return Unchanged(new MemoryStream(bytes), contentType);
            }

            output.Position = 0;
            return new ProcessedImage(output, AttachmentContentTypes.Jpeg);
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or IOException or InvalidOperationException or OutOfMemoryException)
        {
            // Decoding failed (e.g. a HEIC the device cannot read): keep the original.
            return Unchanged(content, contentType);
        }
    }

    private static float RotationDegrees(byte[] jpeg)
    {
        try
        {
            using var stream = new MemoryStream(jpeg);
            using var exif = new Android.Media.ExifInterface(stream);
            return exif.GetAttributeInt(Android.Media.ExifInterface.TagOrientation, 1) switch
            {
                6 => 90f,
                3 => 180f,
                8 => 270f,
                _ => 0f,
            };
        }
        catch (Exception ex) when (ex is Java.Lang.Exception or IOException)
        {
            return 0f;
        }
    }
#endif
}
