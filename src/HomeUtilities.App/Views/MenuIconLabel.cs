namespace HomeUtilities.App.Views;

/// <summary>
/// A flyout menu icon drawn as text: a <see cref="Label"/> in the Material icon font that takes its glyph from the
/// <see cref="FontImageSource"/> of the menu item. On Windows a <see cref="FontImageSource"/> is rendered as a 96 dpi bitmap
/// squeezed into a 16 × 16 box and looks soft; text is drawn by the font renderer at the display's resolution, so it stays
/// sharp (owner 2026-11-06, like the section icons of Settings).
/// </summary>
public sealed class MenuIconLabel : Label
{
    public static readonly BindableProperty SourceProperty = BindableProperty.Create(
        nameof(Source),
        typeof(ImageSource),
        typeof(MenuIconLabel),
        null,
        propertyChanged: (view, _, value) => ((MenuIconLabel)view).Text = value is FontImageSource font ? font.Glyph : string.Empty);

    public MenuIconLabel()
    {
        SetDynamicResource(FontFamilyProperty, "IconFontFamily");
        FontSize = 22;
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Center;
        InputTransparent = true;
    }

    /// <summary>The menu item's icon; only a <see cref="FontImageSource"/> carries a glyph.</summary>
    public ImageSource? Source
    {
        get => (ImageSource?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }
}
