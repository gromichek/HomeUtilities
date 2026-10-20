namespace HomeUtilities.ViewModels;

public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Returns the chosen option or null if the user cancelled.</summary>
    Task<string?> ChooseAsync(string title, IReadOnlyList<string> options);
}
