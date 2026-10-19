namespace HomeUtilities.Core;

/// <summary>Source of the current date and time; replaceable in tests.</summary>
public interface IClock
{
    DateOnly Today { get; }

    DateTimeOffset UtcNow { get; }
}
