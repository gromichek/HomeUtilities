namespace HomeUtilities.ViewModels.Common;

/// <summary>A card of a list screen that can be highlighted as the item opened last.</summary>
public interface ISelectableListItem
{
    /// <summary>True for the item opened last (it stays highlighted when the user comes back from its editor).</summary>
    bool IsSelected { get; }
}
