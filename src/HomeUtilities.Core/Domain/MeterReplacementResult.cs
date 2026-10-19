namespace HomeUtilities.Core.Domain;

/// <summary>Entities created by <see cref="MeterReplacement"/>.</summary>
/// <param name="NewMeter">The new meter with its registers; the Initial readings are inside the registers.</param>
/// <param name="FinalReadings">Final readings added to the registers of the old meter.</param>
/// <param name="InitialReadings">Initial readings added to the registers of the new meter.</param>
public sealed record MeterReplacementResult(
    Meter NewMeter,
    IReadOnlyList<MeterReading> FinalReadings,
    IReadOnlyList<MeterReading> InitialReadings);
