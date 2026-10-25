namespace HomeUtilities.ViewModels;

/// <summary>
/// Shows a short, non-modal message at the bottom of the window, optionally with one action button ("Undo").
/// Only one message is visible at a time.
/// </summary>
public interface ISnackbarService
{
    /// <summary>
    /// Shows <paramref name="message"/> and completes when it disappears. A new message replaces the current one
    /// (the replaced call completes with false).
    /// </summary>
    /// <param name="message">The text.</param>
    /// <param name="actionText">Text of the action button; null for a plain message.</param>
    /// <param name="duration">How long the message stays; default 3 s, with an action 6 s.</param>
    /// <returns>True if the action button was tapped before the message disappeared.</returns>
    Task<bool> ShowAsync(string message, string? actionText = null, TimeSpan? duration = null);
}

/// <summary>Helpers of <see cref="ISnackbarService"/>.</summary>
public static class SnackbarServiceExtensions
{
    /// <summary>
    /// Shows a plain confirmation ("Saved", "Copied") without waiting until it disappears, so the caller carries on at once.
    /// </summary>
    public static void Notify(this ISnackbarService snackbar, string message)
    {
        ArgumentNullException.ThrowIfNull(snackbar);
        _ = snackbar.ShowAsync(message);
    }
}
