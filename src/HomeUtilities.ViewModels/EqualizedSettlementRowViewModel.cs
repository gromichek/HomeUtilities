using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>One month of the settlement table.</summary>
public sealed class EqualizedSettlementRowViewModel
{
    public EqualizedSettlementRowViewModel(
        string periodText,
        string planText,
        string billedText,
        string actualText,
        string differenceText,
        string runningText,
        bool isComplete,
        decimal? difference,
        string missingText,
        Action openReadings)
    {
        PeriodText = periodText;
        PlanText = planText;
        BilledText = billedText;
        ActualText = actualText;
        DifferenceText = differenceText;
        RunningText = runningText;
        IsComplete = isComplete;
        IsSurcharge = difference > 0;
        IsRefund = difference < 0;
        MissingText = missingText;
        OpenReadingsCommand = new RelayCommand(openReadings);
    }

    /// <summary>"06.2026".</summary>
    public string PeriodText { get; }

    /// <summary>"Plan 40,00 €"; empty when no plan covers the month.</summary>
    public string PlanText { get; }

    public string BilledText { get; }

    public string ActualText { get; }

    public string DifferenceText { get; }

    public string RunningText { get; }

    public bool IsComplete { get; }

    public bool IsIncomplete => !IsComplete;

    /// <summary>The actual charge is above what was billed (red).</summary>
    public bool IsSurcharge { get; }

    /// <summary>The actual charge is below what was billed (green).</summary>
    public bool IsRefund { get; }

    /// <summary>"Readings missing" — a link to the readings screen; empty for complete months.</summary>
    public string MissingText { get; }

    public IRelayCommand OpenReadingsCommand { get; }
}
