using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The screens a charge warning can lead to; each opens a page, so Back returns to the charges.</summary>
/// <param name="OpenComponent">The tariff component editor: contract id, component id.</param>
/// <param name="OpenProperty">The property editor: property id.</param>
/// <param name="OpenReadings">The readings screen: property id.</param>
/// <param name="OpenContract">The contract editor: property id, contract id.</param>
public sealed record ChargeNavigation(
    Func<Guid, Guid, Task> OpenComponent,
    Func<Guid, Task> OpenProperty,
    Func<Guid, Task> OpenReadings,
    Func<Guid, Guid, Task> OpenContract);

/// <summary>
/// Maps a <see cref="CalculatedCharge"/> to the card of the charges screen: line texts, the readings behind each quantity and
/// the localized warnings with their links. No calculation happens here; every number comes from <see cref="ChargeCalculator"/>.
/// </summary>
public sealed class ChargeCardBuilder
{
    private readonly ILocalizer _localizer;
    private readonly ChargeNavigation _navigation;
    private readonly DateOnly _today;

    public ChargeCardBuilder(ILocalizer localizer, ChargeNavigation navigation, DateOnly today)
    {
        _localizer = localizer;
        _navigation = navigation;
        _today = today;
    }

    /// <summary>A card for a contract whose calculation threw; the screen stays usable.</summary>
    public ChargeCardViewModel BuildError(Contract contract, string supplierName, string color) =>
        new(contract.Id, contract.Title, supplierName, color, ChargeCardStatus.Error, _localizer["ChargeStatus_Error"], string.Empty, string.Empty, string.Empty)
        {
            ErrorText = _localizer["ChargeCardError"],
        };

    /// <summary>The card of the contract for the period of <paramref name="input"/>.</summary>
    public ChargeCardViewModel Build(ChargeInput input, CalculatedCharge charge, string supplierName, string color)
    {
        var contract = input.Contract;
        var linked = input.ContractMeters.Select(cm => cm.MeterId).ToHashSet();
        var describer = new TariffDescriber(
            _localizer, _today, contract.Currency, input.VatCatalog, input.Property, [.. input.PropertyMeters.Where(m => linked.Contains(m.Id))]);
        var symbol = describer.CurrencySymbol;
        var registers = RegisterLookup(input.PropertyMeters);

        var hasComponents = input.Components.Any(c => !c.IsDeleted);
        var status =
            !hasComponents ? ChargeCardStatus.NoTariff
            : charge.Lines.Count > 0 && charge.Lines.All(l => l.IsFromBill) ? ChargeCardStatus.FromBill
            : charge.IsComplete ? ChargeCardStatus.Complete
            : ChargeCardStatus.Incomplete;

        var hasAmounts = status is ChargeCardStatus.Complete or ChargeCardStatus.Incomplete;
        var lines = new List<ChargeLineViewModel>();
        var detailShown = new HashSet<Guid>();
        foreach (var line in charge.Lines)
        {
            var details = detailShown.Add(line.ComponentId) ? Details(line, registers) : [];
            lines.Add(Line(line, input.Components.FirstOrDefault(c => c.Id == line.ComponentId), describer, symbol, details, charge.Period));
        }

        var card = new ChargeCardViewModel(
            contract.Id,
            contract.Title,
            supplierName,
            color,
            status,
            _localizer["ChargeStatus_" + status],
            hasAmounts ? Money(charge.TotalGross, symbol) : string.Empty,
            Money(charge.TotalNet, symbol),
            Money(charge.TotalVat, symbol))
        {
            Lines = lines,
            VatGroupTexts = hasAmounts && contract.VatCalculation == VatCalculation.OnTotal
                ? [.. (charge.VatGroups ?? []).Select(g => _localizer.Get(
                    "ChargeVatGroup", TariffDescriber.FormatQuantity(g.VatPercent), Money(g.Net, symbol), Money(g.Vat, symbol)))]
                : [],
            Warnings = Warnings(input, charge, registers),
            NoTariffText = _localizer["ChargeNoTariff"],
            NoTariffLinkText = _localizer["ChargeOpenContract"],
            NoTariffCommand = new AsyncRelayCommand(() => _navigation.OpenContract(contract.PropertyId, contract.Id)),
            NetLabel = _localizer["ChargeNet"],
            VatLabel = _localizer["ChargeVat"],
            TotalLabel = _localizer["ChargeTotal"],
        };
        return card;
    }

