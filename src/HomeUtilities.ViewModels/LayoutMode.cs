namespace HomeUtilities.ViewModels;

/// <summary>Layout class chosen by available width, not by platform (ARCHITECTURE §4.9).</summary>
public enum LayoutMode
{
    /// <summary>Below 600 dp wide, or a window lower than 480 dp (phone in landscape): phone.</summary>
    Compact,

    /// <summary>600–1000 dp: tablet portrait.</summary>
    Medium,

    /// <summary>Above 1000 dp: tablet landscape / desktop.</summary>
    Expanded,
}
