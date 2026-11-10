using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One part of the stacked bar of the "by category" card.</summary>
/// <param name="Fraction">Share of the whole, 0–1.</param>
/// <param name="Color">Colour as <c>#RRGGBB</c> (the category's own colour).</param>
public sealed record ShareSegment(double Fraction, string Color);

/// <summary>One tappable row of the "by category" card: colour dot, name, amount and share; opens the bills of the category.</summary>
public sealed class CategoryRowViewModel(string name, string color, string amountText, string shareText, string description, Func<Task> open)
{
    public string Name { get; } = name;

    /// <summary>Colour of the dot as <c>#RRGGBB</c>.</summary>
    public string Color { get; } = color;

    public string AmountText { get; } = amountText;

    /// <summary>"12 %"; empty if the share is not shown.</summary>
    public string ShareText { get; } = shareText;

    public bool HasShare => ShareText.Length > 0;

    /// <summary>Accessibility text: "Water: 120,00 €, 12 %".</summary>
    public string Description { get; } = description;

    public IAsyncRelayCommand OpenCommand { get; } = Row.Command(open);
}

/// <summary>The data of a small line chart: twelve monthly values; a null value is a gap, an approximate one is drawn muted.</summary>
/// <param name="Values">The values, oldest first.</param>
/// <param name="Approximate">One flag per value.</param>
/// <param name="Color">Line colour as <c>#RRGGBB</c>.</param>
public sealed record SparklineData(IReadOnlyList<decimal?> Values, IReadOnlyList<bool> Approximate, string Color);

/// <summary>One row of the "Consumption" card: service, last complete month, change against the average and a small line.</summary>
public sealed class ConsumptionRowViewModel(
    string name,
    string valueText,
    string changeText,
    bool isRise,
    string riseText,
    SparklineData? line,
    string description,
    Func<Task> open)
{
    public string Name { get; } = name;

    /// <summary>"14 m³" (with "≈" if the month rests on incomplete data); "—" if the month is not known.</summary>
    public string ValueText { get; } = valueText;

    /// <summary>"+8 %" against the average of the eleven months before; empty if it cannot be calculated.</summary>
    public string ChangeText { get; } = changeText;

    public bool HasChange => ChangeText.Length > 0;

    /// <summary>A noticeable rise: the change is drawn in the warning colour with an icon. A hint, not an alarm.</summary>
    public bool IsRise { get; } = isRise;

    /// <summary>"Higher than usual": accessibility and tooltip text of the rise icon.</summary>
    public string RiseText { get; } = riseText;

    /// <summary>The line of the last twelve months; null with fewer than three months of data.</summary>
    public SparklineData? Line { get; } = line;

    public bool HasLine => Line is not null;

    /// <summary>Accessibility text of the whole row.</summary>
    public string Description { get; } = description;

    public IAsyncRelayCommand OpenCommand { get; } = Row.Command(open);
}
