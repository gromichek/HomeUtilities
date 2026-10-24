using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Services;

/// <summary>The view model of the page on top of the current section (a list screen answers for its side-pane editor).</summary>
public sealed class ShellActiveEditorProvider : IActiveEditorProvider
{
    public IUnsavedChangesGuarded? Current => Shell.Current?.CurrentPage?.BindingContext switch
    {
        // The layout became two-pane: this page is about to pop itself and the editor lives in the side pane,
        // so closing the page loses nothing.
        PaneEditorViewModel { IsHostedInPage: false } => null,
        IUnsavedChangesGuarded guarded => guarded,
        _ => null,
    };
}
