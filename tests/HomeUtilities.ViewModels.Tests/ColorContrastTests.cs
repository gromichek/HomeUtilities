using System.Text.RegularExpressions;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>C-08 and C-16: card outlines and the default category colours are visible (WCAG 3 : 1) in both themes.</summary>
public sealed class ColorContrastTests
{
    private static string Colour(string xaml, string key) =>
        Regex.Match(xaml, $"x:Key=\"{key}\">(#[0-9A-Fa-f]{{6}})<").Groups[1].Value;

    private static string ColorsXaml()
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "HomeUtilities.sln")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return File.ReadAllText(Path.Combine(directory!, "src", "HomeUtilities.App", "Resources", "Styles", "Colors.xaml"));
    }

    [Theory]
    [InlineData("OutlineLight", "CardLight")]
    [InlineData("OutlineLight", "SurfaceLight")]
    [InlineData("OutlineDark", "CardDark")]
    [InlineData("OutlineDark", "SurfaceDark")]
    public void CardOutline_HasAtLeastThreeToOneAgainstCardAndPage(string outline, string background)
    {
        var xaml = ColorsXaml();

        Assert.True(Contrast.Ratio(Colour(xaml, outline), Colour(xaml, background)) >= 3.0, $"{outline} on {background}");
    }

    [Fact]
    public void DefaultCategoryColours_AreVisibleOnTheLightCard_AndAreNotStatusColours()
    {
        var colours = CategoryNames.Palette.Concat(BuiltInCategories.Create().Select(c => c.Color!)).Distinct();

        foreach (var colour in colours)
        {
            Assert.True(Contrast.Ratio(colour, "#FFFFFF") >= 3.0, $"{colour} on the light card");
            ColorChecks.AssertNotStatusColor(colour);
        }
    }
}
