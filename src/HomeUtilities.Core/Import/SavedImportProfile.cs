using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Import;

/// <summary>A named, reusable <see cref="ImportProfile"/>.</summary>
public class SavedImportProfile : SyncEntity
{
    /// <summary>Display name, e.g. "Water sheet".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The mapping (stored as a JSON column).</summary>
    public ImportProfile Profile { get; set; } = new();
}
