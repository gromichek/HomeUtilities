using System.Globalization;
#if WINDOWS
using HomeUtilities.App.Services;
#endif

namespace HomeUtilities.App.Converters;

/// <summary>
/// Turns "a way to open the picture" (a <c>Func&lt;Task&lt;Stream&gt;&gt;</c>) into an image source for a thumbnail. Android (Glide) decodes
/// at the size of the view; WinUI would decode the whole bitmap (a 12 MP photo is ~48 MB per thumbnail), so on Windows the
/// picture is scaled down to <see cref="ThumbnailSide"/> while it is decoded (T-039). Done in memory, per display: no thumbnail
/// files to invalidate, clean up or keep out of the sync.
/// </summary>
public sealed class StreamFactoryImageConverter : IValueConverter
{
    /// <summary>Longest side of a thumbnail in pixels (the thumbnails are at most ~200 dp; 360 px is sharp at 175 % scale).</summary>
    public const int ThumbnailSide = 360;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Func<Task<Stream>> open ? ImageSource.FromStream(ct => OpenAsync(open, ct)) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

#if WINDOWS
    private static async Task<Stream> OpenAsync(Func<Task<Stream>> open, CancellationToken ct)
    {
        var original = await open();
        try
        {
            var thumbnail = await WindowsImageResizer.ResizeAsync(original, ThumbnailSide, 0.8, alwaysEncode: false, ct);
            if (thumbnail is null)
            {
                if (original.CanSeek)
                {
                    original.Position = 0;
                }

                return original;
            }

            await original.DisposeAsync();
            return thumbnail;
        }
        catch
        {
            await original.DisposeAsync();
            throw;
        }
    }
#else
    private static Task<Stream> OpenAsync(Func<Task<Stream>> open, CancellationToken ct) => open();
#endif
}
