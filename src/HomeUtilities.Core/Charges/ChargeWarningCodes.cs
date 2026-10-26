namespace HomeUtilities.Core.Charges;

/// <summary>Codes of <see cref="ChargeWarning"/>. The UI maps them to localized messages.</summary>
public static class ChargeWarningCodes
{
    /// <summary>The contract is not active in the period.</summary>
    public const string ContractInactive = "charge.contract_inactive";

    /// <summary>The component has no rate on the rate date.</summary>
    public const string NoRateForDate = "charge.no_rate_for_date";

    /// <summary>The currency of the rate differs from the currency of the contract.</summary>
    public const string CurrencyMismatch = "charge.currency_mismatch";

    /// <summary>The VAT code is unknown or has no rate on the rate date.</summary>
    public const string UnknownVat = "charge.unknown_vat";

    /// <summary>Consumption of at least one meter could not be determined.</summary>
    public const string ConsumptionIncomplete = "charge.consumption_incomplete";

    /// <summary>No active meter of the contract provides a register for the component.</summary>
    public const string NoMeters = "charge.no_meters";

    /// <summary>The consumption earlier in the tier period could not be determined; zero was assumed.</summary>
    public const string TierUsageUnknown = "charge.tier_usage_unknown";

    /// <summary>The rate cannot be applied to the component (tiers on a net export component).</summary>
    public const string InvalidTariff = "charge.invalid_tariff";

    /// <summary>The quantity of a QuantityFromBill component has not been entered from the bill.</summary>
    public const string QuantityFromBill = "charge.quantity_from_bill";

    /// <summary>The line amount typed from the bill does not match quantity × price (the price may have changed).</summary>
    public const string AmountMismatch = "charge.amount_mismatch";

    /// <summary>The property parameter that gives the quantity has no value on the rate date.</summary>
    public const string MissingParameter = "charge.missing_parameter";
}
