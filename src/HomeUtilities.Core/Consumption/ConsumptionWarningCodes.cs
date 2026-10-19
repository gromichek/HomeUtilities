namespace HomeUtilities.Core.Consumption;

/// <summary>Codes of <see cref="ConsumptionWarning"/>. The UI maps them to localized messages.</summary>
public static class ConsumptionWarningCodes
{
    /// <summary>The end reading is lower than the start reading and a rollover of the dial was assumed.</summary>
    public const string RolloverAssumed = "consumption.rollover_assumed";

    /// <summary>The end reading is lower than the start reading and the number of digits is unknown.</summary>
    public const string NegativeConsumption = "consumption.negative_consumption";

    /// <summary>No reading on or before the start date.</summary>
    public const string MissingStartReading = "consumption.missing_start_reading";

    /// <summary>No reading on or before the end date.</summary>
    public const string MissingEndReading = "consumption.missing_end_reading";

    /// <summary>A replaced meter has no Final reading.</summary>
    public const string MissingFinalReading = "consumption.missing_final_reading";

    /// <summary>A replacing meter has no readings.</summary>
    public const string MissingInitialReading = "consumption.missing_initial_reading";

    /// <summary>An estimated reading was used.</summary>
    public const string EstimatedReadingUsed = "consumption.estimated_reading_used";

    /// <summary>No reading was found inside the reading window; an earlier one was used.</summary>
    public const string ReadingOutsideWindow = "consumption.reading_outside_window";

    /// <summary>Consumption is much higher than usual.</summary>
    public const string UnusuallyHigh = "consumption.unusually_high";

    /// <summary>Consumption is zero although it is usually positive.</summary>
    public const string ZeroConsumption = "consumption.zero";
}
