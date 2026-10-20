using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Billing;

/// <summary>A named field defined once on a contract (or bill template) that reappears on every bill.</summary>
public class CustomFieldDefinition : SyncEntity
{
    /// <summary>The contract or bill template that owns the definition.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Field name, e.g. "Losses m³".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type of the value.</summary>
    public CustomFieldKind Kind { get; set; }

    /// <summary>Whether the field is entered per bill or per bill line.</summary>
    public CustomFieldTarget Target { get; set; }

    /// <summary>Display order.</summary>
    public int SortOrder { get; set; }
}
