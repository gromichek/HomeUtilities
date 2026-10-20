namespace HomeUtilities.ViewModels;

/// <summary>Layout class chosen by available width, not by platform (ARCHITECTURE §4.9).</summary>
public enum LayoutMode
{
    /// <summary>Below 600 dp: phone.</summary>
    Compact,

    /// <summary>600–1000 dp: tablet portrait.</summary>
    Medium,

    /// <summary>Above 1000 dp: tablet landscape / desktop.</summary>
    Expanded,
}
