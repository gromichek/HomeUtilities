using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class DialogService(ILocalizer localizer) : IDialogService
{
    public Task AlertAsync(string title, string message) =>
        Page.DisplayAlertAsync(title, message, "OK");

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Page.DisplayAlertAsync(title, message, accept, cancel);

    public async Task<string?> ChooseAsync(string title, IReadOnlyList<string> options)
    {
        var cancel = localizer["Cancel"];
        var result = await Page.DisplayActionSheetAsync(title, cancel, null, [.. options]);
        return result is null || result == cancel ? null : result;
    }

    private static Page Page =>
        Shell.Current ?? Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("No page is available to show a dialog.");
}
