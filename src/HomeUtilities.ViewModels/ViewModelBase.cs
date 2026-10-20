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

    public virtual Task OnAppearingAsync() => Task.CompletedTask;

    public virtual Task OnDisappearingAsync() => Task.CompletedTask;

    /// <summary>Re-reads all localized texts; called after the language changed.</summary>
    protected virtual void RefreshTexts()
    {
    }

    void IRecipient<LanguageChangedMessage>.Receive(LanguageChangedMessage message) => RefreshTexts();
}
