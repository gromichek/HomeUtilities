namespace HomeUtilities.ViewModels;

public static class LayoutModes
{
    public const double MediumMinWidth = 600;
    public const double ExpandedAboveWidth = 1000;

    /// <summary>
    /// A window lower than this is treated as a phone whatever its width (a phone in landscape: ~900 x 400 dp), so the
    /// editors open as pages with the phone commands instead of a cramped layout.
    /// </summary>
    public const double CompactBelowHeight = 480;

    /// <summary>The mode from width and height; a low window (<see cref="CompactBelowHeight"/>) is always <see cref="LayoutMode.Compact"/>.</summary>
    public static LayoutMode FromSize(double widthDp, double heightDp) =>
        heightDp > 0 && heightDp < CompactBelowHeight ? LayoutMode.Compact : FromWidth(widthDp);

    public static LayoutMode FromWidth(double widthDp) =>
        widthDp < MediumMinWidth ? LayoutMode.Compact
        : widthDp <= ExpandedAboveWidth ? LayoutMode.Medium
        : LayoutMode.Expanded;
}
