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

    /// <summary>
    /// The mode from width and height. On Android a low window (<see cref="CompactBelowHeight"/>: a phone in landscape) is always
    /// <see cref="LayoutMode.Compact"/>; on Windows only the width counts, so a short but wide desktop window keeps the desktop
    /// layout (owner 2026-11-05, UI_GUIDELINES §3.5).
    /// </summary>
    /// <param name="widthDp">Window width in dp.</param>
    /// <param name="heightDp">Window height in dp.</param>
    /// <param name="lowWindowIsPhone">True on Android: a low window is a phone.</param>
    public static LayoutMode FromSize(double widthDp, double heightDp, bool lowWindowIsPhone = true) =>
        lowWindowIsPhone && heightDp > 0 && heightDp < CompactBelowHeight ? LayoutMode.Compact : FromWidth(widthDp);

    public static LayoutMode FromWidth(double widthDp) =>
        widthDp < MediumMinWidth ? LayoutMode.Compact
        : widthDp <= ExpandedAboveWidth ? LayoutMode.Medium
        : LayoutMode.Expanded;
}
