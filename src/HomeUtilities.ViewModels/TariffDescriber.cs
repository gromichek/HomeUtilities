using System.Globalization;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>A worked example of a tariff component: what a typical quantity would cost with the newest price.</summary>
/// <param name="Quantity">The example quantity; null when no example can be given.</param>
/// <param name="Slices">The parts of the quantity with one unit price each (several for a stepped price).</param>
/// <param name="Net">Amount without VAT.</param>
/// <param name="VatPercent">VAT percent used.</param>
/// <param name="Vat">VAT amount.</param>
/// <param name="Gross">Amount with VAT.</param>
/// <param name="Text">The example as text (several lines for a stepped price); a hint when there is no example.</param>
public sealed record TariffExample(
    decimal? Quantity,
    IReadOnlyList<PriceSlice> Slices,
    decimal Net,
    decimal VatPercent,
    decimal Vat,
    decimal Gross,
    string Text)
{
    /// <summary>True when the example has amounts.</summary>
    public bool HasAmounts => Quantity is not null;
}

/// <summary>
/// Turns tariff components into the texts of the screens: units, price lines, the one-line summary of the contract card and
/// the live example of the component editor. Numbers follow the current culture.
/// </summary>
public sealed class TariffDescriber
{
    /// <summary>The quantity of the example of a metered component without tiers.</summary>
    public const decimal ExampleUnits = 10m;

    private readonly ILocalizer _localizer;
    private readonly DateOnly _today;
    private readonly Currency _currency;
    private readonly IReadOnlyCollection<VatRate> _catalog;
    private readonly Property? _property;
    private readonly IReadOnlyList<Meter> _meters;

    /// <param name="localizer">Localizer.</param>
    /// <param name="today">Date used for "current" prices, VAT and parameters.</param>
    /// <param name="currency">Currency of the contract.</param>
    /// <param name="catalog">The VAT catalog.</param>
    /// <param name="property">The property of the contract, for area and residents; null if unknown.</param>
    /// <param name="meters">The meters linked to the contract, for the unit words.</param>
    public TariffDescriber(
        ILocalizer localizer,
        DateOnly today,
        Currency currency,
        IReadOnlyCollection<VatRate> catalog,
        Property? property,
        IReadOnlyList<Meter> meters)
    {
        _localizer = localizer;
        _today = today;
        _currency = currency;
        _catalog = catalog;
        _property = property;
        _meters = meters;
    }

    /// <summary>The newest price of a component: the one valid today, otherwise the one with the latest valid-from date.</summary>
    public TariffRate? NewestRate(TariffComponent component)
    {
        var rates = component.Rates.Where(r => !r.IsDeleted).ToList();
        return component.RateAt(_today) ?? rates.OrderByDescending(r => r.ValidFrom).FirstOrDefault();
    }

    /// <summary>The unit word of the quantity: the meter unit, m², resident or month.</summary>
    public string UnitOf(TariffComponent component)
    {
        switch (component.Basis)
        {
            case ChargeBasis.PerArea:
                return _localizer["Unit_Area"];
            case ChargeBasis.PerResident:
                return _localizer["Unit_Resident"];
            case ChargeBasis.FixedMonthly:
                return _localizer["Unit_Month"];
            case ChargeBasis.FromBill:
                return string.Empty;
        }

        var selected = component.MeterIds.Count == 0 ? _meters : [.. _meters.Where(m => component.MeterIds.Contains(m.Id))];
        return selected.Count > 0 ? EnumText.Get(_localizer, selected[0].Unit) : _localizer["Unit_Units"];
    }

    /// <summary>The currency symbol, e.g. €.</summary>
    public string CurrencySymbol => _localizer["Currency_Symbol_" + _currency];

    /// <summary>A price with at least two and at most six decimals: 1,41 or 0,6711.</summary>
    public static string FormatPrice(decimal price) => price.ToString("0.00####", CultureInfo.CurrentCulture);

    /// <summary>An amount with two decimals.</summary>
    public static string FormatMoney(decimal amount) => amount.ToString("N2", CultureInfo.CurrentCulture);

    /// <summary>A quantity with up to three decimals.</summary>
    public static string FormatQuantity(decimal quantity) => DecimalInput.Format(quantity, CultureInfo.CurrentCulture, 3);

    /// <summary>"1,41 €/m³ excl. VAT", or "stepped price" for tiers.</summary>
    public string PriceText(TariffComponent component, TariffRate rate)
    {
        var vat = _localizer[rate.PriceIncludesVat ? "TariffVatIncluded" : "TariffVatExcluded"];
        if (rate.Tiers.Count > 0)
        {
            return _localizer.Get("TariffPriceStepped", rate.Tiers.Count, vat);
        }

        return _localizer.Get("TariffPriceLine", FormatPrice(rate.Price), CurrencySymbol, UnitOf(component), vat);
    }

