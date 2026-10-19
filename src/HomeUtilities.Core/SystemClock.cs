namespace HomeUtilities.Core;

public sealed class SystemClock : IClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
