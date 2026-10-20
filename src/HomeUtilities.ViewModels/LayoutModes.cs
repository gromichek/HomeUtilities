namespace HomeUtilities.ViewModels;

public static class LayoutModes
{
    public const double MediumMinWidth = 600;
    public const double ExpandedAboveWidth = 1000;

    public static LayoutMode FromWidth(double widthDp) =>
        widthDp < MediumMinWidth ? LayoutMode.Compact
        : widthDp <= ExpandedAboveWidth ? LayoutMode.Medium
        : LayoutMode.Expanded;
}
