using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Lets a section root keep its data and skip the reload when it appears again (T-028h): the data is fresh while nothing was
/// saved since the last load (<see cref="IDataChangeNotifier.Version"/>), the day is the same and the language did not change.
/// </summary>
public sealed class ReloadGate
{
    private readonly IDataChangeNotifier _changes;
    private readonly IClock _clock;
    private bool _valid;
    private long _version;
    private DateOnly _day;

    public ReloadGate(IDataChangeNotifier changes, IClock clock)
    {
        _changes = changes;
        _clock = clock;
    }

    /// <summary>True when the last load still shows the current data.</summary>
    public bool IsFresh => _valid && _version == _changes.Version && _day == _clock.Today;

    /// <summary>The version to remember: read it <b>before</b> the data is read, so a save during the load makes the load stale.</summary>
    public (long Version, DateOnly Day) Snapshot() => (_changes.Version, _clock.Today);

    /// <summary>Remembers that a load that started at <paramref name="snapshot"/> finished.</summary>
    public void MarkLoaded((long Version, DateOnly Day) snapshot)
    {
        _version = snapshot.Version;
        _day = snapshot.Day;
        _valid = true;
    }

    /// <summary>The next appearance reloads (language change, failed load).</summary>
    public void Invalidate() => _valid = false;
}
