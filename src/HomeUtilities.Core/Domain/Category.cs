using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>A payment category the user sees and reports group by, e.g. "Kindergarten" or "Insurance".</summary>
public class Category : SyncEntity
{
    /// <summary>Set for built-in categories (one per <see cref="ServiceKind"/>); null for user-created ones.</summary>
    public ServiceKind? SystemKey { get; set; }

    /// <summary>User-given name. Null on a built-in category means "use the localized name of <see cref="SystemKey"/>".</summary>
    public string? Name { get; set; }

    /// <summary>Colour as <c>#RRGGBB</c>, used in charts.</summary>
    public string Color { get; set; } = "#808080";

    /// <summary>Icon key; reserved for later.</summary>
    public string? Icon { get; set; }

    /// <summary>Position in lists.</summary>
    public int SortOrder { get; set; }

    /// <summary>Hidden from pickers, history is kept.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Free-form notes.</summary>
    public string? Notes { get; set; }

    /// <summary>True for a built-in category, which can be renamed and recoloured but not deleted.</summary>
    public bool IsBuiltIn => SystemKey is not null;
}