    private static string Money(decimal amount, string symbol) => $"{TariffDescriber.FormatMoney(amount)} {symbol}";

    private ChargeLineViewModel Line(
        ChargeLine line, TariffComponent? component, TariffDescriber describer, string symbol, IReadOnlyList<ChargeReadingDetailViewModel> details, YearMonth period)
    {
        var quantity = line.Quantity is { } q ? QuantityText(line.Basis, q, component, describer) : string.Empty;
        if (line.Basis == ChargeBasis.QuantityFromBill)
        {
            // The quantity is never measured here: say where it comes from (or that it is still missing).
            quantity = line.Quantity is null
                ? _localizer["ChargeQuantityFromBill"]
                : _localizer.Get(line.AmountFromBill ? "ChargeQuantityAmountFromBillOf" : "ChargeQuantityFromBillOf", quantity, BillText.Period(period));
        }

        var unitPrice = line.UnitPrice is { } price && !line.IsFromBill
            ? _localizer.Get("ChargeUnitPrice", TariffDescriber.FormatPrice(price), symbol, _localizer[line.PriceIncludesVat ? "TariffVatIncluded" : "TariffVatExcluded"])
            : string.Empty;
        var hasAmounts = !line.IsFromBill && line.IsComplete;
        return new ChargeLineViewModel(
            line.ComponentName,
            quantity,
            unitPrice,
            hasAmounts ? Money(line.Net, symbol) : string.Empty,
            hasAmounts ? _localizer.Get("ChargePercent", TariffDescriber.FormatQuantity(line.VatPercent)) : string.Empty,
            hasAmounts ? Money(line.Vat, symbol) : string.Empty,
            hasAmounts ? Money(line.Gross, symbol) : string.Empty,
            line.IsFromBill,
            _localizer["ChargeFromBill"],
            !line.IsFromBill && !line.IsComplete,
            details);
    }

    private string QuantityText(ChargeBasis basis, decimal quantity, TariffComponent? component, TariffDescriber describer)
    {
        var culture = CultureInfo.CurrentCulture;
        switch (basis)
        {
            case ChargeBasis.PerArea:
                return $"{quantity.ToString("N2", culture)} {_localizer["Unit_Area"]}";
            case ChargeBasis.PerResident:
                return $"{quantity.ToString("0.##", culture)} {_localizer[quantity == 1m ? "ChargeUnitResident_One" : "ChargeUnitResident_Many"]}";
            case ChargeBasis.FixedMonthly:
                return $"{quantity.ToString("0.##", culture)} {_localizer["Unit_Month"]}";
            default:
                // A metered line: the unit word of the component's meters.
                var unit = describer.UnitOf(component ?? new TariffComponent { Basis = basis });
                return $"{quantity.ToString("N3", culture)} {unit}";
        }
    }

    private static Dictionary<Guid, (Meter Meter, MeterRegister Register)> RegisterLookup(IEnumerable<Meter> meters) =>
        meters.SelectMany(m => m.Registers.Select(r => (Meter: m, Register: r))).ToDictionary(x => x.Register.Id);

    private string MeterTitle(IReadOnlyList<Meter> meters, Guid meterId) =>
        meters.FirstOrDefault(m => m.Id == meterId) is { } meter ? MeterNames.Title(_localizer, meter) : string.Empty;

    private IReadOnlyList<ChargeReadingDetailViewModel> Details(ChargeLine line, Dictionary<Guid, (Meter Meter, MeterRegister Register)> registers) =>
        [.. line.Consumption.Select(r => Detail(r, registers)).OfType<ChargeReadingDetailViewModel>()];

