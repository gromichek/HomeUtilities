using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>One counting register of a meter (total, day/night tariff, export).</summary>
public class MeterRegister : SyncEntity
{
    /// <summary>Meter the register belongs to.</summary>
    public Guid MeterId { get; set; }

    /// <summary>Kind of the register.</summary>
    public RegisterKind Kind { get; set; }

    /// <summary>Direction of the measured energy flow.</summary>
    public EnergyDirection Direction { get; set; }

    /// <summary>Optional display name override.</summary>
    public string? Name { get; set; }

    /// <summary>Readings taken from this register.</summary>
    public List<MeterReading> Readings { get; set; } = [];

    /// <summary>Creates the default <see cref="RegisterKind.Total"/> / <see cref="EnergyDirection.Import"/> register.</summary>
    public static MeterRegister CreateDefault(Guid meterId) =>
        new() { MeterId = meterId, Kind = RegisterKind.Total, Direction = EnergyDirection.Import };
}
