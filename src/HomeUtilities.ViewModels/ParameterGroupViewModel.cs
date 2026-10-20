namespace HomeUtilities.ViewModels;

/// <summary>All values of one parameter (a kind, or a custom name), newest first, with the value valid today.</summary>
public sealed class ParameterGroupViewModel(string title, string currentText, IReadOnlyList<ParameterRowViewModel> rows)
{
    public string Title { get; } = title;

    public string CurrentText { get; } = currentText;

    /// <summary>History ordered by <c>ValidFrom</c> descending.</summary>
    public IReadOnlyList<ParameterRowViewModel> Rows { get; } = rows;
}
