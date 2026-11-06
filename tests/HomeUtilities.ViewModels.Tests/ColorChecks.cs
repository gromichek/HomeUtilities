using System.Globalization;

namespace HomeUtilities.ViewModels.Tests;

internal static class ColorChecks
{
    /// <summary>Asserts that <paramref name="hex"/> (#RRGGBB) is not a saturated red or green.</summary>
    public static void AssertNotStatusColor(string hex)
    {
        var r = int.Parse(hex[1..3], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var g = int.Parse(hex[3..5], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var b = int.Parse(hex[5..7], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        if (delta < 0.0001 || delta / max < 0.5)
        {
            return; // grey, brown or washed out: cannot be mistaken for a status colour
        }

        var hue = max == r ? 60 * (((g - b) / delta) % 6)
            : max == g ? 60 * (((b - r) / delta) + 2)
            : 60 * (((r - g) / delta) + 4);
        hue = (hue + 360) % 360;
        Assert.False(hue >= 345 || hue <= 15, $"{hex} (hue {hue:0}°) looks like the status red");
        Assert.False(hue is >= 90 and <= 165, $"{hex} (hue {hue:0}°) looks like the status green");
    }
}

internal static class Contrast
{
    /// <summary>WCAG contrast ratio of two #RRGGBB colours.</summary>
    public static double Ratio(string a, string b)
    {
        var (x, y) = (Luminance(a), Luminance(b));
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    private static double Luminance(string hex)
    {
        double Channel(int start)
        {
            var c = int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(1)) + (0.7152 * Channel(3)) + (0.0722 * Channel(5));
    }
}
