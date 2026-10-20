namespace HomeUtilities.Core.Billing;

/// <summary>A non-blocking finding about a bill.</summary>
/// <param name="Code">Code from <see cref="BillWarningCodes"/>.</param>
/// <param name="Args">Values for the localized message, if any.</param>
public sealed record BillWarning(string Code, object?[]? Args = null);
