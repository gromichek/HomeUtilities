namespace HomeUtilities.ViewModels;

/// <summary>
/// Writes the exception to the <see cref="IErrorLog"/> and shows the standard error dialog
/// (<c>docs/UI_GUIDELINES.md</c> §5); Debug builds add the exception message.
/// </summary>
public sealed class ErrorReporter(IErrorLog log, IDialogService dialogs, ILocalizer localizer) : IErrorReporter
{
    public async Task ReportAsync(string source, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        log.Append(source, exception);
        try
        {
            var message = localizer["Error_Unexpected_Message"];
#if DEBUG
            message += Environment.NewLine + Environment.NewLine + exception.GetType().Name + ": " + exception.Message;
#endif
            await dialogs.AlertAsync(localizer["Error_Unexpected_Title"], message);
        }
        catch (Exception)
        {
            // The dialog itself failed (no page yet, shutting down); the log has the details.
        }
    }
}
