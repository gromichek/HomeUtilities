namespace HomeUtilities.Core.Charges;

/// <summary>A non-fatal finding of a charge calculation.</summary>
/// <param name="Code">Code from <see cref="ChargeWarningCodes"/>.</param>
/// <param name="ComponentId">The tariff component the finding is about, if any.</param>
/// <param name="Args">Values for the localized message, if any.</param>
public sealed record ChargeWarning(string Code, Guid? ComponentId = null, object?[]? Args = null);
