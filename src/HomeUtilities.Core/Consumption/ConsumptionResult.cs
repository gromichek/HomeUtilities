using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Consumption;

/// <summary>Result of a consumption calculation.</summary>
/// <param name="Quantity">Total consumption rounded to 3 decimals; 0 if the result is incomplete.</param>
/// <param name="StartReading">The reading the interval starts at, if found.</param>
/// <param name="EndReading">The reading the interval ends at, if found.</param>
/// <param name="Segments">One segment per involved meter, old to new.</param>
/// <param name="Warnings">Findings the user should look at.</param>
/// <param name="IsComplete">False if the consumption could not be determined.</param>
public sealed record ConsumptionResult(
    decimal Quantity,
    MeterReading? StartReading,
    MeterReading? EndReading,
    IReadOnlyList<ConsumptionSegment> Segments,
    IReadOnlyList<ConsumptionWarning> Warnings,
    bool IsComplete);
