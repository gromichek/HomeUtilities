using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Base class of all view models. Receives <see cref="LanguageChangedMessage"/> through the weak messenger,
/// so a view model never needs to unsubscribe and is never kept alive by a service.
/// </summary>
public abstract partial class ViewModelBase : ObservableObject, IRecipient<LanguageChangedMessage>
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _title = string.Empty;

    protected ViewModelBase()
    {
        WeakReferenceMessenger.Default.RegisterAll(this);
    }

    /// <summary>
    /// Where <see cref="RunSafeAsync"/> sends unexpected exceptions; set once at app startup. When null (unit tests)
    /// exceptions are rethrown, so a test notices a handler that fails.
    /// </summary>
    public static IErrorReporter? ErrorReporter { get; set; }

    public virtual Task OnAppearingAsync() => Task.CompletedTask;

    public virtual Task OnDisappearingAsync() => Task.CompletedTask;

    /// <summary>Re-reads all localized texts; called after the language changed.</summary>
    protected virtual void RefreshTexts()
    {
    }

    void IRecipient<LanguageChangedMessage>.Receive(LanguageChangedMessage message) => RefreshTexts();

    /// <summary>
    /// Runs a command handler so that an unexpected exception is logged and shown with the standard error dialog
    /// instead of crashing the app. Every async command handler goes through here.
    /// </summary>
    protected async Task RunSafeAsync(Func<Task> action, [System.Runtime.CompilerServices.CallerMemberName] string source = "")
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (ErrorReporter is not { } reporter)
            {
                throw;
            }

            await reporter.ReportAsync($"{GetType().Name}.{source}", ex);
        }
    }
}
