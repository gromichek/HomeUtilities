namespace HomeUtilities.Core.Abstractions;

/// <summary>Tells interested services (the reminders) that something was written to the database.</summary>
public interface IDataChangeNotifier
{
    /// <summary>Raised after every successful save. Handlers must be quick and must not throw.</summary>
    event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/>.</summary>
    void Notify();
}

/// <summary>Default <see cref="IDataChangeNotifier"/>; a handler that throws never breaks the save that raised the event.</summary>
public sealed class DataChangeNotifier : IDataChangeNotifier
{
    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public void Notify()
    {
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
