using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Charges;

/// <summary>
/// Calculates the charge the app expects for one contract and one billing month from tariffs, VAT,
/// meter consumption and property parameters. The result is later compared with the supplier's bill.
/// </summary>
/// <remarks>
/// Limitation (v1): a month is never split at a mid-month price change; one rate is chosen per component
/// (the one valid on the end reading date for metered bases, on the first day of the month otherwise).
/// </remarks>
public static class ChargeCalculator
{
    private sealed record Source(MeterChain Chain, RegisterKind Kind, ConsumptionResult Result);

    private sealed record Metered(IReadOnlyList<Source> Sources, decimal Quantity, bool IsComplete, DateOnly RateDate)
    {
        public IReadOnlyList<ConsumptionResult> Results => [.. Sources.Select(s => s.Result)];
    }

    /// <summary>Calculates the expected charge of the contract for the period of <paramref name="input"/>.</summary>
    public static CalculatedCharge Calculate(ChargeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var contract = input.Contract;
        var period = input.Period;
        var lines = new List<ChargeLine>();
        var warnings = new List<ChargeWarning>();

        if (!contract.IsActiveOn(period.FirstDay) && !contract.IsActiveOn(period.LastDay))
        {
            warnings.Add(new ChargeWarning(ChargeWarningCodes.ContractInactive));
            return Totals(input, lines, warnings);
        }

        var components = input.Components
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name, StringComparer.Ordinal);
        foreach (var component in components)
        {
            lines.AddRange(CalculateComponent(input, component, warnings));
        }

