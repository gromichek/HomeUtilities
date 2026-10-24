namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// Remembers which item was open in the side pane of a two-pane list screen, per section. The screen's page and view
/// model may be recreated when the user leaves the section and comes back, so the selection must not live there.
/// </summary>
public interface ISectionSelectionMemory
{
    /// <summary>Stores the selected item of the section, or forgets it when <paramref name="id"/> is null.</summary>
    void Remember(string sectionKey, Guid? id);

    /// <summary>The remembered item of the section, or null.</summary>
    Guid? Recall(string sectionKey);
}

/// <summary>Process-wide, in-memory implementation (registered as a singleton).</summary>
public sealed class SectionSelectionMemory : ISectionSelectionMemory
{
    private readonly Dictionary<string, Guid> _selected = [];

    public void Remember(string sectionKey, Guid? id)
    {
        lock (_selected)
        {
            if (id is { } value)
            {
                _selected[sectionKey] = value;
            }
            else
            {
                _selected.Remove(sectionKey);
            }
        }
    }

    public Guid? Recall(string sectionKey)
    {
        lock (_selected)
        {
            return _selected.TryGetValue(sectionKey, out var id) ? id : null;
        }
    }
}
