namespace HomeUtilities.Core.Abstractions;

/// <summary>Tells interested services (the reminders) that something was written to the database.</summary>
public interface IDataChangeNotifier
{
    /// <summary>Raised after every successful save. Handlers must be quick and must not throw.</summary>
    event EventHandler? Changed;

    /// <summary>Counts the saves so far; screens compare it with the value at their last load to know whether to reload (T-028h).</summary>
    long Version { get; }

    /// <summary>Raises <see cref="Changed"/> and increases <see cref="Version"/>.</summary>
    void Notify();
}

/// <summary>Default <see cref="IDataChangeNotifier"/>; a handler that throws never breaks the save that raised the event.</summary>
public sealed class DataChangeNotifier : IDataChangeNotifier
{
    private long _version;

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public long Version => Interlocked.Read(ref _version);

    /// <inheritdoc />
    public void Notify()
    {
        Interlocked.Increment(ref _version);
        foreach (var handler in Changed?.GetInvocationList().Cast<EventHandler>() ?? [])
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"A data change handler failed: {ex}");
            }
        }
    }
}