        return Totals(input, lines, warnings);
    }

    private static CalculatedCharge Totals(ChargeInput input, List<ChargeLine> lines, List<ChargeWarning> warnings)
    {
        var groups = VatGroups(lines, input.Contract.VatCalculation);
        return new CalculatedCharge(
            input.Contract.Id,
            input.Period,
            input.Contract.Currency,
            lines,
            groups.Sum(g => g.Net),
            groups.Sum(g => g.Vat),
            groups.Sum(g => g.Gross),
            lines.All(l => l.IsComplete),
            warnings,
            groups);
    }

    /// <summary>
    /// Totals per VAT percent. <see cref="VatCalculation.OnTotal"/>: VAT = Round(Σ rounded line net × percent); a group
    /// whose lines are all priced including VAT is split from its gross sum instead. <see cref="VatCalculation.PerLine"/>:
    /// the sums of the line values. Lines without any amount (from the bill, incomplete) do not form groups.
    /// </summary>
    private static List<VatGroup> VatGroups(IReadOnlyList<ChargeLine> lines, VatCalculation mode)
    {
        var groups = new List<VatGroup>();
        foreach (var group in lines.Where(l => l.Net != 0 || l.Vat != 0 || l.Gross != 0).GroupBy(l => l.VatPercent).OrderBy(g => g.Key))
        {
            if (mode == VatCalculation.PerLine)
            {
                groups.Add(new VatGroup(group.Key, group.Sum(l => l.Net), group.Sum(l => l.Vat), group.Sum(l => l.Gross)));
            }
            else if (group.All(l => l.PriceIncludesVat))
            {
                var (net, vat, gross) = MoneyMath.FromGross(group.Sum(l => l.Gross), group.Key);
                groups.Add(new VatGroup(group.Key, net, vat, gross));
            }
            else
            {
                var (net, vat, gross) = MoneyMath.FromNet(group.Sum(l => l.Net), group.Key);
                groups.Add(new VatGroup(group.Key, net, vat, gross));
            }
        }

        return groups;
    }

    private static List<ChargeLine> CalculateComponent(ChargeInput input, TariffComponent component, List<ChargeWarning> warnings)
    {
        var period = input.Period;
        if (component.Basis == ChargeBasis.FromBill)
        {
            var percent = TryResolveVat(component, input.VatCatalog, period.FirstDay) ?? 0m;
            return [Line(input, component, null, null, (0m, 0m, 0m), percent, fromBill: true, complete: true, [])];
        }

        var isMetered = component.Basis is ChargeBasis.PerUnit or ChargeBasis.NetExport;
        var metered = isMetered ? Meter(input, component, warnings) : null;
        var rateDate = metered?.RateDate ?? period.FirstDay;
        var consumption = metered?.Results ?? [];

        List<ChargeLine> Failed(string code, params object?[] args)
        {
            warnings.Add(new ChargeWarning(code, component.Id, args.Length == 0 ? null : args));
            return [Line(input, component, null, null, (0m, 0m, 0m), 0m, fromBill: false, complete: false, consumption)];
        }

        var rate = component.RateAt(rateDate);
        if (rate is null)
        {
            return Failed(ChargeWarningCodes.NoRateForDate, rateDate.ToString("yyyy-MM-dd"));
        }

        if (rate.Currency != input.Contract.Currency)
        {
            return Failed(ChargeWarningCodes.CurrencyMismatch, rate.Currency, input.Contract.Currency);
        }

        if (TryResolveVat(component, input.VatCatalog, rateDate) is not { } vatPercent)
        {
            return Failed(ChargeWarningCodes.UnknownVat, component.VatCode);
        }

        decimal quantity;
        decimal? billAmount = null;
        var complete = true;
        switch (component.Basis)
        {
            case ChargeBasis.PerUnit:
            case ChargeBasis.NetExport:
                if (component.Basis == ChargeBasis.NetExport && rate.Tiers.Count > 0)
                {
                    return Failed(ChargeWarningCodes.InvalidTariff);
                }

                quantity = component.Basis == ChargeBasis.NetExport ? -metered!.Quantity : metered!.Quantity;
                complete = metered.IsComplete;
                if (rate.Tiers.Count > 0)
                {
                    return TieredLines(input, component, rate, metered, vatPercent, warnings);
                }

                break;
            case ChargeBasis.PerArea:
            case ChargeBasis.PerResident:
                var (name, value) = ReadParameter(input, component);
                if (value is not { } parameter)
                {
                    return Failed(ChargeWarningCodes.MissingParameter, name);
                }

                quantity = MoneyMath.RoundQuantity(parameter);
                break;
            case ChargeBasis.FixedMonthly:
                quantity = 1m;
                break;
            case ChargeBasis.QuantityFromBill:
                if (input.QuantitiesFromBill is null || !input.QuantitiesFromBill.TryGetValue(component.Id, out var typed))
                {
                    return Failed(ChargeWarningCodes.QuantityFromBill);
                }

                if (rate.Tiers.Count > 0)
                {
                    return Failed(ChargeWarningCodes.InvalidTariff);
                }

                quantity = MoneyMath.RoundQuantity(typed);
                if (input.AmountsFromBill is not null && input.AmountsFromBill.TryGetValue(component.Id, out var typedAmount))
                {
                    // The supplier prints the quantity rounded but bills the unrounded one: the printed amount wins.
                    billAmount = typedAmount;
                    if (!AmountMatches(typedAmount, quantity, rate.Price))
                    {
                        warnings.Add(new ChargeWarning(ChargeWarningCodes.AmountMismatch, component.Id));
                    }
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(component), component.Basis, "Unsupported charge basis.");
        }

        return [Priced(input, component, rate, quantity, rate.Price, vatPercent, complete, consumption, billAmount)];
    }

    /// <summary>
    /// True if the amount typed from the bill is what quantity × price gives for any unrounded quantity within half a unit
    /// of the last printed decimal (±0.0005), widened by one cent for rounding of the amount.
    /// </summary>
    private static bool AmountMatches(decimal amount, decimal quantity, decimal price)
    {
        const decimal HalfStep = 0.0005m;
        const decimal Cent = 0.01m;
        var a = (quantity - HalfStep) * price;
        var b = (quantity + HalfStep) * price;
        return amount >= Math.Min(a, b) - Cent && amount <= Math.Max(a, b) + Cent;
    }

    private static ChargeLine Priced(
        ChargeInput input,
        TariffComponent component,
        TariffRate rate,
        decimal quantity,
        decimal unitPrice,
        decimal vatPercent,
        bool complete,
        IReadOnlyList<ConsumptionResult> consumption,
        decimal? billAmount = null)
    {
        var amount = billAmount ?? quantity * unitPrice;
        var split = rate.PriceIncludesVat ? MoneyMath.FromGross(amount, vatPercent) : MoneyMath.FromNet(amount, vatPercent);
        return Line(input, component, quantity, unitPrice, split, vatPercent, fromBill: false, complete, consumption, rate.PriceIncludesVat, billAmount is not null);
    }

    private static ChargeLine Line(
        ChargeInput input,
        TariffComponent component,
        decimal? quantity,
        decimal? unitPrice,
        (decimal Net, decimal Vat, decimal Gross) amounts,
        decimal vatPercent,
        bool fromBill,
        bool complete,
        IReadOnlyList<ConsumptionResult> consumption,
        bool priceIncludesVat = false,
        bool amountFromBill = false) =>
        new(
            component.Id,
            component.Name,
            component.ChargeKind,
            component.CategoryId ?? input.Contract.CategoryId,
            component.Basis,
            quantity,
            unitPrice,
            amounts.Net,
            vatPercent,
            amounts.Vat,
            amounts.Gross,
            fromBill,
            complete,
            consumption,
            priceIncludesVat,
            amountFromBill);

    private static decimal? TryResolveVat(TariffComponent component, IReadOnlyCollection<VatRate> catalog, DateOnly date)
    {
        try
        {
            return TariffPricing.ResolveVatPercent(component, catalog, date);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static (string Name, decimal? Value) ReadParameter(ChargeInput input, TariffComponent component)
    {
        var kind = component.QuantityParameter
            ?? (component.Basis == ChargeBasis.PerArea ? PropertyParameterKind.TotalArea : PropertyParameterKind.DeclaredResidents);
        var date = input.Period.FirstDay;
        if (kind != PropertyParameterKind.Custom)
        {
            return (kind.ToString(), input.Property.GetParameter(kind, date));
        }

        var name = component.QuantityParameterName ?? string.Empty;
        return (name, string.IsNullOrWhiteSpace(name) ? null : input.Property.GetParameter(name, date));
    }

    private static Metered Meter(ChargeInput input, TariffComponent component, List<ChargeWarning> warnings)
    {
        var period = input.Period;
        var window = input.Contract is { ReadingDayFrom: { } from, ReadingDayTo: { } to } ? (from, to) : ((int, int)?)null;
        var sources = new List<Source>();
        foreach (var (chain, kind) in ChainsAndKinds(input, component))
        {
            sources.Add(new Source(chain, kind, ConsumptionCalculator.CalculateForPeriod(chain, kind, period, window)));
        }

        var complete = sources.All(s => s.Result.IsComplete);
        if (sources.Count == 0)
        {
            complete = false;
            warnings.Add(new ChargeWarning(ChargeWarningCodes.NoMeters, component.Id));
        }
        else if (!complete)
        {
            warnings.Add(new ChargeWarning(ChargeWarningCodes.ConsumptionIncomplete, component.Id));
        }

        var quantity = MoneyMath.RoundQuantity(sources.Sum(s => s.Result.Quantity));
        var endDates = sources.Where(s => s.Result.EndReading is not null).Select(s => s.Result.EndReading!.Date).ToList();
        var rateDate = endDates.Count == 0 ? period.LastDay : endDates.Max();
        return new Metered(sources, quantity, complete, rateDate);
    }

    private static IEnumerable<(MeterChain Chain, RegisterKind Kind)> ChainsAndKinds(ChargeInput input, TariffComponent component)
    {
        var linked = input.ContractMeters
            .Where(cm => cm.IsActiveOn(input.Period.LastDay))
            .Select(cm => cm.MeterId)
            .Where(id => input.PropertyMeters.Any(m => !m.IsDeleted && m.Id == id));

        var seen = new HashSet<Guid>();
        foreach (var meterId in linked)
        {
            var chain = MeterChain.Build(input.PropertyMeters, meterId);

            // MeterIds identifies a meter position: any meter of the replacement chain may be the selected one.
            if (component.MeterIds.Count > 0 && !chain.Meters.Any(m => component.MeterIds.Contains(m.Id)))
            {
                continue;
            }

            if (!seen.Add(chain.Meters[0].Id))
            {
                continue;
            }

            foreach (var kind in KindsOf(chain, component))
            {
                yield return (chain, kind);
            }
        }
    }

    private static IEnumerable<RegisterKind> KindsOf(MeterChain chain, TariffComponent component)
    {
        var registers = chain.Meters.SelectMany(m => m.Registers).Where(r => !r.IsDeleted).ToList();
        if (component.Basis == ChargeBasis.NetExport)
        {
            return registers.Any(r => r.Kind == RegisterKind.Export) ? [RegisterKind.Export] : [];
        }

        var wanted = component.RegisterKinds.Count > 0
            ? registers.Where(r => component.RegisterKinds.Contains(r.Kind))
            : registers.Where(r => r.Direction == EnergyDirection.Import);
        return wanted.Select(r => r.Kind).Distinct().Order();
    }

    private static List<ChargeLine> TieredLines(ChargeInput input, TariffComponent component, TariffRate rate, Metered metered, decimal vatPercent, List<ChargeWarning> warnings)
    {
        var alreadyUsed = 0m;
        var tierStart = rate.TierPeriod == TierPeriod.Year ? new DateOnly(input.Period.Year, 1, 1) : input.Period.FirstDay;
        foreach (var source in metered.Sources)
        {
            if (source.Result.StartReading is not { } start || start.Date < tierStart)
            {
                continue;
            }

            var used = ConsumptionCalculator.Calculate(source.Chain, source.Kind, tierStart, start.Date);
            if (used.IsComplete)
            {
                alreadyUsed += used.Quantity;
            }
            else
            {
                warnings.Add(new ChargeWarning(ChargeWarningCodes.TierUsageUnknown, component.Id));
            }
        }

        var slices = TariffPricing.Slice(rate, metered.Quantity, alreadyUsed);
        if (slices.Count == 0)
        {
            slices = [new PriceSlice(0m, rate.Tiers[0].Price)];
        }

        return [.. slices.Select(s => Priced(input, component, rate, MoneyMath.RoundQuantity(s.Quantity), s.UnitPrice, vatPercent, metered.IsComplete, metered.Results))];
    }
}
