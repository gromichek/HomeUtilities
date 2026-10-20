using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>All values of one parameter (a kind, or a custom name), newest first, with the value valid today.</summary>
public sealed partial class ParameterGroupViewModel(
    string key, string title, string currentText, string historyText, IReadOnlyList<ParameterRowViewModel> rows)
    : ObservableObject
{
    /// <summary>Stable identity of the group across rebuilds (keeps the history expanded).</summary>
    public string Key { get; } = key;

    public string Title { get; } = title;

    public string CurrentText { get; } = currentText;

    /// <summary>"History (n)".</summary>
    public string HistoryText { get; } = historyText;

    /// <summary>History ordered by <c>ValidFrom</c> descending.</summary>
    public IReadOnlyList<ParameterRowViewModel> Rows { get; } = rows;

    /// <summary>Whether the history rows are shown; collapsed by default.</summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Called after the user expanded or collapsed the history.</summary>
    public Action<ParameterGroupViewModel>? Toggled { get; set; }

    [RelayCommand]
    private void ToggleHistory()
    {
        IsExpanded = !IsExpanded;
        Toggled?.Invoke(this);
    }
}
