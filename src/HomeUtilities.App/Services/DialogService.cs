using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using HomeUtilities.App.Views;
using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Services;

public sealed class DialogService(ILocalizer localizer, ILayoutModeService layoutMode) : IDialogService
{
    private const double PopupWidth = 480;

    public Task AlertAsync(string title, string message) =>
        Page.DisplayAlertAsync(title, message, localizer["Dialog_Ok"]);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Page.DisplayAlertAsync(title, message, accept, cancel);

    public async Task<string?> ChooseAsync(string title, IReadOnlyList<string> options)
    {
        var cancel = localizer["Cancel"];
        var result = await Page.DisplayActionSheetAsync(title, cancel, null, [.. options]);
        return result is null || result == cancel ? null : result;
    }

    // An action sheet gives three buttons on both platforms; dismissing it is the same as Cancel.
    public async Task<UnsavedChangesChoice> AskUnsavedChangesAsync()
    {
        var save = localizer["Dialog_Unsaved_Save"];
        var dontSave = localizer["Dialog_Unsaved_DontSave"];
        var result = await Page.DisplayActionSheetAsync(localizer["Dialog_Unsaved_Title"], localizer["Dialog_Unsaved_Cancel"], dontSave, save);
        return result == save
            ? UnsavedChangesChoice.Save
            : result == dontSave ? UnsavedChangesChoice.DontSave : UnsavedChangesChoice.Cancel;
    }

    public async Task<bool> ShowEditorAsync(EditorDialogViewModel editor)
    {
        var view = CreateView(editor);
        if (layoutMode.Current == LayoutMode.Compact)
        {
            var modal = new EditorDialogPage(view, editor);
            Action close = () => _ = modal.Navigation.PopModalAsync();
            editor.CloseRequested += close;
            try
            {
                await Page.Navigation.PushModalAsync(modal);
                await modal.Closed;
            }
            finally
            {
                editor.CloseRequested -= close;
            }
        }
        else
        {
            var host = Page;
            view.WidthRequest = PopupWidth;
            Action close = () => _ = host.ClosePopupAsync();
            editor.CloseRequested += close;
            try
            {
                await host.ShowPopupAsync(view, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = false });
            }
            finally
            {
                editor.CloseRequested -= close;
            }
        }

        return editor.Saved;
    }

    // One view per dialog view model; add a case with every new child-item dialog.
    private static View CreateView(EditorDialogViewModel editor) => editor switch
    {
        ParameterEditorViewModel => new ParameterEditorView { BindingContext = editor },
        LinkEditorViewModel => new LinkEditorView { BindingContext = editor },
        _ => throw new NotSupportedException($"No dialog view is registered for {editor.GetType().Name}."),
    };

    private static Page Page =>
        Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("No page is available to show a dialog.");
}
