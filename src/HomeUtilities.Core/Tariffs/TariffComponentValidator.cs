using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tariffs;

/// <summary>Validation rules of <see cref="TariffComponent"/> including its rates and tiers.</summary>
public static class TariffComponentValidator
{
    /// <summary>Validates a tariff component.</summary>
    public static ValidationResult Validate(TariffComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);

        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(component.Name))
        {
            errors.Add(new(ErrorCodes.TariffNameRequired, nameof(TariffComponent.Name)));
        }
        else if (component.Name.Length > ValidationHelpers.MaxNameLength)
        {
            errors.Add(new(ErrorCodes.TariffNameTooLong, nameof(TariffComponent.Name)));
        }

        if (component.ContractId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.TariffContractRequired, nameof(TariffComponent.ContractId)));
        }

        if (component.VatPercentOverride is { } percent)
        {
            if (percent < 0 || percent > 100)
            {
                errors.Add(new(ErrorCodes.VatPercentOutOfRange, nameof(TariffComponent.VatPercentOverride)));
            }
        }
        else if (string.IsNullOrWhiteSpace(component.VatCode))
        {
            errors.Add(new(ErrorCodes.TariffVatRequired, nameof(TariffComponent.VatCode)));
        }

        ValidateScope(component, errors);

        var rates = component.Rates.Where(r => !r.IsDeleted).ToList();
        if (rates.Count == 0)
        {
            errors.Add(new(ErrorCodes.TariffRateRequired, nameof(TariffComponent.Rates)));
        }

        errors.AddRange(Versioned.Validate(component.Rates).Errors.Select(e => e with { Field = nameof(TariffComponent.Rates) }));

        for (var i = 0; i < component.Rates.Count; i++)
        {
            if (!component.Rates[i].IsDeleted)
            {
                ValidateRate(component.Rates[i], $"{nameof(TariffComponent.Rates)}[{i}]", errors);
            }
        }

        return ValidationHelpers.ToResult(errors);
    }

    private static void ValidateScope(TariffComponent component, List<ValidationError> errors)
    {
        var supportsSelection = component.Basis is ChargeBasis.PerUnit or ChargeBasis.NetExport;
        if (!supportsSelection && (component.RegisterKinds.Count > 0 || component.MeterIds.Count > 0))
        {
            errors.Add(new(ErrorCodes.TariffSelectionNotAllowed, nameof(TariffComponent.RegisterKinds)));
        }

        if (component.Basis == ChargeBasis.NetExport
            && component.RegisterKinds.Count > 0
            && !(component.RegisterKinds.Count == 1 && component.RegisterKinds[0] == RegisterKind.Export))
        {
            errors.Add(new(ErrorCodes.TariffNetExportRegister, nameof(TariffComponent.RegisterKinds)));
        }
    }

    private static void ValidateRate(TariffRate rate, string path, List<ValidationError> errors)
    {
        if (rate.Price < 0)
        {
            errors.Add(new(ErrorCodes.TariffPriceNegative, $"{path}.{nameof(TariffRate.Price)}"));
        }

        if (rate.Tiers.Count == 0)
        {
            return;
        }

        var tiersPath = $"{path}.{nameof(TariffRate.Tiers)}";
        if (rate.TierPeriod is null)
        {
            errors.Add(new(ErrorCodes.TariffTierPeriodRequired, $"{path}.{nameof(TariffRate.TierPeriod)}"));
        }

        if (rate.Tiers.Any(t => t.Price < 0))
        {
            errors.Add(new(ErrorCodes.TariffPriceNegative, tiersPath));
        }

        var last = rate.Tiers.Count - 1;
        if (rate.Tiers[last].UpToQuantity is not null)
        {
            errors.Add(new(ErrorCodes.TariffTierLastNotUnlimited, tiersPath));
        }

        decimal? previous = null;
        for (var i = 0; i < last; i++)
        {
            var upTo = rate.Tiers[i].UpToQuantity;
            if (upTo is null || upTo <= 0 || (previous is not null && upTo <= previous))
            {
                errors.Add(new(ErrorCodes.TariffTierOrderInvalid, tiersPath));
                break;
            }

            previous = upTo;
        }
    }
}
