using System.Globalization;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class TariffComponentEditViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    private readonly ContractTestKit _kit = new(Today);
    private readonly Contract _contract;
    private readonly Meter _cold;
    private readonly Meter _hot;

    public TariffComponentEditViewModelTests()
    {
        _contract = new Contract
        {
            PropertyId = _kit.Property.Id,
            Title = "Water",
            Service = ServiceKind.ColdWater,
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        _kit.Contracts.Stored.Add(_contract);
        _kit.Property.Parameters.Add(new PropertyParameter { PropertyId = _kit.Property.Id, Kind = PropertyParameterKind.TotalArea, Value = 52.3m, ValidFrom = new DateOnly(2020, 1, 1) });
        _kit.Property.Parameters.Add(new PropertyParameter { PropertyId = _kit.Property.Id, Kind = PropertyParameterKind.DeclaredResidents, Value = 3m, ValidFrom = new DateOnly(2020, 1, 1) });
        _cold = AddMeter(MeterResource.ColdWater, MeterUnit.M3, "Kitchen cold", RegisterKind.Total);
        _hot = AddMeter(MeterResource.HotWater, MeterUnit.M3, "Bathroom hot", RegisterKind.Total);
    }

    [Fact]
    public async Task NewComponent_StartsWithTheServiceOfTheContractStandardVatAndPerUnit()
    {
        var vm = await NewEditorAsync();

        Assert.True(vm.IsNew);
        Assert.Equal(ServiceKind.ColdWater, vm.SelectedService!.Value);
        Assert.Equal(ChargeBasis.PerUnit, vm.Basis);
        Assert.Equal("LV-STANDARD", vm.SelectedVatOption!.Code);
        Assert.Equal("TariffComponentNew [en]", vm.Title);
        Assert.Equal("TariffCategorySameAsContract [en]", vm.SelectedCategoryName);
    }

    [Theory]
    [InlineData(ChargeBasis.PerUnit, true, false, true)]
    [InlineData(ChargeBasis.NetExport, true, false, true)]
    [InlineData(ChargeBasis.PerArea, false, true, true)]
    [InlineData(ChargeBasis.PerResident, false, true, true)]
    [InlineData(ChargeBasis.FixedMonthly, false, false, true)]
    [InlineData(ChargeBasis.FromBill, false, false, false)]
    public async Task Basis_DecidesWhichCardsAreVisible(ChargeBasis basis, bool meters, bool parameter, bool prices)
    {
        var vm = await NewEditorAsync();

        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(basis));

        Assert.Equal(meters, vm.ShowMeterSelection);
        Assert.Equal(meters, vm.ShowRegisterSelection);
        Assert.Equal(parameter, vm.ShowParameter);
        Assert.Equal(meters || parameter, vm.ShowCountedCard);
        Assert.Equal(prices, vm.ShowPrices);
        Assert.Equal($"TariffBasisHint_{basis} [en]", vm.BasisHint);
    }

    [Fact]
    public async Task AllMetersOn_StoresEmptyMeterIds_AndAPartialChoiceStoresTheIds()
    {
        var vm = await FilledAsync();
        Assert.Equal(new[] { _cold.Id, _hot.Id }.Order(), vm.Meters.Select(m => m.MeterId).Order());
        Assert.All(vm.Meters, m => Assert.True(m.IsOn));

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(_kit.Tariffs.Saved[^1].MeterIds);

        vm.Meters.Single(m => m.MeterId == _cold.Id).IsOn = false;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal([_hot.Id], _kit.Tariffs.Saved[^1].MeterIds);
    }

    [Fact]
    public async Task AllMetersOff_ShowsAnErrorAndDoesNotSave()
    {
        var vm = await FilledAsync();
        foreach (var meter in vm.Meters)
        {
            meter.IsOn = false;
        }

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Tariffs.Saved);
        Assert.Equal("Error_tariff_editor_no_meter_selected [en]", vm.CountedError);
    }

    [Fact]
    public async Task Registers_ListTheKindsOfTheSelectedMeters_AndAllOnStoresNone()
    {
        var dual = AddMeter(MeterResource.Electricity, MeterUnit.KWh, "Hall", RegisterKind.Day, RegisterKind.Night, RegisterKind.Export);
        _kit.Contracts.Links.Add(new ContractMeter { ContractId = _contract.Id, MeterId = dual.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        var vm = await FilledAsync();

        // Water meters have Total, the hall meter Day and Night; Export is an export register and is not offered for PerUnit.
        Assert.Equal([RegisterKind.Total, RegisterKind.Day, RegisterKind.Night], vm.Registers.Select(r => r.Kind));

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(_kit.Tariffs.Saved[^1].RegisterKinds);

        vm.Registers.Single(r => r.Kind == RegisterKind.Night).IsOn = false;
        vm.Registers.Single(r => r.Kind == RegisterKind.Total).IsOn = false;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal([RegisterKind.Day], _kit.Tariffs.Saved[^1].RegisterKinds);
    }

    [Fact]
    public async Task RegistersFollowTheSwitchedOnMeters()
    {
        var dual = AddMeter(MeterResource.Electricity, MeterUnit.KWh, "Hall", RegisterKind.Day, RegisterKind.Night);
        _kit.Contracts.Links.Add(new ContractMeter { ContractId = _contract.Id, MeterId = dual.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        var vm = await FilledAsync();

        vm.Meters.Single(m => m.MeterId == _cold.Id).IsOn = false;
        vm.Meters.Single(m => m.MeterId == _hot.Id).IsOn = false;

        Assert.Equal([RegisterKind.Day, RegisterKind.Night], vm.Registers.Select(r => r.Kind));
    }

    [Fact]
    public async Task NetExport_ShowsOnlyTheExportRegister_AndStoresIt()
    {
        var solar = AddMeter(MeterResource.Electricity, MeterUnit.KWh, "Cottage", RegisterKind.Day, RegisterKind.Export);
        _kit.Contracts.Links.Add(new ContractMeter { ContractId = _contract.Id, MeterId = solar.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        var vm = await FilledAsync();
        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(ChargeBasis.NetExport));

        var export = Assert.Single(vm.Registers);
        Assert.Equal(RegisterKind.Export, export.Kind);
        Assert.True(export.IsOn);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal([RegisterKind.Export], _kit.Tariffs.Saved[^1].RegisterKinds);
    }

    [Fact]
    public async Task PerArea_ShowsTheCurrentValueOfTheParameter_AndStoresNullForTheDefault()
    {
        var vm = await FilledAsync(ChargeBasis.PerArea);

        Assert.Contains(DecimalInput.Format(52.3m, CultureInfo.CurrentCulture, 2), vm.ParameterNowText);

        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(_kit.Tariffs.Saved[^1].QuantityParameter);

        vm.SelectedParameter = vm.Parameters.Single(o => o.Value.Equals(PropertyParameterKind.HeatedArea));
        Assert.Equal("TariffParameterMissing [en]", vm.ParameterNowText);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(PropertyParameterKind.HeatedArea, _kit.Tariffs.Saved[^1].QuantityParameter);
    }

    [Fact]
    public async Task PerResident_OffersResidentsAndACustomParameter()
    {
        var vm = await FilledAsync(ChargeBasis.PerResident);

        Assert.Equal([PropertyParameterKind.DeclaredResidents, PropertyParameterKind.Custom], vm.Parameters.Select(p => p.Value));
        Assert.Contains("3", vm.ParameterNowText);

        vm.SelectedParameter = vm.Parameters.Single(o => o.Value.Equals(PropertyParameterKind.Custom));
        Assert.True(vm.ShowCustomParameter);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Error_tariff_quantity_parameter_name_required [en]", vm.CountedError);

        vm.CustomParameterName = " Pets ";
        await vm.SaveCommand.ExecuteAsync(null);
        var saved = _kit.Tariffs.Saved[^1];
        Assert.Equal((PropertyParameterKind.Custom, "Pets"), (saved.QuantityParameter, saved.QuantityParameterName));
    }

    [Fact]
    public async Task Vat_CatalogEntryOrCustomPercent()
    {
        var vm = await FilledAsync();
        Assert.Equal(["LV-REDUCED", "LV-STANDARD", "ZERO", string.Empty], vm.VatOptions.Select(o => o.Code));

        vm.SelectedVatOption = vm.VatOptions.Single(o => o.Code == "LV-REDUCED");
        await vm.SaveCommand.ExecuteAsync(null);
        var catalog = _kit.Tariffs.Saved[^1];
        Assert.Equal(("LV-REDUCED", (decimal?)null), (catalog.VatCode, catalog.VatPercentOverride));

        vm.SelectedVatOption = vm.VatOptions.Single(o => o.IsCustom);
        Assert.True(vm.IsCustomVat);
        vm.VatPercentText = "15,5";
        await vm.SaveCommand.ExecuteAsync(null);
        var custom = _kit.Tariffs.Saved[^1];
        Assert.Equal((string.Empty, (decimal?)15.5m), (custom.VatCode, custom.VatPercentOverride));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("101")]
    [InlineData("-1")]
    public async Task CustomVat_WithBadText_ShowsAMessageUnderTheField(string text)
    {
        var vm = await FilledAsync();
        vm.SelectedVatOption = vm.VatOptions.Single(o => o.IsCustom);
        vm.VatPercentText = text;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Tariffs.Saved);
        Assert.NotEmpty(vm.VatError);
    }

    [Fact]
    public async Task RateDialog_AddsAPriceNewestFirst()
    {
        var vm = await NewEditorAsync();

        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "1,32");
        await AddRateAsync(vm, new DateOnly(2026, 9, 1), "1,41");

        Assert.Equal([new DateOnly(2026, 9, 1), new DateOnly(2026, 1, 1)], vm.Rates.Select(r => r.Rate.ValidFrom));
        Assert.Equal([1.41m, 1.32m], vm.Rates.Select(r => r.Rate.Price));
        Assert.All(vm.Rates, r => Assert.Equal(Currency.EUR, r.Rate.Currency));
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task RateDialog_TheNewPriceStartsFromTheNewestOneForTheVatFlag()
    {
        var vm = await NewEditorAsync();
        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "22,95", includesVat: true);

        await vm.AddRateCommand.ExecuteAsync(null);

        Assert.True(((TariffRateEditorViewModel)_kit.Dialogs.LastEditor!).PriceIncludesVat);
    }

    [Fact]
    public async Task RateDialog_TwoPricesOnTheSameDate_AreRefused()
    {
        var vm = await NewEditorAsync();
        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "1,32");

        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "1,41");

        Assert.Single(vm.Rates);
        var dialog = (TariffRateEditorViewModel)_kit.Dialogs.LastEditor!;
        Assert.False(dialog.Saved);
        Assert.StartsWith("Error_versioned_duplicate_valid_from ", dialog.ValidFromError);
    }

    [Fact]
    public async Task RateDialog_SteppedPrice_StoresTheTiersAndThePeriod()
    {
        var vm = await NewEditorAsync();
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (TariffRateEditorViewModel)editor;
            dialog.ValidFrom = new DateOnly(2026, 1, 1);
            dialog.IsStepped = true;
            Assert.Equal(2, dialog.TierRows.Count);
            Assert.True(dialog.TierRows[1].IsLast);
            dialog.TierRows[0].UpToText = "100";
            dialog.TierRows[0].PriceText = "0,1065";
            dialog.TierRows[1].PriceText = "0,0818";
            dialog.SelectedTierPeriod = dialog.TierPeriods.Single(o => o.Value.Equals(TierPeriod.Year));
            dialog.SaveCommand.Execute(null);
        };

        await vm.AddRateCommand.ExecuteAsync(null);

        var rate = Assert.Single(vm.Rates).Rate;
        Assert.Equal([new TariffTier(100m, 0.1065m), new TariffTier(null, 0.0818m)], rate.Tiers);
        Assert.Equal(TierPeriod.Year, rate.TierPeriod);
        Assert.True(Assert.Single(vm.Rates).IsStepped);
    }

    [Fact]
    public async Task RateDialog_LastTierMustBeUnlimited()
    {
        var vm = await NewEditorAsync();
        TariffRateEditorViewModel? dialog = null;
        _kit.Dialogs.EditorScript = editor =>
        {
            dialog = (TariffRateEditorViewModel)editor;
            dialog.IsStepped = true;
            dialog.TierRows[0].UpToText = "100";
            dialog.TierRows[0].PriceText = "0,1";
            dialog.TierRows[1].UpToText = "500";
            dialog.TierRows[1].PriceText = "0,08";
            dialog.SaveCommand.Execute(null);
        };

        await vm.AddRateCommand.ExecuteAsync(null);

        Assert.Empty(vm.Rates);
        Assert.False(dialog!.Saved);
        Assert.Equal("Error_tariff_tier_last_not_unlimited [en]", dialog.TiersError);
    }

    [Fact]
    public async Task RateDialog_TierThresholdsMustIncrease_AndAtLeastTwoStepsStay()
    {
        var vm = await NewEditorAsync();
        TariffRateEditorViewModel? dialog = null;
        _kit.Dialogs.EditorScript = editor =>
        {
            dialog = (TariffRateEditorViewModel)editor;
            dialog.IsStepped = true;
            dialog.AddTierCommand.Execute(null);
            dialog.TierRows[0].UpToText = "200";
            dialog.TierRows[0].PriceText = "0,1";
            dialog.TierRows[1].UpToText = "100";
            dialog.TierRows[1].PriceText = "0,09";
            dialog.TierRows[2].PriceText = "0,08";
            dialog.SaveCommand.Execute(null);
        };

        await vm.AddRateCommand.ExecuteAsync(null);

        Assert.Equal("Error_tariff_tier_order_invalid [en]", dialog!.TiersError);
        Assert.Equal(3, dialog.TierRows.Count);
        dialog.TierRows[1].RemoveCommand.Execute(null);
        Assert.Equal(2, dialog.TierRows.Count);
        Assert.False(dialog.TierRows[0].CanRemove);
        Assert.True(dialog.TierRows[^1].IsLast);
        dialog.TierRows[0].RemoveCommand.Execute(null);
        Assert.Equal(2, dialog.TierRows.Count);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1,1234567")]
    [InlineData("99999999999999999999999")]
    [InlineData("")]
    public async Task RateDialog_BadPrice_ShowsAMessage(string text)
    {
        var vm = await NewEditorAsync();
        TariffRateEditorViewModel? dialog = null;
        _kit.Dialogs.EditorScript = editor =>
        {
            dialog = (TariffRateEditorViewModel)editor;
            dialog.PriceText = text;
            dialog.SaveCommand.Execute(null);
        };

        await vm.AddRateCommand.ExecuteAsync(null);

        Assert.Empty(vm.Rates);
        Assert.NotEmpty(dialog!.PriceError);
    }

    [Fact]
    public async Task RateDialog_SixDecimalsAreAccepted()
    {
        var vm = await NewEditorAsync();

        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "0,123456");

        Assert.Equal(0.123456m, Assert.Single(vm.Rates).Rate.Price);
    }

    [Fact]
    public async Task RateDialog_NetExportHasNoSteppedPrice()
    {
        var vm = await NewEditorAsync();
        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(ChargeBasis.NetExport));

        await vm.AddRateCommand.ExecuteAsync(null);

        Assert.False(((TariffRateEditorViewModel)_kit.Dialogs.LastEditor!).AllowTiers);
    }

    [Fact]
    public async Task EditingAPrice_ReplacesItAndRemovingAsksForConfirmation()
    {
        var vm = await NewEditorAsync();
        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "1,32");
        await AddRateAsync(vm, new DateOnly(2026, 9, 1), "1,41");
        _kit.Dialogs.EditorScript = editor =>
        {
            ((TariffRateEditorViewModel)editor).PriceText = "1,50";
            editor.SaveCommand.Execute(null);
        };

        vm.Rates[0].EditCommand.Execute(null);
        Assert.Equal(1.5m, vm.Rates[0].Rate.Price);
        Assert.Equal(new DateOnly(2026, 9, 1), vm.Rates[0].Rate.ValidFrom);

        _kit.Dialogs.ConfirmResult = false;
        vm.Rates[1].RemoveCommand.Execute(null);
        Assert.Equal(2, vm.Rates.Count);
        _kit.Dialogs.ConfirmResult = true;
        vm.Rates[1].RemoveCommand.Execute(null);
        Assert.Single(vm.Rates);
        Assert.Contains(_kit.Dialogs.Confirmations, c => c.Title == "Dialog_DeleteRow_Title [en]");
    }

    [Fact]
    public async Task Example_PerUnit_TenCubicMetresAtOnePointFortyOneExclVat()
    {
        var vm = await NewEditorAsync();
        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "1,41");

        var example = vm.Example;

        // 10 m3 * 1.41 EUR = 14.10; VAT 21% = 2.96; total 17.06
        Assert.Equal(10m, example.Quantity);
        Assert.Equal([new PriceSlice(10m, 1.41m)], example.Slices);
        Assert.Equal((14.10m, 21m, 2.96m, 17.06m), (example.Net, example.VatPercent, example.Vat, example.Gross));
        Assert.StartsWith("TariffExampleTotal 10|", vm.ExampleText);
    }

    [Fact]
    public async Task Example_SteppedPrice_ShowsEachStep()
    {
        var vm = await NewEditorAsync();
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (TariffRateEditorViewModel)editor;
            dialog.ValidFrom = new DateOnly(2026, 1, 1);
            dialog.IsStepped = true;
            dialog.TierRows[0].UpToText = "100";
            dialog.TierRows[0].PriceText = "0,1065";
            dialog.TierRows[1].PriceText = "0,0818";
            dialog.SaveCommand.Execute(null);
        };
        await vm.AddRateCommand.ExecuteAsync(null);

        var example = vm.Example;

        // 150 kWh: 100 * 0.1065 = 10.65; 50 * 0.0818 = 4.09; net 14.74 excl. VAT
        Assert.Equal(150m, example.Quantity);
        Assert.Equal([new PriceSlice(100m, 0.1065m), new PriceSlice(50m, 0.0818m)], example.Slices);
        Assert.Equal(14.74m, example.Net);
        Assert.Equal(3, vm.ExampleText.Split('\n').Length);
        Assert.StartsWith("TariffExampleSlice 100|", vm.ExampleText);
    }

    [Fact]
    public async Task Example_PerArea_UsesTheCurrentAreaAndAGrossPrice()
    {
        var vm = await NewEditorAsync();
        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(ChargeBasis.PerArea));
        vm.SelectedVatOption = vm.VatOptions.Single(o => o.IsCustom);
        vm.VatPercentText = "0";
        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "0,6711", includesVat: true);

        var example = vm.Example;

        // 52.3 m2 * 0.6711 EUR = 35.09853, incl. VAT 0% -> 35.10
        Assert.Equal(52.3m, example.Quantity);
        Assert.Equal((35.10m, 0m, 35.10m), (example.Net, example.Vat, example.Gross));
    }

    [Fact]
    public async Task Example_FixedMonthlyGrossPrice_SplitsOutTheVat()
    {
        var vm = await NewEditorAsync();
        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(ChargeBasis.FixedMonthly));
        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "22,95", includesVat: true);

        var example = vm.Example;

        // 22.95 incl. 21% VAT: net 18.97, VAT 3.98
        Assert.Equal(1m, example.Quantity);
        Assert.Equal((18.97m, 3.98m, 22.95m), (example.Net, example.Vat, example.Gross));
    }

    [Fact]
    public async Task Example_FromBill_And_MissingPrice_ExplainWhy()
    {
        var vm = await NewEditorAsync();
        Assert.Equal("TariffExampleNoPrice [en]", vm.ExampleText);

        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(ChargeBasis.FromBill));

        Assert.Equal("TariffExampleFromBill [en]", vm.ExampleText);
        Assert.Null(vm.Example.Quantity);
    }

    [Fact]
    public async Task Example_UsesTheNewestPriceAndFollowsTheVatChoice()
    {
        var vm = await NewEditorAsync();
        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "1,00");
        await AddRateAsync(vm, new DateOnly(2026, 9, 1), "2,00");
        Assert.Equal(20m, vm.Example.Net);

        vm.SelectedVatOption = vm.VatOptions.Single(o => o.Code == "LV-REDUCED");

        Assert.Equal(12m, vm.Example.VatPercent);
        Assert.Equal(2.4m, vm.Example.Vat);
    }

    [Fact]
    public async Task Save_StaysOnThePage_AndNumbersTheComponentsOfTheContract()
    {
        var vm = await FilledAsync();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(0, _kit.Navigation.BackCount);
        Assert.False(vm.IsNew);
        Assert.False(vm.IsDirty);
        Assert.Single(_kit.Toast.Messages);
        Assert.Equal(0, _kit.Tariffs.Saved[^1].SortOrder);
        Assert.Equal("Water supply", vm.Title);

        var second = await FilledAsync();
        second.ComponentName = "Sewerage";
        await second.SaveCommand.ExecuteAsync(null);
        Assert.Equal(1, _kit.Tariffs.Saved[^1].SortOrder);
        Assert.Equal(_contract.Id, _kit.Tariffs.Saved[^1].ContractId);
        Assert.All(_kit.Tariffs.Saved[^1].Rates, r => Assert.Equal(_kit.Tariffs.Saved[^1].Id, r.ComponentId));
    }

    [Fact]
    public async Task Save_ValidationErrorsAppearUnderTheirFields()
    {
        var vm = await NewEditorAsync();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Tariffs.Saved);
        Assert.Equal("Error_tariff_name_required [en]", vm.NameError);
        Assert.Equal("Error_tariff_rate_required [en]", vm.RatesError);
    }

    [Fact]
    public async Task FromBillComponent_NeedsNoPrice()
    {
        var vm = await NewEditorAsync();
        vm.ComponentName = "Heating";
        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(ChargeBasis.FromBill));

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Tariffs.Saved);
        Assert.Empty(saved.Rates);
        Assert.Equal(ChargeBasis.FromBill, saved.Basis);
    }

    [Fact]
    public async Task SwitchingToFromBill_DropsTheHiddenPrices_ButSwitchingBackKeepsThem()
    {
        var vm = await FilledAsync();
        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(ChargeBasis.FromBill));
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Empty(_kit.Tariffs.Saved[^1].Rates);

        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(ChargeBasis.PerUnit));
        Assert.Single(vm.Rates);
    }

    [Fact]
    public async Task ExistingComponent_LoadsEveryChoice()
    {
        var component = new TariffComponent
        {
            ContractId = _contract.Id,
            Name = "Water heating",
            ChargeKind = ServiceKind.WaterHeating,
            Basis = ChargeBasis.PerUnit,
            MeterIds = [_hot.Id],
            VatPercentOverride = 12m,
            Notes = "Hot only",
            Rates = [new TariffRate { ValidFrom = new DateOnly(2026, 1, 1), Price = 4.56m, Currency = Currency.EUR }],
        };
        _kit.Tariffs.Stored.Add(component);
        var vm = _kit.CreateTariffEditor();

        await vm.LoadAsync(component.Id);

        Assert.False(vm.IsNew);
        Assert.Equal("Water heating", vm.Title);
        Assert.Equal(ServiceKind.WaterHeating, vm.SelectedService!.Value);
        Assert.Equal("Hot only", vm.Notes);
        Assert.True(vm.IsCustomVat);
        Assert.Equal("12", vm.VatPercentText);
        Assert.Equal([false, true], vm.Meters.OrderBy(m => m.MeterId == _hot.Id).Select(m => m.IsOn));
        Assert.Equal(4.56m, Assert.Single(vm.Rates).Rate.Price);
        Assert.False(vm.IsDirty);

        // Nothing changed: saving writes back the same choices.
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal([_hot.Id], _kit.Tariffs.Saved[^1].MeterIds);
        Assert.Equal(12m, _kit.Tariffs.Saved[^1].VatPercentOverride);
    }

    [Fact]
    public async Task StoredRateWithAProblem_MarksItsRowOnSave()
    {
        var component = new TariffComponent
        {
            ContractId = _contract.Id,
            Name = "Odd",
            VatCode = "ZERO",
            Rates =
            [
                new TariffRate { ValidFrom = new DateOnly(2026, 1, 1), Price = 1m },
                new TariffRate { ValidFrom = new DateOnly(2026, 6, 1), Price = -2m },
            ],
        };
        _kit.Tariffs.Stored.Add(component);
        var vm = _kit.CreateTariffEditor();
        await vm.LoadAsync(component.Id);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Tariffs.Saved);
        var broken = vm.Rates.Single(r => r.Rate.Price == -2m);
        Assert.Equal("Error_tariff_price_negative [en]", broken.ErrorText);
        Assert.True(broken.HasError);
        Assert.All(vm.Rates.Where(r => r != broken), r => Assert.False(r.HasError));
    }

    [Fact]
    public async Task CategoryOverride_CanBeChosenAndReset()
    {
        var vm = await FilledAsync();
        _kit.Dialogs.ChooseScript = options =>
        {
            Assert.Equal("TariffCategorySameAsContract [en]", options[0]);
            return "Enum_ServiceKind_Sewerage [en]";
        };

        await vm.ChooseCategoryCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal(_kit.CategoryIdFor(ServiceKind.Sewerage), _kit.Tariffs.Saved[^1].CategoryId);

        _kit.Dialogs.ChooseScript = options => options[0];
        await vm.ChooseCategoryCommand.ExecuteAsync(null);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Null(_kit.Tariffs.Saved[^1].CategoryId);
    }

    [Fact]
    public async Task CanLeave_AsksOnlyWhileDirty()
    {
        var vm = await NewEditorAsync();
        Assert.True(await vm.CanLeaveAsync());
        Assert.Equal(0, _kit.Dialogs.UnsavedAsked);

        vm.ComponentName = "Water supply";
        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;
        Assert.False(await vm.CanLeaveAsync());
        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.DontSave;
        Assert.True(await vm.CanLeaveAsync());
        Assert.Empty(_kit.Tariffs.Saved);

        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;
        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "1,41");
        Assert.True(await vm.CanLeaveAsync());
        Assert.Single(_kit.Tariffs.Saved);
    }

    [Fact]
    public async Task Back_GoesBackAndDeleteRemovesTheComponent()
    {
        var vm = await FilledAsync();
        await vm.SaveCommand.ExecuteAsync(null);
        var id = vm.EntityId;

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal([id], _kit.Tariffs.Deleted);
        Assert.Equal(1, _kit.Navigation.BackCount);
    }

    [Fact]
    public async Task Help_OpensTheTariffsSection()
    {
        var vm = await NewEditorAsync();

        await vm.OpenHelpCommand.ExecuteAsync(null);

        var visit = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.HelpSection, visit.Route);
        Assert.Equal(HelpTopic.Tariffs, visit.Parameters![HelpNavigation.TopicParameter]);
    }

    [Fact]
    public async Task ChangingTheLanguage_RefreshesTextsAndKeepsTheInput()
    {
        var vm = await FilledAsync();
        var vatCode = vm.SelectedVatOption!.Code;

        _kit.Language.SetLanguage("ru");

        Assert.Equal("TariffGeneral [ru]", vm.GeneralHeader);
        Assert.Equal("Water supply", vm.ComponentName);
        Assert.Equal(vatCode, vm.SelectedVatOption!.Code);
        Assert.Equal(ChargeBasis.PerUnit, vm.Basis);
        Assert.Single(vm.Rates);
        Assert.Equal(2, vm.Meters.Count);
    }

    private Meter AddMeter(MeterResource resource, MeterUnit unit, string location, params RegisterKind[] kinds)
    {
        var meter = new Meter { PropertyId = _kit.Property.Id, Resource = resource, Unit = unit, Location = location, InstalledOn = new DateOnly(2020, 1, 1) };
        foreach (var kind in kinds)
        {
            meter.Registers.Add(new MeterRegister
            {
                MeterId = meter.Id,
                Kind = kind,
                Direction = kind == RegisterKind.Export ? EnergyDirection.Export : EnergyDirection.Import,
            });
        }

        _kit.Meters.Stored.Add(meter);
        if (resource is MeterResource.ColdWater or MeterResource.HotWater)
        {
            _kit.Contracts.Links.Add(new ContractMeter { ContractId = _contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        }

        return meter;
    }

    private async Task<TariffComponentEditViewModel> NewEditorAsync()
    {
        var vm = _kit.CreateTariffEditor();
        vm.ContractId = _contract.Id;
        vm.IsHostedInPage = true;
        await vm.LoadAsync(null);
        return vm;
    }

    /// <summary>A new, valid "Water supply" component with one price.</summary>
    private async Task<TariffComponentEditViewModel> FilledAsync(ChargeBasis basis = ChargeBasis.PerUnit)
    {
        var vm = await NewEditorAsync();
        vm.ComponentName = "Water supply";
        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(basis));
        await AddRateAsync(vm, new DateOnly(2026, 1, 1), "1,41");
        return vm;
    }

    private async Task AddRateAsync(TariffComponentEditViewModel vm, DateOnly validFrom, string price, bool includesVat = false)
    {
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (TariffRateEditorViewModel)editor;
            dialog.ValidFrom = validFrom;
            dialog.PriceText = price;
            dialog.PriceIncludesVat = includesVat;
            dialog.SaveCommand.Execute(null);
        };
        await vm.AddRateCommand.ExecuteAsync(null);
        _kit.Dialogs.EditorScript = null;
    }
}
