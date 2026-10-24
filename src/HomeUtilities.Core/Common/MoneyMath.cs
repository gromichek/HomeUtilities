namespace HomeUtilities.Core.Common;

/// <summary>Rounding and VAT helpers. All rounding is away from zero so negative values are symmetric.</summary>
public static class MoneyMath
{
    /// <summary>Rounds a money value to 2 decimals, midpoint away from zero.</summary>
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>Rounds a quantity (m³, kWh, …) to the given decimals, midpoint away from zero.</summary>
    public static decimal RoundQuantity(decimal value, int decimals = 3) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Calculates VAT and gross from a net amount, like a supplier invoice does: the net is rounded to 0.01 first,
    /// then VAT = Round(rounded net × VAT %), gross = rounded net + VAT.
    /// </summary>
    /// <param name="net">Net amount.</param>
    /// <param name="vatPercent">VAT rate in percent, e.g. 21.</param>
    public static (decimal Net, decimal Vat, decimal Gross) FromNet(decimal net, decimal vatPercent)
    {
        var roundedNet = Round(net);
        var vat = Round(roundedNet * vatPercent / 100);
        return (roundedNet, vat, roundedNet + vat);
    }

    /// <summary>Splits a gross amount into net and VAT so that Net + Vat equals Gross exactly.</summary>
    /// <param name="gross">Gross amount.</param>
    /// <param name="vatPercent">VAT rate in percent, e.g. 21.</param>
    public static (decimal Net, decimal Vat, decimal Gross) FromGross(decimal gross, decimal vatPercent)
    {
        var roundedGross = Round(gross);
        var net = Round(gross / (1 + vatPercent / 100));
        return (net, roundedGross - net, roundedGross);
    }
}
