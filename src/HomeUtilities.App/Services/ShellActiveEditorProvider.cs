using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Services;

/// <summary>The view model of the page on top of the current section (an editor page or a screen with typed values).</summary>
public sealed class ShellActiveEditorProvider : IActiveEditorProvider
{
    public IUnsavedChangesGuarded? Current => Shell.Current?.CurrentPage?.BindingContext switch
    {
        IUnsavedChangesGuarded guarded => guarded,
        _ => null,
    };
}
