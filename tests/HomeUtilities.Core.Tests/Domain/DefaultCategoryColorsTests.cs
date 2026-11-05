using System.Globalization;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class DefaultCategoryColorsTests
{
    /// <summary>
    /// The colour of a category dot must not look like a status (UI_GUIDELINES §8): no saturated red (hue 345°…15°) and no
    /// saturated green (hue 90°…165°). Existing categories keep their colours; this is about new ones.
    /// </summary>
    [Fact]
    public void BuiltInCategories_HaveNoStatusRedOrGreen()
    {
        foreach (var category in BuiltInCategories.Create())
        {
            AssertNotStatusColor(category.Color);
        }
    }

    /// <summary>Asserts that <paramref name="hex"/> (#RRGGBB) is not a saturated red or green.</summary>
    private static void AssertNotStatusColor(string hex)
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
