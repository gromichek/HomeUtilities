using HomeUtilities.Core;

namespace HomeUtilities.Core.Tests.Fakes;

/// <summary>Test clock with a settable time; <see cref="Today"/> is derived from it.</summary>
public sealed class FakeClock : IClock
{
    public FakeClock(DateTimeOffset utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTimeOffset UtcNow { get; set; }

    public DateOnly Today => DateOnly.FromDateTime(UtcNow.UtcDateTime);
}