    private ChargeReadingDetailViewModel? Detail(ConsumptionResult result, Dictionary<Guid, (Meter Meter, MeterRegister Register)> registers)
    {
        var meters = registers.Values.Select(v => v.Meter).DistinctBy(m => m.Id).ToList();
        var anchor = AnchorOf(result, registers);
        if (anchor is null && result.Segments.Count == 0 && result.StartReading is null && result.EndReading is null)
        {
            return null;
        }

        var culture = CultureInfo.CurrentCulture;
        var title = anchor is { } a ? MeterNames.Title(_localizer, a.Meter) : string.Empty;
        var registerLabel = anchor is { } b ? RegisterLabel.For(_localizer, b.Meter, b.Register) : string.Empty;
        var header = registerLabel.Length > 0 ? $"{title} · {registerLabel}" : title;
        var unit = anchor is { } c ? EnumText.Get(_localizer, c.Meter.Unit) : string.Empty;

        var lines = new List<string>();
        var notes = new List<string>();
        lines.Add(result.StartReading is { } start
            ? _localizer.Get("ChargeReadingStart", start.Value.ToString("N3", culture), start.Date)
            : _localizer["ChargeReadingStartMissing"]);

        if (result.Segments.Count > 1)
        {
            for (var i = 0; i < result.Segments.Count; i++)
            {
                var segment = result.Segments[i];
                var segmentMeter = MeterTitle(meters, segment.MeterId);
                lines.Add(_localizer.Get(
                    i == 0 ? "ChargeSegmentOld" : "ChargeSegmentNew",
                    segmentMeter,
                    segment.StartValue.ToString("N3", culture),
                    segment.EndValue.ToString("N3", culture),
                    segment.Quantity.ToString("N3", culture)));
            }
        }

        lines.Add(result.EndReading is { } end
            ? _localizer.Get("ChargeReadingEnd", end.Value.ToString("N3", culture), end.Date)
            : _localizer["ChargeReadingEndMissing"]);
        lines.Add(_localizer.Get("ChargeReadingConsumption", result.Quantity.ToString("N3", culture), unit));

        foreach (var segment in result.Segments.Where(s => s.Rollover))
        {
            notes.Add(_localizer.Get("ChargeNoteRollover", MeterTitle(meters, segment.MeterId), segment.StartValue.ToString("N3", culture), segment.EndValue.ToString("N3", culture)));
        }

        foreach (var warning in result.Warnings)
        {
            // A rollover is already flagged per segment, with the values.
            if (warning.Code == ConsumptionWarningCodes.RolloverAssumed && result.Segments.Any(s => s.Rollover))
            {
                continue;
            }

            notes.Add(_localizer.TryGet("ChargeConsumption_" + warning.Code[(warning.Code.IndexOf('.') + 1)..], out var text) ? text : warning.Code);
        }

        return new ChargeReadingDetailViewModel(header, lines, notes);
    }

    private static (Meter Meter, MeterRegister Register)? AnchorOf(
        ConsumptionResult result, Dictionary<Guid, (Meter Meter, MeterRegister Register)> registers)
    {
        foreach (var registerId in new[] { result.EndReading?.RegisterId, result.StartReading?.RegisterId })
        {
            if (registerId is { } id && registers.TryGetValue(id, out var found))
            {
                return found;
            }
        }

        return null;
    }

