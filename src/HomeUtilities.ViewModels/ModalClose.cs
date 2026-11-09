namespace HomeUtilities.ViewModels;

/// <summary>
/// Closing a modal dialog page (R-5): whatever happens while it is popped, the "closed" signal is raised so the code waiting for
/// the dialog (<c>ShowEditorAsync</c>) always finishes; a failure is written to the error log and does not escape.
/// </summary>
public static class ModalClose
{
    public static async Task RunAsync(Func<Task> pop, Action markClosed, IErrorLog? errorLog)
    {
        ArgumentNullException.ThrowIfNull(pop);
        ArgumentNullException.ThrowIfNull(markClosed);

        try
        {
            await pop();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errorLog?.Append("DialogService.CloseModal", ex);
        }
        finally
        {
            markClosed();
        }
    }
}
