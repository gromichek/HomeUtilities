namespace HomeUtilities.Core.Attachments;

/// <summary>
/// The size rules of stored photos and thumbnails, shared by the platform image code (T-039): the longest side is limited,
/// the aspect ratio is kept, nothing is ever enlarged. Pure arithmetic, so it is unit-tested on every platform.
/// </summary>
public static class ImageSizing
{
    /// <summary>
    /// The size an image of <paramref name="width"/> × <paramref name="height"/> pixels (as shown, i.e. after the EXIF
    /// rotation) has when its longest side is limited to <paramref name="maxSide"/>. Returns the original size when it already
    /// fits.
    /// </summary>
    public static (int Width, int Height) Fit(int width, int height, int maxSide)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSide);
        if (width <= 0 || height <= 0)
        {
            return (Math.Max(0, width), Math.Max(0, height));
        }

        var longSide = Math.Max(width, height);
        if (longSide <= maxSide)
        {
            return (width, height);
        }

        var scale = (double)maxSide / longSide;
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    /// <summary>Whether an image of this size has to be shrunk to fit <paramref name="maxSide"/>.</summary>
    public static bool NeedsShrinking(int width, int height, int maxSide) => Math.Max(width, height) > maxSide;

    /// <summary>
    /// Clockwise rotation in degrees (0, 90, 180, 270) that makes an image upright for an EXIF orientation value (1–8).
    /// Mirrored orientations (2, 4, 5, 7) are not produced by cameras and are treated as their unmirrored rotation.
    /// </summary>
    public static int RotationFor(int exifOrientation) => exifOrientation switch
    {
        3 or 4 => 180,
        6 or 5 => 90,
        8 or 7 => 270,
        _ => 0,
    };

    /// <summary>Whether the rotation swaps width and height.</summary>
    public static bool SwapsSides(int exifOrientation) => RotationFor(exifOrientation) is 90 or 270;
}
