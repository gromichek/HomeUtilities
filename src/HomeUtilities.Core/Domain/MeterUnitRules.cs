namespace HomeUtilities.Core.Domain;

/// <summary>Which units a resource may be measured in (DATA_MODEL §4).</summary>
public static class MeterUnitRules
{
    /// <summary>The units valid for a resource; the first one is the default.</summary>
    public static IReadOnlyList<MeterUnit> UnitsFor(MeterResource resource) => resource switch
    {
        MeterResource.ColdWater or MeterResource.HotWater or MeterResource.Gas => [MeterUnit.M3],
        MeterResource.Electricity => [MeterUnit.KWh],
        MeterResource.Heat => [MeterUnit.MWh, MeterUnit.Gcal, MeterUnit.KWh],
        _ => [MeterUnit.M3, MeterUnit.KWh, MeterUnit.MWh, MeterUnit.Gcal],
    };

    /// <summary>The unit a new meter of the resource starts with.</summary>
    public static MeterUnit DefaultUnit(MeterResource resource) => UnitsFor(resource)[0];

    /// <summary>True if the unit is valid for the resource.</summary>
    public static bool IsCompatible(MeterResource resource, MeterUnit unit) => UnitsFor(resource).Contains(unit);
}
