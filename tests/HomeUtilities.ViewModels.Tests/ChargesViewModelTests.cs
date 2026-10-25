using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// The charges screen over the real <c>ChargeCalculator</c>: the numbers on the cards are the calculator's, so every
/// test writes the expected arithmetic out, e.g. <c>5.500 m³ × 1.41 = 7.76 net; VAT 21 % = 1.63; total 9.39</c>.
/// </summary>
public class ChargesViewModelTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 27);
    private static readonly DateOnly Long = new(2020, 1, 1);

    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly ContractTestKit _kit = new(Today);

    public ChargesViewModelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
    }

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private Contract AddContract(string title, int? from = 25, int? to = 28, DateOnly? validFrom = null, DateOnly? validTo = null, Property? property = null)
    {
        var supplier = _kit.Suppliers.Stored.FirstOrDefault() ?? new Supplier { Name = "Water utility" };
        if (_kit.Suppliers.Stored.All(s => s.Id != supplier.Id))
        {
            _kit.Suppliers.Stored.Add(supplier);
        }

        var contract = new Contract
        {
            PropertyId = (property ?? _kit.Property).Id,
            SupplierId = supplier.Id,
            Title = title,
            Service = ServiceKind.ColdWater,
            CategoryId = _kit.CategoryIdFor(ServiceKind.ColdWater),
            ValidFrom = validFrom ?? Long,
            ValidTo = validTo,
            ReadingDayFrom = from,
            ReadingDayTo = to,
        };
        _kit.Contracts.Stored.Add(contract);
        return contract;
    }

    private Meter AddMeter(Contract? contract, string location, MeterUnit unit = MeterUnit.M3, int? digits = null, DateOnly? installed = null, Guid? replaces = null)
    {
        var meter = new Meter
        {
            PropertyId = _kit.Property.Id,
            Resource = MeterResource.ColdWater,
            Unit = unit,
            Location = location,
            Digits = digits,
            InstalledOn = installed ?? Long,
            ReplacedMeterId = replaces,
        };
        meter.Registers.Add(new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Total, Direction = EnergyDirection.Import });
        _kit.Meters.Stored.Add(meter);
        if (contract is not null)
        {
            _kit.Contracts.Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = Long });
        }

        return meter;
    }

    private static void Read(Meter meter, DateOnly date, decimal value, ReadingKind kind = ReadingKind.Regular) =>
        meter.Registers[0].Readings.Add(new MeterReading { RegisterId = meter.Registers[0].Id, Date = date, Value = value, Kind = kind });

    private TariffComponent AddComponent(Contract contract, string name, ChargeBasis basis, decimal price, bool includesVat = false, string vat = "LV-STANDARD", int order = 0)
    {
        var component = new TariffComponent
        {
            ContractId = contract.Id,
            Name = name,
            ChargeKind = ServiceKind.ColdWater,
            Basis = basis,
            VatCode = vat,
            SortOrder = order,
        };
        component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = Long, Price = price, PriceIncludesVat = includesVat, Currency = Currency.EUR });
        _kit.Tariffs.Stored.Add(component);
        return component;
    }

    private async Task<ChargesViewModel> LoadAsync()
    {
        var vm = _kit.CreateCharges();
        await vm.ReloadAsync();
        return vm;
    }

    /// <summary>A water contract with Sep 26 = 100.000 and Oct 26 = 105.500 on a cold meter and a 1.41 price.</summary>
    private (Contract Contract, Meter Meter, TariffComponent Component) AddWater()
    {
        var contract = AddContract("Water");
        var meter = AddMeter(contract, "Bathroom cold");
        Read(meter, new DateOnly(2026, 9, 26), 100m);
        Read(meter, new DateOnly(2026, 10, 26), 105.5m);
        var component = AddComponent(contract, "Water supply", ChargeBasis.PerUnit, 1.41m);
        return (contract, meter, component);
    }

    [Fact]
    public async Task MeteredLine_ShowsQuantityPriceAndTheCalculatorsAmounts()
    {
        // 5.500 m³ × 1.41 = 7.755 → net 7.76; VAT 21 % = 1.63; total 9.39
        AddWater();

        var vm = await LoadAsync();

        var card = Assert.Single(vm.Cards);
        Assert.Equal(ChargeCardStatus.Complete, card.Status);
        var line = Assert.Single(card.Lines);
        Assert.Equal("Water supply", line.Name);
        Assert.Equal("5.500 Enum_MeterUnit_M3 [en]", line.QuantityText);
        Assert.Equal("ChargeUnitPrice 1.41|Currency_Symbol_EUR [en]|TariffVatExcluded [en] [en]", line.UnitPriceText);
        Assert.Equal("7.76 Currency_Symbol_EUR [en]", line.NetText);
        Assert.Equal("ChargePercent 21 [en]", line.VatPercentText);
        Assert.Equal("1.63 Currency_Symbol_EUR [en]", line.VatText);
        Assert.Equal("9.39 Currency_Symbol_EUR [en]", line.TotalText);
        Assert.Equal("9.39 Currency_Symbol_EUR [en]", card.TotalText);
        Assert.Equal("ChargesTotals 7.76 Currency_Symbol_EUR [en]|1.63 Currency_Symbol_EUR [en]|9.39 Currency_Symbol_EUR [en] [en]", vm.TotalsText);
        Assert.False(vm.IsIncomplete);
    }

    [Fact]
    public async Task PriceIncludingVat_IsLabelledInclVat()
    {
        // 2 residents... fixed 22.95 incl. VAT: gross 22.95; net 22.95 / 1.21 = 18.97; VAT 3.98
        var contract = AddContract("Internet", null, null);
        AddComponent(contract, "Internet and TV", ChargeBasis.FixedMonthly, 22.95m, includesVat: true);

        var vm = await LoadAsync();

        var line = Assert.Single(Assert.Single(vm.Cards).Lines);
        Assert.Equal("1 Unit_Month [en]", line.QuantityText);
        Assert.Contains("TariffVatIncluded", line.UnitPriceText);
        Assert.Equal("18.97 Currency_Symbol_EUR [en]", line.NetText);
        Assert.Equal("3.98 Currency_Symbol_EUR [en]", line.VatText);
        Assert.Equal("22.95 Currency_Symbol_EUR [en]", line.TotalText);
    }

    [Fact]
    public async Task AreaAndResidentLines_UseThePropertyParameters()
    {
        // 52.30 m² × 0.60 = 31.38 net; VAT 21 % = 6.59; and 2 residents × 5.40 = 10.80 net
        _kit.Property.Parameters.Add(new PropertyParameter { PropertyId = _kit.Property.Id, Kind = PropertyParameterKind.TotalArea, Value = 52.3m, ValidFrom = Long });
        _kit.Property.Parameters.Add(new PropertyParameter { PropertyId = _kit.Property.Id, Kind = PropertyParameterKind.DeclaredResidents, Value = 2m, ValidFrom = Long });
        var contract = AddContract("Management", null, null);
        AddComponent(contract, "Maintenance", ChargeBasis.PerArea, 0.6m, order: 0);
        AddComponent(contract, "Waste", ChargeBasis.PerResident, 5.4m, order: 1);

        var vm = await LoadAsync();

        var lines = Assert.Single(vm.Cards).Lines;
        Assert.Equal("52.30 Unit_Area [en]", lines[0].QuantityText);
        Assert.Equal("31.38 Currency_Symbol_EUR [en]", lines[0].NetText);
        Assert.Equal("6.59 Currency_Symbol_EUR [en]", lines[0].VatText);
        Assert.Equal("2 ChargeUnitResident_Many [en]", lines[1].QuantityText);
        Assert.Equal("10.80 Currency_Symbol_EUR [en]", lines[1].NetText);
    }

    [Fact]
    public async Task FromBillLine_HasNoAmounts_AndTheCardIsFromBill()
    {
        var contract = AddContract("Heating", null, null);
        AddComponent(contract, "Heating", ChargeBasis.FromBill, 0m, vat: "LV-REDUCED");

        var vm = await LoadAsync();

        var card = Assert.Single(vm.Cards);
        Assert.Equal(ChargeCardStatus.FromBill, card.Status);
        Assert.False(card.HasTotal);
        var line = Assert.Single(card.Lines);
        Assert.True(line.IsFromBill);
        Assert.False(line.HasAmounts);
        Assert.Equal("ChargeFromBill [en]", line.FromBillText);
        Assert.Equal(string.Empty, line.NetText);
        Assert.Equal(string.Empty, vm.TotalsText);
    }

    [Fact]
    public async Task SteppedPrice_ShowsOneLinePerSlice()
    {
        // 150.000 kWh: 100 × 0.1065 = 10.65 and 50 × 0.0818 = 4.09 net
        var contract = AddContract("Electricity");
        var meter = AddMeter(contract, "Hall", MeterUnit.KWh);
        Read(meter, new DateOnly(2026, 9, 26), 1000m);
        Read(meter, new DateOnly(2026, 10, 26), 1150m);
        var energy = AddComponent(contract, "Energy", ChargeBasis.PerUnit, 0m);
        energy.Rates[0].Tiers = [new TariffTier(100m, 0.1065m), new TariffTier(null, 0.0818m)];
        energy.Rates[0].TierPeriod = TierPeriod.Month;

        var vm = await LoadAsync();

        var lines = Assert.Single(vm.Cards).Lines;
        Assert.Equal(2, lines.Count);
        Assert.Equal("100.000 Enum_MeterUnit_KWh [en]", lines[0].QuantityText);
        Assert.Equal("10.65 Currency_Symbol_EUR [en]", lines[0].NetText);
        Assert.Equal("50.000 Enum_MeterUnit_KWh [en]", lines[1].QuantityText);
        Assert.Equal("4.09 Currency_Symbol_EUR [en]", lines[1].NetText);
        Assert.Single(lines, l => l.HasDetails);
    }

    [Fact]
    public async Task ReadingsBehindALine_ShowStartEndAndConsumption_AndTapOpensThem()
    {
        AddWater();

        var vm = await LoadAsync();

        var line = vm.Cards[0].Lines[0];
        Assert.False(line.IsDetailOpen);
        line.ToggleDetailCommand.Execute(null);
        Assert.True(line.IsDetailOpen);
        var detail = Assert.Single(line.Details);
        Assert.Equal("Bathroom cold", detail.Header);
        Assert.Equal(
            [
                $"ChargeReadingStart 100.000|{new DateOnly(2026, 9, 26)} [en]",
                $"ChargeReadingEnd 105.500|{new DateOnly(2026, 10, 26)} [en]",
                "ChargeReadingConsumption 5.500|Enum_MeterUnit_M3 [en] [en]",
            ],
            detail.Lines);
        Assert.Empty(detail.Notes);
    }

    [Fact]
    public async Task ReplacedMeter_ShowsTwoSegments()
    {
        // old meter 120.000 → final 125.500 = 5.500; new meter initial 0.000 → 2.000 = 2.000; total 7.500
        var contract = AddContract("Water");
        var old = AddMeter(null, "Bathroom cold", digits: 6);
        old.RemovedOn = new DateOnly(2026, 10, 15);
        Read(old, new DateOnly(2026, 9, 26), 120m);
        Read(old, new DateOnly(2026, 10, 15), 125.5m, ReadingKind.Final);
        var replacement = AddMeter(contract, "Bathroom cold", installed: new DateOnly(2026, 10, 15), replaces: old.Id);
        Read(replacement, new DateOnly(2026, 10, 15), 0m, ReadingKind.Initial);
        Read(replacement, new DateOnly(2026, 10, 26), 2m);
        AddComponent(contract, "Water supply", ChargeBasis.PerUnit, 1m);

        var vm = await LoadAsync();

        var line = Assert.Single(Assert.Single(vm.Cards).Lines);
        Assert.Equal("7.500 Enum_MeterUnit_M3 [en]", line.QuantityText);
        var lines = Assert.Single(line.Details).Lines;
        Assert.Contains(lines, l => l.StartsWith("ChargeSegmentOld Bathroom cold|120.000|125.500|5.500", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.StartsWith("ChargeSegmentNew Bathroom cold|0.000|2.000|2.000", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Rollover_IsFlaggedOnTheReadingsRow()
    {
        // dial of 4 digits: 9990 → 0010 = 20.000
        var contract = AddContract("Water");
        var meter = AddMeter(contract, "Kitchen cold", digits: 4);
        Read(meter, new DateOnly(2026, 9, 26), 9990m);
        Read(meter, new DateOnly(2026, 10, 26), 10m);
        AddComponent(contract, "Water supply", ChargeBasis.PerUnit, 1m);

        var vm = await LoadAsync();

        var line = Assert.Single(Assert.Single(vm.Cards).Lines);
        Assert.Equal("20.000 Enum_MeterUnit_M3 [en]", line.QuantityText);
        var note = Assert.Single(Assert.Single(line.Details).Notes);
        Assert.StartsWith("ChargeNoteRollover Kitchen cold|9,990.000|10.000", note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoRateForDate_ExplainsAndOpensTheComponent()
    {
        var contract = AddContract("Water");
        var component = AddComponent(contract, "Sewerage", ChargeBasis.FixedMonthly, 1m);
        component.Rates[0].ValidFrom = new DateOnly(2026, 11, 1);

        var vm = await LoadAsync();

        var card = Assert.Single(vm.Cards);
        Assert.Equal(ChargeCardStatus.Incomplete, card.Status);
        Assert.True(card.ShowWarningIcon);
        Assert.True(vm.IsIncomplete);
        var warning = Assert.Single(card.Warnings);
        Assert.StartsWith($"ChargeWarn_NoRate {new DateOnly(2026, 10, 1)}|Sewerage", warning.Message, StringComparison.Ordinal);
        Assert.Equal("ChargeOpenTariff [en]", warning.ActionText);

        await warning.ActionCommand!.ExecuteAsync(null);

        var (route, parameters) = _kit.Navigation.Visited[^1];
        Assert.Equal(Routes.TariffComponentEdit, route);
        Assert.Equal(contract.Id, parameters![EditorNavigation.ContractIdParameter]);
        Assert.Equal(component.Id, parameters[EditorNavigation.IdParameter]);
    }

    [Fact]
    public async Task MissingParameter_OpensTheProperty()
    {
        var contract = AddContract("Management", null, null);
        AddComponent(contract, "Maintenance", ChargeBasis.PerArea, 0.6m);

        var vm = await LoadAsync();

        var warning = Assert.Single(Assert.Single(vm.Cards).Warnings);
        Assert.Contains("Enum_PropertyParameterKind_TotalArea [en]", warning.Message);
        await warning.ActionCommand!.ExecuteAsync(null);
        var (route, parameters) = _kit.Navigation.Visited[^1];
        Assert.Equal(Routes.PropertyEdit, route);
        Assert.Equal(_kit.Property.Id, parameters![EditorNavigation.IdParameter]);
    }

    [Fact]
    public async Task MissingEndReading_NamesTheMeter_AndOpensReadingsDatedInTheMonth()
    {
        var contract = AddContract("Water");
        var meter = AddMeter(contract, "Kitchen cold");
        Read(meter, new DateOnly(2026, 9, 26), 100m);
        AddComponent(contract, "Water supply", ChargeBasis.PerUnit, 1.41m);

        var vm = await LoadAsync();

        var card = Assert.Single(vm.Cards);
        Assert.Equal(ChargeCardStatus.Incomplete, card.Status);
        Assert.False(Assert.Single(card.Lines).HasAmounts);
        var warning = Assert.Single(card.Warnings);
        Assert.Equal("ChargeWarn_ReadingsMissing Kitchen cold [en]", warning.Message);
        await warning.ActionCommand!.ExecuteAsync(null);
        var (route, parameters) = _kit.Navigation.Visited[^1];
        Assert.Equal(Routes.ReadingsFor, route);
        Assert.Equal(_kit.Property.Id, parameters![EditorNavigation.PropertyIdParameter]);
        Assert.Equal(Today, parameters[ReadingsViewModel.DateParameter]);
    }

    [Fact]
    public async Task NoReadingsAtAll_NamesTheComponent()
    {
        var contract = AddContract("Water");
        AddMeter(contract, "Kitchen cold");
        AddComponent(contract, "Water supply", ChargeBasis.PerUnit, 1.41m);

        var vm = await LoadAsync();

        Assert.Equal("ChargeWarn_ReadingsMissing Water supply [en]", Assert.Single(Assert.Single(vm.Cards).Warnings).Message);
    }

    [Fact]
    public async Task NoMeters_OpensTheContract()
    {
        var contract = AddContract("Water");
        AddComponent(contract, "Water supply", ChargeBasis.PerUnit, 1.41m);

        var vm = await LoadAsync();

        var warning = Assert.Single(Assert.Single(vm.Cards).Warnings);
        Assert.Equal("ChargeWarn_NoMeters Water supply [en]", warning.Message);
        await warning.ActionCommand!.ExecuteAsync(null);
        var (route, parameters) = _kit.Navigation.Visited[^1];
        Assert.Equal(Routes.ContractEdit, route);
        Assert.Equal(contract.Id, parameters![EditorNavigation.IdParameter]);
        Assert.Equal(_kit.Property.Id, parameters[EditorNavigation.PropertyIdParameter]);
    }

    [Fact]
    public async Task UnknownVat_CurrencyMismatch_InvalidTariff_AreShownAsMessages()
    {
        var contract = AddContract("Mixed", null, null);
        AddComponent(contract, "Unknown VAT", ChargeBasis.FixedMonthly, 1m, vat: "NOPE", order: 0);
        var foreign = AddComponent(contract, "Foreign price", ChargeBasis.FixedMonthly, 1m, order: 1);
        foreign.Rates[0].Currency = Currency.LVL;
        var meter = AddMeter(contract, "Export meter");
        meter.Registers.Add(new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Export, Direction = EnergyDirection.Export });
        var export = AddComponent(contract, "Export", ChargeBasis.NetExport, 0m, order: 2);
        export.Rates[0].Tiers = [new TariffTier(null, 0.05m)];

        var vm = await LoadAsync();

        var messages = Assert.Single(vm.Cards).Warnings.Select(w => w.Message).ToList();
        Assert.Contains("ChargeWarn_UnknownVat NOPE|Unknown VAT [en]", messages);
        Assert.Contains("ChargeWarn_CurrencyMismatch Foreign price|LVL|EUR [en]", messages);
        Assert.Contains("ChargeWarn_InvalidTariff Export [en]", messages);
    }

    [Fact]
    public async Task TierUsageUnknown_IsAPlainMessage()
    {
        // a yearly stepped price needs the usage since 1 Jan, but there is only one reading
        var contract = AddContract("Electricity");
        var meter = AddMeter(contract, "Hall", MeterUnit.KWh);
        Read(meter, new DateOnly(2026, 9, 26), 1000m);
        Read(meter, new DateOnly(2026, 10, 26), 1100m);
        var energy = AddComponent(contract, "Energy", ChargeBasis.PerUnit, 0m);
        energy.Rates[0].Tiers = [new TariffTier(1200m, 0.1m), new TariffTier(null, 0.2m)];
        energy.Rates[0].TierPeriod = TierPeriod.Year;

        var vm = await LoadAsync();

        var warning = Assert.Single(Assert.Single(vm.Cards).Warnings);
        Assert.Equal("ChargeWarn_TierUsageUnknown Energy [en]", warning.Message);
        Assert.False(warning.HasAction);
    }

    [Fact]
    public async Task ContractActiveOnlyInTheMiddleOfTheMonth_ShowsTheInactiveMessage()
    {
        AddContract("Running");
        var contract = AddContract("Short", null, null, new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 20));
        AddComponent(contract, "Fee", ChargeBasis.FixedMonthly, 1m);

        var vm = await LoadAsync();

        var card = vm.Cards.Single(c => c.Title == "Short");
        Assert.Equal("ChargeWarn_ContractInactive [en]", Assert.Single(card.Warnings).Message);
    }

    [Fact]
    public async Task ContractWithoutTariff_ShowsTheHintAndALinkToTheContract()
    {
        var contract = AddContract("Water");

        var vm = await LoadAsync();

        var card = Assert.Single(vm.Cards);
        Assert.Equal(ChargeCardStatus.NoTariff, card.Status);
        Assert.True(card.ShowNoTariff);
        Assert.False(card.HasTotal);
        Assert.False(vm.IsIncomplete);
        await card.NoTariffCommand!.ExecuteAsync(null);
        var (route, parameters) = _kit.Navigation.Visited[^1];
        Assert.Equal(Routes.ContractEdit, route);
        Assert.Equal(contract.Id, parameters![EditorNavigation.IdParameter]);
    }

    [Fact]
    public async Task InactiveContracts_AreHidden_AndOneEndedInTheMonthIsShown()
    {
        AddContract("Ended before", validTo: new DateOnly(2026, 9, 30));
        AddContract("Starts later", validFrom: new DateOnly(2026, 11, 1));
        AddContract("Ends in the month", validTo: new DateOnly(2026, 10, 5));
        AddContract("Running");

        var vm = await LoadAsync();

        Assert.Equal(["Ends in the month", "Running"], vm.Cards.Select(c => c.Title));
    }

    [Fact]
    public async Task ErrorInOneContract_BecomesAnErrorCard_AndTheRestWorks()
    {
        var (broken, _, _) = AddWater();
        var fine = AddContract("Internet", null, null);
        AddComponent(fine, "Internet", ChargeBasis.FixedMonthly, 10m);
        _kit.ChargeLoader.Failures[broken.Id] = new InvalidOperationException("boom");

        var vm = await LoadAsync();

        Assert.Equal(["Internet", "Water"], vm.Cards.Select(c => c.Title));
        Assert.Equal(ChargeCardStatus.Complete, vm.Cards[0].Status);
        var error = vm.Cards[1];
        Assert.Equal(ChargeCardStatus.Error, error.Status);
        Assert.True(error.ShowError);
        Assert.True(error.ShowWarningIcon);
        Assert.Equal("ChargeCardError [en]", error.ErrorText);
        Assert.Contains("boom", Assert.Single(_kit.ErrorLog.Entries).Exception.Message);
        Assert.True(vm.IsIncomplete);
        Assert.Equal("ChargesTotals 10.00 Currency_Symbol_EUR [en]|2.10 Currency_Symbol_EUR [en]|12.10 Currency_Symbol_EUR [en] [en]", vm.TotalsText);
    }

    [Fact]
    public async Task Appearing_RecalculatesWithTheCurrentData()
    {
        var (_, meter, _) = AddWater();
        var vm = await LoadAsync();
        Assert.Equal("9.39 Currency_Symbol_EUR [en]", vm.Cards[0].TotalText);

        Read(meter, new DateOnly(2026, 10, 27), 106.5m);
        await vm.OnAppearingAsync();

        // the window is 25–28, the latest reading in it is now 106.500: 6.500 × 1.41 = 9.165 → net 9.17; VAT 21 % of 9.17 = 1.9257 → 1.93; total 11.10
        Assert.Equal("11.10 Currency_Symbol_EUR [en]", vm.Cards[0].TotalText);
    }

    [Fact]
    public async Task DefaultMonth_IsTheCurrentOneOnceAWindowHasStarted_ElseThePreviousOne()
    {
        AddContract("Water", 25, 28);
        _kit.Clock.Today = new DateOnly(2026, 10, 24);
        var before = await LoadAsync();
        _kit.Clock.Today = new DateOnly(2026, 10, 25);
        var started = await LoadAsync();

        Assert.Equal(new YearMonth(2026, 9), before.Month);
        Assert.Equal(new YearMonth(2026, 10), started.Month);
        Assert.Equal("ChargesNoContracts [en]", before.NoContractsText);
    }

    [Fact]
    public async Task MonthButtons_MoveTheMonth_AndKeepItOnReload()
    {
        AddWater();
        var vm = await LoadAsync();

        await vm.PreviousMonthCommand.ExecuteAsync(null);
        Assert.Equal(new YearMonth(2026, 9), vm.Month);
        await vm.OnAppearingAsync();
        Assert.Equal(new YearMonth(2026, 9), vm.Month);
        await vm.NextMonthCommand.ExecuteAsync(null);
        await vm.NextMonthCommand.ExecuteAsync(null);
        Assert.Equal(new YearMonth(2026, 11), vm.Month);
        Assert.Equal("November 2026", vm.MonthText);
        Assert.Equal(new DateOnly(2026, 11, 1), vm.PickerDate);
    }

    [Fact]
    public async Task PickerDate_ChoosesTheMonthOfTheDate()
    {
        AddWater();
        var vm = await LoadAsync();

        vm.PickerDate = new DateOnly(2026, 3, 17);
        await vm.PendingMonthChange;

        Assert.Equal(new YearMonth(2026, 3), vm.Month);
        Assert.DoesNotContain(vm.Cards.SelectMany(c => c.Lines), l => l.HasAmounts);
    }

    [Fact]
    public async Task SwitchingProperty_LoadsItsContracts_ResetsTheMonthAndRemembersTheChoice()
    {
        AddWater();
        var cottage = new Property { Name = "Cottage", Kind = PropertyKind.House };
        _kit.Properties.Stored.Add(cottage);
        var power = AddContract("Cottage power", 28, 3, property: cottage);
        AddComponent(power, "Fee", ChargeBasis.FixedMonthly, 5m);
        var vm = await LoadAsync();
        await vm.PreviousMonthCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Properties.Count);

        vm.SelectedPropertyIndex = vm.Properties.ToList().FindIndex(p => p.Id == cottage.Id);
        await vm.PendingPropertyChange;

        // window 28–3 has not started on 27 Oct → September
        Assert.Equal(new YearMonth(2026, 9), vm.Month);
        Assert.Equal(["Cottage power"], vm.Cards.Select(c => c.Title));
        Assert.Equal(cottage.Id.ToString(), _kit.Settings.Values[ChargesViewModel.LastPropertyKey]);
        Assert.Equal(cottage.Id, (await LoadAsync()).SelectedPropertyId);
    }

    [Fact]
    public async Task PropertyWithoutContracts_AndNoProperties_ShowTheirHints()
    {
        var vm = await LoadAsync();
        Assert.True(vm.ShowNoContracts);
        Assert.Empty(vm.Cards);
        Assert.Equal(string.Empty, vm.TotalsText);

        _kit.Properties.Stored.Clear();
        await vm.ReloadAsync();
        Assert.True(vm.ShowNoProperties);
        Assert.False(vm.ShowNoContracts);
    }

    [Fact]
    public async Task Cards_StartCollapsedOnPhones_ExpandedWhenWide_AndRememberTheUsersChoice()
    {
        AddContract("A", null, null);
        AddContract("B", null, null);
        var vm = await LoadAsync();
        Assert.All(vm.Cards, c => Assert.False(c.IsExpanded));

        vm.Cards[0].ToggleExpandedCommand.Execute(null);
        await vm.OnAppearingAsync();
        Assert.True(vm.Cards[0].IsExpanded);
        Assert.False(vm.Cards[1].IsExpanded);

        _kit.Layout.Change(LayoutMode.Expanded);
        Assert.All(vm.Cards, c => Assert.True(c.IsExpanded));
    }

    [Fact]
    public async Task OpenFor_SelectsThePropertyAndExpandsAndFocusesTheContract()
    {
        var (water, _, _) = AddWater();
        var other = AddContract("Other", null, null);
        var vm = _kit.CreateCharges();

        await vm.OpenForAsync(_kit.Property.Id, water.Id);
        await vm.OnAppearingAsync();

        Assert.Equal(water.Id, vm.FocusedCard!.ContractId);
        Assert.True(vm.FocusedCard.IsExpanded);
        Assert.False(vm.Cards.Single(c => c.ContractId == other.Id).IsExpanded);
        Assert.Equal(new YearMonth(2026, 10), vm.Month);
    }

    [Fact]
    public async Task HelpCommand_OpensTheChargesSection()
    {
        var vm = await LoadAsync();

        await vm.OpenHelpCommand.ExecuteAsync(null);

        var (route, parameters) = _kit.Navigation.Visited[^1];
        Assert.Equal(Routes.HelpSection, route);
        Assert.Equal(HelpTopic.Charges, parameters![HelpNavigation.TopicParameter]);
    }

    [Fact]
    public async Task LanguageChange_RebuildsTheTextsWithoutRecalculating()
    {
        AddWater();
        var vm = await LoadAsync();

        _kit.Language.SetLanguage("ru");

        Assert.Equal("SectionCharges [ru]", vm.Title);
        Assert.Contains("[ru]", vm.Cards[0].StatusText);
        Assert.Contains("[ru]", vm.TotalsText);
    }

    [Fact]
    public async Task TwoCurrencies_GetATotalLineEach()
    {
        var eur = AddContract("Euro", null, null);
        AddComponent(eur, "Fee", ChargeBasis.FixedMonthly, 10m);
        var old = AddContract("Lats", null, null);
        old.Currency = Currency.LVL;
        var lats = AddComponent(old, "Fee", ChargeBasis.FixedMonthly, 2m);
        lats.Rates[0].Currency = Currency.LVL;

        var vm = await LoadAsync();

        var lines = vm.TotalsText.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains(lines, l => l.Contains("12.10 Currency_Symbol_EUR"));
        Assert.Contains(lines, l => l.Contains("2.42 Currency_Symbol_LVL"));
    }

    // ---- Bills (T-019) --------------------------------------------------------------------------------------------

    [Fact]
    public async Task CardWithoutABill_OffersEnterBill_WithTheCalculatedTotalAsASuggestion()
    {
        // 5.500 m³ × 1.41 → total 9.39
        var (contract, _, _) = AddWater();
        var vm = await LoadAsync();

        var card = Assert.Single(vm.Cards);
        Assert.False(card.HasBill);
        Assert.True(card.ShowEnterBill);
        Assert.Equal("ChargesEnterBill [en]", card.EnterBillText);

        card.EnterBillCommand!.Execute(null);
        await Task.Delay(50);

        var (route, parameters) = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.BillEdit, route);
        Assert.Equal(contract.Id, parameters![EditorNavigation.ContractIdParameter]);
        Assert.Equal(_kit.Property.Id, parameters[EditorNavigation.PropertyIdParameter]);
        Assert.Equal("2026-10", parameters[BillEditViewModel.PeriodParameter]);
        Assert.Equal(9.39m, parameters[BillEditViewModel.GrossParameter]);
    }

    [Fact]
    public async Task CardWithoutATariff_OffersEnterBill_WithoutASuggestion()
    {
        AddContract("Waste", null, null);
        var vm = await LoadAsync();

        vm.Cards[0].EnterBillCommand!.Execute(null);
        await Task.Delay(50);

        var (_, parameters) = Assert.Single(_kit.Navigation.Visited);
        Assert.False(parameters!.ContainsKey(BillEditViewModel.GrossParameter));
    }

    [Fact]
    public async Task CardWithABill_ShowsTheBillTheDifferenceAndTheStatus_AndOpensTheBill()
    {
        var (contract, _, _) = AddWater();
        var bill = Bill.CreateFor(contract, new YearMonth(2026, 10), _kit.Clock);
        bill.Gross = 10.31m;
        bill.DueOn = new DateOnly(2026, 11, 20);
        bill.CalculatedSnapshot = new CalculatedCharge(
            contract.Id, new YearMonth(2026, 10), Currency.EUR,
            [new ChargeLine(Guid.NewGuid(), "Water", ServiceKind.ColdWater, contract.CategoryId, ChargeBasis.PerUnit, 5.5m, 1.41m, 7.76m, 21m, 1.63m, 9.39m, false, true, [])],
            7.76m, 1.63m, 9.39m, true, []);
        _kit.Bills.All.Add(bill);
        var payment = new Payment { ContractId = contract.Id, PaidOn = Today, Amount = 10.31m };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = 10.31m });
        _kit.Payments.Add(payment);

        var vm = await LoadAsync();

        // bill 10.31 against the calculated 9.39: +0.92
        var card = Assert.Single(vm.Cards);
        Assert.True(card.HasBill);
        Assert.False(card.ShowEnterBill);
        Assert.Equal("ChargesBillLine 10.31 Currency_Symbol_EUR [en] · +0.92 Currency_Symbol_EUR [en] · BillBadgePaid [en] [en]", card.BillText);

        card.OpenBillCommand!.Execute(null);
        await Task.Delay(50);

        var (route, parameters) = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.BillEdit, route);
        Assert.Equal(bill.Id, parameters![EditorNavigation.IdParameter]);
    }

    [Fact]
    public async Task OpenFor_WithAMonth_ShowsThatMonth()
    {
        AddWater();
        var vm = await LoadAsync();

        await vm.OpenForAsync(_kit.Property.Id, null, new YearMonth(2026, 9));

        Assert.Equal(new YearMonth(2026, 9), vm.Month);
    }
}
