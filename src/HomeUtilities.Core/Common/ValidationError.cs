namespace HomeUtilities.Core.Common;

/// <summary>A single validation problem, identified by a code rather than a message.</summary>
/// <param name="Code">Error code from <see cref="ErrorCodes"/>.</param>
/// <param name="Field">Name of the offending field, if any.</param>
/// <param name="Args">Values for the localized message, if any.</param>
public sealed record ValidationError(string Code, string? Field = null, object?[]? Args = null);
