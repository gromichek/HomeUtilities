namespace HomeUtilities.Core.Consumption;

/// <summary>A non-fatal finding of a consumption calculation.</summary>
/// <param name="Code">Code from <see cref="ConsumptionWarningCodes"/>.</param>
/// <param name="Args">Values for the localized message, if any.</param>
public sealed record ConsumptionWarning(string Code, object?[]? Args = null);
