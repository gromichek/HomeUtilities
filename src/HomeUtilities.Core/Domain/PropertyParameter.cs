using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>A time-versioned parameter of a property (area, residents, cadastral value, ...).</summary>
public class PropertyParameter : SyncEntity, IVersioned
{
    /// <summary>Owning property.</summary>
    public Guid PropertyId { get; set; }

    /// <summary>Kind of the parameter.</summary>
    public PropertyParameterKind Kind { get; set; }

    /// <summary>Name of a <see cref="PropertyParameterKind.Custom"/> parameter; required only for it.</summary>
    public string? Name { get; set; }

    /// <summary>First date on which <see cref="Value"/> applies.</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>Value of the parameter.</summary>
    public decimal Value { get; set; }

    /// <summary>Free-form note.</summary>
    public string? Note { get; set; }
}