    /// <summary>The current price with VAT and quantity for the row of the contract card.</summary>
    public string Summary(TariffComponent component)
    {
        if (component.Basis == ChargeBasis.FromBill)
        {
            return _localizer["TariffFromBill"];
        }

        if (NewestRate(component) is not { } rate)
        {
            return _localizer["TariffNoPrice"];
        }

        var price = PriceText(component, rate);
        if (component.Basis is (ChargeBasis.PerArea or ChargeBasis.PerResident) && QuantityParameter(component) is { } quantity)
        {
            price = _localizer.Get("TariffQuantityTimes", FormatQuantityWithUnit(component, quantity), price);
        }

        if (rate.PriceIncludesVat || ResolveVat(component) is not { } percent)
        {
            return price;
        }

        return _localizer.Get("TariffSummaryVat", price, FormatQuantity(percent));
    }

    /// <summary>The VAT percent valid today, or null if the code is unknown.</summary>
    public decimal? ResolveVat(TariffComponent component)
    {
        try
        {
            return TariffPricing.ResolveVatPercent(component, _catalog, _today);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>The current value of the property parameter the component uses; null if the property has none.</summary>
    public decimal? QuantityParameter(TariffComponent component)
    {
        if (_property is null)
        {
            return null;
        }

        var kind = component.QuantityParameter ?? DefaultParameter(component.Basis);
        if (kind != PropertyParameterKind.Custom)
        {
            return _property.GetParameter(kind, _today);
        }

        return string.IsNullOrWhiteSpace(component.QuantityParameterName)
            ? null
            : _property.GetParameter(component.QuantityParameterName.Trim(), _today);
    }

    /// <summary>The parameter a PerArea / PerResident component uses when none is chosen.</summary>
    public static PropertyParameterKind DefaultParameter(ChargeBasis basis) =>
        basis == ChargeBasis.PerResident ? PropertyParameterKind.DeclaredResidents : PropertyParameterKind.TotalArea;

    /// <summary>"now: 52,3 m²" for the parameter of the component, or a hint when the property has no value.</summary>
    public string ParameterNowText(TariffComponent component)
    {
        if (component.Basis is not (ChargeBasis.PerArea or ChargeBasis.PerResident))
        {
            return string.Empty;
        }

        return QuantityParameter(component) is { } value
            ? _localizer.Get("TariffParameterNow", FormatQuantityWithUnit(component, value))
            : _localizer["TariffParameterMissing"];
    }

    /// <summary>The worked example with the newest price.</summary>
    public TariffExample Example(TariffComponent component)
    {
        if (component.Basis == ChargeBasis.FromBill)
        {
            return NoExample("TariffExampleFromBill");
        }

        if (NewestRate(component) is not { } rate)
        {
            return NoExample("TariffExampleNoPrice");
        }

        if (ResolveVat(component) is not { } vatPercent)
        {
            return NoExample("TariffExampleNoVat");
        }

        decimal quantity;
        switch (component.Basis)
        {
            case ChargeBasis.PerArea:
            case ChargeBasis.PerResident:
                if (QuantityParameter(component) is not { } value)
                {
                    return NoExample("TariffExampleNoParameter");
                }

                quantity = MoneyMath.RoundQuantity(value);
                break;
            case ChargeBasis.FixedMonthly:
                quantity = 1m;
                break;
            case ChargeBasis.NetExport:
                quantity = -ExampleUnits;
                break;
            default:
                quantity = ExampleUnits;
                if (rate.Tiers.Count > 1 && rate.Tiers[^2].UpToQuantity is { } threshold)
                {
                    quantity = threshold * 1.5m;
                }

                break;
        }

        if (rate.Tiers.Count > 0 && quantity < 0m)
        {
            return NoExample("TariffExampleInvalid");
        }

        var slices = TariffPricing.Slice(rate, quantity);
        var amount = slices.Sum(s => s.Quantity * s.UnitPrice);
        var (net, vat, gross) = rate.PriceIncludesVat ? MoneyMath.FromGross(amount, vatPercent) : MoneyMath.FromNet(amount, vatPercent);

        var unit = UnitOf(component);
        var lines = new List<string>();
        if (slices.Count > 1)
        {
            lines.AddRange(slices.Select(s => _localizer.Get(
                "TariffExampleSlice", FormatQuantity(s.Quantity), unit, FormatPrice(s.UnitPrice), FormatMoney(MoneyMath.Round(s.Quantity * s.UnitPrice)))));
        }

        lines.Add(_localizer.Get(
            "TariffExampleTotal", FormatQuantity(quantity), unit, FormatMoney(net), FormatQuantity(vatPercent), FormatMoney(vat), FormatMoney(gross), CurrencySymbol));
        return new TariffExample(quantity, slices, net, vatPercent, vat, gross, string.Join("\n", lines));
    }

    private TariffExample NoExample(string key) => new(null, [], 0m, 0m, 0m, 0m, _localizer[key]);

    private string FormatQuantityWithUnit(TariffComponent component, decimal quantity)
    {
        var text = DecimalInput.Format(quantity, CultureInfo.CurrentCulture, 2);
        var kind = component.QuantityParameter ?? DefaultParameter(component.Basis);
        return kind is PropertyParameterKind.TotalArea or PropertyParameterKind.HeatedArea or PropertyParameterKind.LandArea
            ? $"{text} {_localizer["Unit_Area"]}"
            : text;
    }
}
