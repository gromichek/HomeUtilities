using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>Full-screen modal page that hosts a child-item editor on the compact layout.</summary>
public sealed class EditorDialogPage : ContentPage
{
    private readonly TaskCompletionSource _closed = new();
    private readonly EditorDialogViewModel _editor;

    public EditorDialogPage(View content, EditorDialogViewModel editor)
    {
        _editor = editor;
        Content = content;
        BindingContext = editor;
    }

    /// <summary>Completes when the page has been removed.</summary>
    public Task Closed => _closed.Task;

    // Hardware / gesture Back behaves like the dialog's Cancel button.
    protected override bool OnBackButtonPressed()
    {
        _editor.CancelCommand.Execute(null);
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _closed.TrySetResult();
    }
}
