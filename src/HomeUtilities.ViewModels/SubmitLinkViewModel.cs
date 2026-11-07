using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>A "Submit: {supplier}" button on a meter card; opens the supplier's page for the meter's contracts.</summary>
public sealed partial class SubmitLinkViewModel : ObservableObject
{
    public SubmitLinkViewModel(Uri uri, IReadOnlySet<Guid> contractIds, string text, IAsyncRelayCommand command)
    {
        Uri = uri;
        ContractIds = contractIds;
        Text = text;
        Command = command;
    }

    /// <summary>The page that is opened.</summary>
    public Uri Uri { get; }

    /// <summary>The contracts whose readings are submitted there (several when their links are identical).</summary>
    public IReadOnlySet<Guid> ContractIds { get; }

    /// <summary>"Submit: {supplier}".</summary>
    public string Text { get; }

    public IAsyncRelayCommand Command { get; }

    /// <summary>"submitted 07.11.2026" after the readings were marked as submitted; the button stays.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSubmittedText))]
    private string _submittedText = string.Empty;

    public bool HasSubmittedText => SubmittedText.Length > 0;
}
