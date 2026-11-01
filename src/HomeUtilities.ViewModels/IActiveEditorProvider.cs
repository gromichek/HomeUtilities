namespace HomeUtilities.ViewModels;

/// <summary>Tells which view model is on screen right now and could hold unsaved changes.</summary>
public interface IActiveEditorProvider
{
    /// <summary>The topmost page's view model (an editor page); null when nothing can be unsaved.</summary>
    IUnsavedChangesGuarded? Current { get; }
}
