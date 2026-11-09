#if WINDOWS
using HomeUtilities.Core.Attachments;
using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace HomeUtilities.App.Services;

/// <summary>
/// Windows implementation of the photo shrinking (T-039), with <c>Windows.Graphics.Imaging</c> (part of Windows, no package).
/// The decoder scales while it decodes, so a 12 MP photo never exists as a full-size bitmap in memory. The EXIF orientation is
/// applied to the pixels (the stored JPEG is upright), the sizes come from <see cref="ImageSizing"/>.
/// </summary>
internal static class WindowsImageResizer
{
    /// <summary>
    /// Returns the image as a JPEG whose longest side is at most <paramref name="maxSide"/>, or null when the image already fits
    /// (and <paramref name="alwaysEncode"/> is false), cannot be decoded, or the result would not be smaller.
    /// </summary>
    public static async Task<MemoryStream?> ResizeAsync(Stream source, int maxSide, double quality, bool alwaysEncode, CancellationToken ct)
    {
        try
        {
            using var input = new InMemoryRandomAccessStream();
            if (source.CanSeek)
            {
                source.Position = 0;
            }

            var writer = input.AsStreamForWrite();
            await source.CopyToAsync(writer, ct);
            await writer.FlushAsync(ct);
            input.Seek(0);

            var decoder = await BitmapDecoder.CreateAsync(input);
            var shownWidth = (int)decoder.OrientedPixelWidth;
            var shownHeight = (int)decoder.OrientedPixelHeight;
            if (!alwaysEncode && !ImageSizing.NeedsShrinking(shownWidth, shownHeight, maxSide))
            {
                return null;
            }

            var (width, height) = ImageSizing.Fit(shownWidth, shownHeight, maxSide);

            // The transform scales first and applies the EXIF rotation afterwards, so the scaled size is given in the stored
            // (unrotated) orientation; for a portrait phone photo (stored landscape, EXIF 90/270) width and height swap here.
            var rotated = decoder.PixelWidth != decoder.OrientedPixelWidth;
            var transform = new BitmapTransform
            {
                ScaledWidth = (uint)(rotated ? height : width),
                ScaledHeight = (uint)(rotated ? width : height),
                InterpolationMode = BitmapInterpolationMode.Fant,
            };

            var pixels = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Ignore,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.DoNotColorManage);

            using var output = new InMemoryRandomAccessStream();
            var options = new BitmapPropertySet
            {
                ["ImageQuality"] = new BitmapTypedValue((float)quality, Windows.Foundation.PropertyType.Single),
            };
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, output, options);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, 96, 96, pixels.DetachPixelData());
            await encoder.FlushAsync();

            var result = new MemoryStream();
            output.Seek(0);
            await output.AsStreamForRead().CopyToAsync(result, ct);
            if (!alwaysEncode && source.CanSeek && result.Length >= source.Length)
            {
                result.Dispose();
                return null;
            }

            result.Position = 0;
            return result;
        }
        catch (Exception ex) when (ex is COMException or IOException or InvalidOperationException or ArgumentException or NotSupportedException or OutOfMemoryException)
        {
            // Not an image Windows can decode: the caller keeps the original.
            return null;
        }
    }
}
#endif
