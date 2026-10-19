namespace HomeUtilities.Core.Tariffs;

/// <summary>
/// Seed data of the VAT catalog with the Latvian rate history. These are editable defaults that the
/// owner is expected to verify; they are not authoritative.
/// </summary>
public static class LatvianVatDefaults
{
    /// <summary>Creates new instances of the default VAT rates on every call.</summary>
    public static IReadOnlyList<VatRate> Create() =>
    [
        Rate("LV-STANDARD", "Standard rate", (new(1995, 1, 1), 18m), (new(2009, 1, 1), 21m), (new(2011, 1, 1), 22m), (new(2012, 7, 1), 21m)),
        Rate("LV-REDUCED", "Reduced rate", (new(2004, 5, 1), 5m), (new(2009, 1, 1), 10m), (new(2011, 1, 1), 12m)),
        Rate("ZERO", "Zero / exempt", (new(1995, 1, 1), 0m)),
    ];

    private static VatRate Rate(string code, string name, params (DateOnly ValidFrom, decimal Percent)[] versions)
    {
        var rate = new VatRate { Code = code, Name = name };
        foreach (var (validFrom, percent) in versions)
        {
            rate.Versions.Add(new VatRateVersion { VatRateId = rate.Id, ValidFrom = validFrom, Percent = percent });
        }

        return rate;
    }
}