    private IReadOnlyList<ChargeWarningViewModel> Warnings(ChargeInput input, CalculatedCharge charge, Dictionary<Guid, (Meter Meter, MeterRegister Register)> registers)
    {
        var contract = input.Contract;
        var result = new List<ChargeWarningViewModel>();
        var seen = new HashSet<string>();
        foreach (var warning in charge.Warnings)
        {
            var component = warning.ComponentId is { } id ? input.Components.FirstOrDefault(c => c.Id == id) : null;
            var name = component?.Name ?? string.Empty;
            var args = warning.Args ?? [];
            var arg0 = args.Length > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty;
            var arg1 = args.Length > 1 ? args[1]?.ToString() ?? string.Empty : string.Empty;

            string message;
            string actionText = string.Empty;
            Func<Task>? action = null;
            switch (warning.Code)
            {
                case ChargeWarningCodes.NoRateForDate:
                    message = _localizer.Get("ChargeWarn_NoRate", DateText(arg0), name);
                    actionText = _localizer["ChargeOpenTariff"];
                    action = () => _navigation.OpenComponent(contract.Id, component?.Id ?? Guid.Empty);
                    break;
                case ChargeWarningCodes.MissingParameter:
                    message = _localizer.Get("ChargeWarn_MissingParameter", ParameterName(arg0), charge.Period.FirstDay);
                    actionText = _localizer["ChargeOpenProperty"];
                    action = () => _navigation.OpenProperty(input.Property.Id);
                    break;
                case ChargeWarningCodes.ConsumptionIncomplete:
                    message = _localizer.Get("ChargeWarn_ReadingsMissing", MissingMeters(charge, warning.ComponentId, input.PropertyMeters, registers, name));
                    actionText = _localizer["ChargeOpenReadings"];
                    action = () => _navigation.OpenReadings(input.Property.Id);
                    break;
                case ChargeWarningCodes.NoMeters:
                    message = _localizer.Get("ChargeWarn_NoMeters", name);
                    actionText = _localizer["ChargeOpenContract"];
                    action = () => _navigation.OpenContract(contract.PropertyId, contract.Id);
                    break;
                case ChargeWarningCodes.UnknownVat:
                    message = _localizer.Get("ChargeWarn_UnknownVat", arg0, name);
                    actionText = _localizer["ChargeOpenTariff"];
                    action = () => _navigation.OpenComponent(contract.Id, component?.Id ?? Guid.Empty);
                    break;
                case ChargeWarningCodes.CurrencyMismatch:
                    message = _localizer.Get("ChargeWarn_CurrencyMismatch", name, arg0, arg1);
                    actionText = _localizer["ChargeOpenTariff"];
                    action = () => _navigation.OpenComponent(contract.Id, component?.Id ?? Guid.Empty);
                    break;
                case ChargeWarningCodes.TierUsageUnknown:
                    message = _localizer.Get("ChargeWarn_TierUsageUnknown", name);
                    break;
                case ChargeWarningCodes.InvalidTariff:
                    message = _localizer.Get("ChargeWarn_InvalidTariff", name);
                    actionText = _localizer["ChargeOpenTariff"];
                    action = () => _navigation.OpenComponent(contract.Id, component?.Id ?? Guid.Empty);
                    break;
                case ChargeWarningCodes.QuantityFromBill:
                    message = _localizer.Get("ChargeWarn_QuantityFromBill", name);
                    break;
                case ChargeWarningCodes.AmountMismatch:
                    // No link: the bill is not part of the charge input, and the bill editor shows the same warning under the row.
                    message = _localizer.Get("ChargeWarn_AmountMismatch", name);
                    break;
                case ChargeWarningCodes.ContractInactive:
                    message = _localizer["ChargeWarn_ContractInactive"];
                    break;
                default:
                    message = warning.Code;
                    break;
            }

            if (seen.Add($"{warning.Code}|{message}"))
            {
                result.Add(new ChargeWarningViewModel(message, actionText, action is null ? null : new AsyncRelayCommand(action)));
            }
        }

        return result;
    }

    private string DateText(string iso) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("d", CultureInfo.CurrentCulture)
            : iso;

    private string ParameterName(string name) =>
        Enum.TryParse<PropertyParameterKind>(name, out var kind) && kind != PropertyParameterKind.Custom && Enum.IsDefined(kind)
            ? EnumText.Get(_localizer, kind)
            : name;

    /// <summary>The meters whose consumption of the component could not be determined; the component name when none can be told.</summary>
    private string MissingMeters(
        CalculatedCharge charge,
        Guid? componentId,
        IReadOnlyList<Meter> meters,
        Dictionary<Guid, (Meter Meter, MeterRegister Register)> registers,
        string componentName)
    {
        var titles = charge.Lines
            .Where(l => l.ComponentId == componentId)
            .SelectMany(l => l.Consumption)
            .Where(r => !r.IsComplete)
            .Select(r => AnchorOf(r, registers) is { } anchor
                ? MeterNames.Title(_localizer, anchor.Meter)
                : r.Segments.Count > 0 ? MeterTitle(meters, r.Segments[0].MeterId) : string.Empty)
            .Where(t => t.Length > 0)
            .Distinct()
            .ToList();
        return titles.Count > 0 ? string.Join(", ", titles) : componentName;
    }
}
