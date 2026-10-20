namespace HomeUtilities.ViewModels;

/// <summary>Sent through the weak messenger when <see cref="ILayoutModeService.Current"/> changes.</summary>
public sealed record LayoutModeChangedMessage(LayoutMode Mode);
