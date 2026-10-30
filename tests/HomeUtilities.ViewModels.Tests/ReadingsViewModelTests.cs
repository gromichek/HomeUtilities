using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

public class ReadingsViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    private readonly ContractTestKit _kit = new(Today);

    private Meter AddMeter(string location, MeterResource resource = MeterResource.ColdWater, int? digits = null, params RegisterKind[] kinds)
    {
        var meter = new Meter
        {
            PropertyId = _kit.Property.Id,
            Resource = resource,
            Unit = resource == MeterResource.Electricity ? MeterUnit.KWh : MeterUnit.M3,
            Location = location,
            Digits = digits,
            InstalledOn = new DateOnly(2020, 1, 1),
        };
        foreach (var kind in kinds.Length == 0 ? [RegisterKind.Total] : kinds)
        {
            meter.Registers.Add(new MeterRegister { MeterId = meter.Id, Kind = kind, Direction = EnergyDirection.Import });
        }

        _kit.Meters.Stored.Add(meter);
        return meter;
    }

    private static MeterReading Read(Meter meter, DateOnly date, decimal value, RegisterKind kind = RegisterKind.Total, ReadingKind readingKind = ReadingKind.Regular)
    {
        var register = meter.Registers.Single(r => r.Kind == kind);
        var reading = new MeterReading { RegisterId = register.Id, Date = date, Value = value, Kind = readingKind };
        register.Readings.Add(reading);
        return reading;
    }

    private Contract AddContract(string title, int? from, int? to, Supplier? supplier, params Meter[] meters)
    {
        supplier ??= new Supplier { Name = "Water utility" };
        if (_kit.Suppliers.Stored.All(s => s.Id != supplier.Id))
        {
            _kit.Suppliers.Stored.Add(supplier);
        }

        var contract = new Contract
        {
            PropertyId = _kit.Property.Id,
            SupplierId = supplier.Id,
            Title = title,
            ValidFrom = new DateOnly(2020, 1, 1),
            ReadingDayFrom = from,
            ReadingDayTo = to,
        };
        _kit.Contracts.Stored.Add(contract);
        foreach (var meter in meters)
        {
            _kit.Contracts.Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        }

        return contract;
    }

    private static Link SubmitLink(string url) => new() { Kind = LinkKind.SubmitReadings, Url = url };

    private async Task<ReadingsViewModel> LoadAsync()
    {
        var vm = _kit.CreateReadings();
        await vm.ReloadAsync();
        return vm;
    }

    private static MeterCardViewModel Card(ReadingsViewModel vm, string title) =>
        vm.Groups.SelectMany(g => g.Cards).Single(c => c.Title == title);

    [Fact]
    public async Task Cards_are_grouped_by_resource_and_ordered_by_location_with_a_row_per_register()
    {
        AddMeter("Kitchen", MeterResource.HotWater);
        AddMeter("Bathroom", MeterResource.ColdWater);
        AddMeter("Kitchen", MeterResource.ColdWater);
        AddMeter("Main", MeterResource.Electricity, null, RegisterKind.Day, RegisterKind.Night);
        var removed = AddMeter("Old", MeterResource.ColdWater);
        removed.RemovedOn = new DateOnly(2026, 1, 1);

        var vm = await LoadAsync();

        Assert.Equal(
            ["Enum_MeterResource_ColdWater", "Enum_MeterResource_HotWater", "Enum_MeterResource_Electricity"],
            vm.Groups.Select(g => g.Header.Split(' ')[0]));
        Assert.Equal(["Bathroom", "Kitchen"], vm.Groups[0].Cards.Select(c => c.Title));
        var electricity = Card(vm, "Main");
        Assert.Equal(["Enum_RegisterKind_Day [en]", "Enum_RegisterKind_Night [en]"], electricity.Rows.Select(r => r.Label));
        Assert.All(Card(vm, "Bathroom").Rows, r => Assert.False(r.HasLabel));
    }

    [Fact]
    public async Task Single_property_has_no_picker_and_two_properties_do()
    {
        var vm = await LoadAsync();
        Assert.False(vm.HasMultipleProperties);

        _kit.Properties.Stored.Add(new Property { Name = "Cottage" });
        await vm.ReloadAsync();
        Assert.True(vm.HasMultipleProperties);
    }

    [Fact]
    public async Task Property_selection_is_remembered()
    {
        var second = new Property { Name = "Cottage", SortOrder = 5 };
        _kit.Properties.Stored.Add(second);
        var vm = await LoadAsync();
        Assert.Equal(_kit.Property.Id, vm.SelectedPropertyId);

        vm.SelectedPropertyIndex = 1;
        await vm.PendingPropertyChange;

        Assert.Equal(second.Id, vm.SelectedPropertyId);
        Assert.Equal(second.Id.ToString(), _kit.Settings.Values[ReadingsViewModel.LastPropertyKey]);

        var again = await LoadAsync();
        Assert.Equal(second.Id, again.SelectedPropertyId);
        Assert.Equal(1, again.SelectedPropertyIndex);
    }

    [Fact]
    public async Task OpenFor_SelectsThatPropertyAndDate_AndRemembersTheProperty()
    {
        var second = new Property { Name = "Cottage", SortOrder = 5 };
        _kit.Properties.Stored.Add(second);
        var vm = _kit.CreateReadings();

        vm.OpenFor(second.Id, new DateOnly(2026, 9, 30));
        await vm.ReloadAsync();

        Assert.Equal(second.Id, vm.SelectedPropertyId);
        Assert.Equal(new DateOnly(2026, 9, 30), vm.ReadingDate);
        Assert.Equal(second.Id.ToString(), _kit.Settings.Values[ReadingsViewModel.LastPropertyKey]);

        // only the first load follows the request; a later reload keeps the user's current choice
        await vm.ReloadAsync();
        Assert.Equal(second.Id, vm.SelectedPropertyId);
    }

    [Theory]
    [InlineData(2026, 10, 23, ReadingWindowState.Upcoming, "ReadingsWindowOpens ", "|2 [")]
    [InlineData(2026, 10, 24, ReadingWindowState.Upcoming, "ReadingsWindowOpensTomorrow ", "")]
    [InlineData(2026, 10, 25, ReadingWindowState.Open, "ReadingsWindowOpen ", "")]
    [InlineData(2026, 10, 26, ReadingWindowState.Open, "ReadingsWindowOpen ", "")]
    [InlineData(2026, 10, 28, ReadingWindowState.Open, "ReadingsWindowOpen ", "")]
    [InlineData(2026, 10, 29, ReadingWindowState.Closed, "ReadingsWindowClosed ", "|25.11 [")]
    public async Task Window_banner_follows_today(int year, int month, int day, ReadingWindowState state, string textStart, string textPart)
    {
        var meter = AddMeter("Kitchen");
        AddContract("Water", 25, 28, null, meter);
        _kit.Clock.Today = new DateOnly(year, month, day);

        var vm = await LoadAsync();

        var banner = Assert.Single(vm.Banners);
        Assert.Equal(state, banner.State);
        Assert.StartsWith(textStart, banner.Text);
        Assert.Contains(textPart, banner.Text);
        Assert.Contains("Water|", banner.Text);
    }

    [Fact]
    public async Task Window_across_the_month_end_is_open_in_the_next_month()
    {
        var meter = AddMeter("Kitchen");
        AddContract("Water", 28, 3, null, meter);
        _kit.Clock.Today = new DateOnly(2026, 11, 2);

        var vm = await LoadAsync();

        Assert.Equal(ReadingWindowState.Open, Assert.Single(vm.Banners).State);
    }

    [Fact]
    public async Task Contracts_without_window_or_without_meters_have_no_banner()
    {
        var meter = AddMeter("Kitchen");
        AddContract("No window", null, null, null, meter);
        AddContract("No meters", 25, 28, null);

        var vm = await LoadAsync();

        Assert.Empty(vm.Banners);
    }

    [Fact]
    public async Task Submit_button_opens_the_contract_link_which_overrides_the_supplier_link()
    {
        var meter = AddMeter("Kitchen");
        var supplier = new Supplier { Name = "Water utility", Links = { SubmitLink("https://supplier.example/readings") } };
        var contract = AddContract("Water", 25, 28, supplier, meter);
        contract.Links.Add(SubmitLink("https://contract.example/readings"));

        var vm = await LoadAsync();
        await vm.Banners[0].SubmitCommand!.ExecuteAsync(null);

        Assert.Equal([new Uri("https://contract.example/readings")], _kit.Launcher.Opened);
    }

    [Fact]
    public async Task Submit_button_uses_the_supplier_link_when_the_contract_has_none_and_is_absent_without_any()
    {
        var meter = AddMeter("Kitchen");
        var supplier = new Supplier { Name = "Water utility", Links = { SubmitLink("https://supplier.example/readings") } };
        AddContract("Water", 25, 28, supplier, meter);
        AddContract("Other", 25, 28, new Supplier { Name = "Other" }, meter);

        var vm = await LoadAsync();

        var withLink = vm.Banners.Single(b => b.Text.Contains("Water|"));
        await withLink.SubmitCommand!.ExecuteAsync(null);
        Assert.Equal([new Uri("https://supplier.example/readings")], _kit.Launcher.Opened);
        Assert.False(vm.Banners.Single(b => b.Text.Contains("Other|")).HasSubmitLink);
    }

    [Fact]
    public async Task Submit_button_with_an_invalid_link_shows_the_error_instead_of_opening()
    {
        var meter = AddMeter("Kitchen");
        var contract = AddContract("Water", 25, 28, null, meter);
        contract.Links.Add(SubmitLink("javascript:alert(1)"));

        var vm = await LoadAsync();
        await vm.Banners[0].SubmitCommand!.ExecuteAsync(null);

        Assert.Empty(_kit.Launcher.Opened);
        Assert.Single(_kit.Dialogs.Alerts);
    }

    [Fact]
    public async Task Preview_shows_the_consumption_since_the_last_reading()
    {
        var meter = AddMeter("Kitchen");
        Read(meter, new DateOnly(2026, 9, 26), 437.097m);
        var vm = await LoadAsync();
        var row = Card(vm, "Kitchen").Rows[0];

        Assert.Contains("437", row.LastText);
        row.ValueText = "445,000";

        Assert.Equal(7.903m, row.Consumption);
        Assert.Contains("7", row.PreviewText);
        Assert.Contains("Enum_MeterUnit_M3", row.PreviewText);
        Assert.False(row.HasError);
        Assert.False(row.HasWarning);
    }

    [Fact]
    public async Task Preview_follows_the_reading_date()
    {
        var meter = AddMeter("Kitchen");
        Read(meter, new DateOnly(2026, 9, 26), 100m);
        var vm = await LoadAsync();
        var row = Card(vm, "Kitchen").Rows[0];
        row.ValueText = "110";
        Assert.Equal(10m, row.Consumption);

        vm.ReadingDate = new DateOnly(2026, 9, 26);

        Assert.True(row.HasError);
        Assert.Null(row.Consumption);
    }

    [Fact]
    public async Task First_reading_of_a_register_has_no_preview()
    {
        AddMeter("Kitchen");
        var vm = await LoadAsync();
        var row = Card(vm, "Kitchen").Rows[0];

        row.ValueText = "12";

        Assert.False(row.HasPreview);
        Assert.False(row.HasError);
        Assert.Contains("ReadingsNoLast", row.LastText);
    }

    [Fact]
    public async Task Lower_value_without_digits_is_an_error()
    {
        var meter = AddMeter("Kitchen");
        Read(meter, new DateOnly(2026, 9, 26), 437.097m);
        var row = Card(await LoadAsync(), "Kitchen").Rows[0];

        row.ValueText = "400";

        Assert.True(row.HasError);
        Assert.Contains("ReadingsErrLower", row.ErrorText);
        Assert.Null(row.Consumption);
    }

    [Fact]
    public async Task Lower_value_with_digits_assumes_a_rollover_and_warns()
    {
        var meter = AddMeter("Kitchen", digits: 5);
        Read(meter, new DateOnly(2026, 9, 26), 99990m);
        var row = Card(await LoadAsync(), "Kitchen").Rows[0];

        row.ValueText = "5";

        // 5 + 10^5 - 99990 = 15
        Assert.Equal(15m, row.Consumption);
        Assert.False(row.HasError);
        Assert.Contains(ConsumptionWarningCodes.RolloverAssumed, row.WarningCodes);
        Assert.Contains("ReadingsWarnRollover", row.WarningText);
    }

    [Fact]
    public async Task Unusually_high_consumption_warns_against_the_monthly_history()
    {
        var meter = AddMeter("Kitchen");
        Read(meter, new DateOnly(2026, 6, 30), 100m);
        Read(meter, new DateOnly(2026, 7, 31), 109m);
        Read(meter, new DateOnly(2026, 8, 31), 119m);
        Read(meter, new DateOnly(2026, 9, 30), 127m);
        var row = Card(await LoadAsync(), "Kitchen").Rows[0];

        // history Jul 9, Aug 10, Sep 8 -> average 9; limit 27
        row.ValueText = "167";
        Assert.Equal(40m, row.Consumption);
        Assert.Contains(ConsumptionWarningCodes.UnusuallyHigh, row.WarningCodes);
        Assert.Contains("ReadingsWarnHigh", row.WarningText);

        row.ValueText = "150";
        Assert.Equal(23m, row.Consumption);
        Assert.Empty(row.WarningCodes);
    }

    [Fact]
    public async Task Zero_consumption_warns()
    {
        var meter = AddMeter("Kitchen");
        Read(meter, new DateOnly(2026, 7, 31), 109m);
        Read(meter, new DateOnly(2026, 8, 31), 119m);
        Read(meter, new DateOnly(2026, 9, 30), 127m);
        var row = Card(await LoadAsync(), "Kitchen").Rows[0];

        row.ValueText = "127";

        Assert.Equal(0m, row.Consumption);
        Assert.Contains(ConsumptionWarningCodes.ZeroConsumption, row.WarningCodes);
    }

    [Fact]
    public async Task Nonsense_text_is_an_error_not_a_crash()
    {
        var meter = AddMeter("Kitchen");
        Read(meter, new DateOnly(2026, 9, 26), 100m);
        var row = Card(await LoadAsync(), "Kitchen").Rows[0];

        row.ValueText = "abc";

        Assert.True(row.HasError);
        Assert.Null(row.Consumption);
    }

    [Fact]
    public async Task Dual_tariff_meter_previews_each_register_on_its_own()
    {
        var meter = AddMeter("Main", MeterResource.Electricity, null, RegisterKind.Day, RegisterKind.Night);
        Read(meter, new DateOnly(2026, 9, 26), 1000m, RegisterKind.Day);
        Read(meter, new DateOnly(2026, 9, 26), 500m, RegisterKind.Night);
        var rows = Card(await LoadAsync(), "Main").Rows;

        rows[0].ValueText = "1120";
        rows[1].ValueText = "560";

        Assert.Equal([120m, 60m], rows.Select(r => r.Consumption!.Value));
    }

    [Fact]
    public async Task Save_all_stores_only_the_filled_rows_in_one_call_and_clears_them()
    {
        var kitchen = AddMeter("Kitchen");
        var bathroom = AddMeter("Bathroom");
        var main = AddMeter("Main", MeterResource.Electricity, null, RegisterKind.Day, RegisterKind.Night);
        Read(kitchen, new DateOnly(2026, 9, 26), 437.097m);
        Read(bathroom, new DateOnly(2026, 9, 26), 100m);
        Read(main, new DateOnly(2026, 9, 26), 1000m, RegisterKind.Day);
        Read(main, new DateOnly(2026, 9, 26), 500m, RegisterKind.Night);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "445,000";
        Card(vm, "Kitchen").Rows[0].NoteText = " after the repair ";
        Card(vm, "Main").Rows[0].ValueText = "1120";
        Card(vm, "Main").Rows[1].ValueText = "560";

        await vm.SaveAllCommand.ExecuteAsync(null);

        var batch = Assert.Single(_kit.Meters.AddedBatches);
        Assert.Equal(3, batch.Count);
        Assert.All(batch, b => Assert.Equal((Today, ReadingKind.Regular), (b.Reading.Date, b.Reading.Kind)));
        var kitchenReading = batch.Single(b => b.RegisterId == kitchen.Registers[0].Id).Reading;
        Assert.Equal((445.000m, "after the repair"), (kitchenReading.Value, kitchenReading.Note));
        Assert.DoesNotContain(batch, b => b.RegisterId == bathroom.Registers[0].Id);

        // rows cleared, "last" updated, toast shown, still on the page
        var kitchenRow = Card(vm, "Kitchen").Rows[0];
        Assert.False(kitchenRow.HasInput);
        Assert.Equal(string.Empty, kitchenRow.NoteText);
        Assert.Contains("445", kitchenRow.LastText);
        Assert.False(vm.HasUnsavedValues);
        Assert.Equal(["ReadingsSaved 3 [en]"], _kit.Toast.Messages);
        Assert.Equal(0, _kit.Navigation.BackCount);
    }

    [Fact]
    public async Task Save_all_with_an_invalid_row_stores_nothing_and_keeps_the_values()
    {
        var kitchen = AddMeter("Kitchen");
        var bathroom = AddMeter("Bathroom");
        Read(kitchen, new DateOnly(2026, 9, 26), 437.097m);
        Read(bathroom, new DateOnly(2026, 9, 26), 100m);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "445";
        Card(vm, "Bathroom").Rows[0].ValueText = "50";

        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Meters.AddedBatches);
        Assert.True(Card(vm, "Bathroom").Rows[0].HasError);
        Assert.Equal("445", Card(vm, "Kitchen").Rows[0].ValueText);
        Assert.Empty(_kit.Toast.Messages);
    }

    [Fact]
    public async Task Save_all_without_values_only_tells_so()
    {
        AddMeter("Kitchen");
        var vm = await LoadAsync();

        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Meters.AddedBatches);
        Assert.Equal(["ReadingsNothingToSave [en]"], _kit.Toast.Messages);
    }

    [Fact]
    public async Task Save_all_rejects_a_second_reading_of_the_same_kind_on_the_same_date()
    {
        var kitchen = AddMeter("Kitchen");
        Read(kitchen, Today, 100m);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "110";

        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Meters.AddedBatches);
        Assert.Contains("Error_meter_reading_duplicate_on_date", Card(vm, "Kitchen").Rows[0].ErrorText);
    }

    [Fact]
    public async Task Typed_values_survive_a_reload()
    {
        var kitchen = AddMeter("Kitchen");
        Read(kitchen, new DateOnly(2026, 9, 26), 100m);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "110";
        Card(vm, "Kitchen").Rows[0].NoteText = "check";

        await vm.ReloadAsync();

        var row = Card(vm, "Kitchen").Rows[0];
        Assert.Equal(("110", "check"), (row.ValueText, row.NoteText));
        Assert.Equal(10m, row.Consumption);
        Assert.True(vm.HasUnsavedValues);
    }

    [Fact]
    public async Task Mark_submitted_sets_the_date_only_on_the_readings_of_that_contracts_meters()
    {
        var water = AddMeter("Kitchen");
        var power = AddMeter("Main", MeterResource.Electricity);
        var untouched = AddMeter("Bathroom");
        Read(water, new DateOnly(2026, 9, 26), 100m);
        Read(power, new DateOnly(2026, 9, 26), 1000m);
        Read(untouched, new DateOnly(2026, 9, 26), 50m);
        var waterSupplier = new Supplier { Name = "Water utility", Links = { SubmitLink("https://water.example/readings") } };
        AddContract("Water", 25, 28, waterSupplier, water, untouched);
        AddContract("Electricity", null, null, new Supplier { Name = "Power company", Links = { SubmitLink("https://power.example/readings") } }, power);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "110";
        Card(vm, "Main").Rows[0].ValueText = "1100";

        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.Equal(2, vm.Prompts.Count);
        var prompt = vm.Prompts.Single(p => p.Text.Contains("Water utility"));
        await prompt.MarkCommand.ExecuteAsync(null);

        var updated = Assert.Single(_kit.Meters.UpdatedBatches);
        var reading = Assert.Single(updated);
        Assert.Equal((water.Registers[0].Id, Today), (reading.RegisterId, reading.SubmittedOn));
        Assert.Equal(Today, _kit.Meters.ReadingsOf(water.Registers[0].Id).Single(r => r.Value == 110m).SubmittedOn);
        Assert.Null(_kit.Meters.ReadingsOf(power.Registers[0].Id).Single(r => r.Value == 1100m).SubmittedOn);
        Assert.Null(_kit.Meters.ReadingsOf(untouched.Registers[0].Id).Single().SubmittedOn);
        Assert.Contains("Power company", Assert.Single(vm.Prompts).Text);
    }

    [Fact]
    public async Task No_prompt_when_the_saved_meters_belong_to_no_contract_with_a_window_or_link()
    {
        var kitchen = AddMeter("Kitchen");
        Read(kitchen, new DateOnly(2026, 9, 26), 100m);
        AddContract("Plain", null, null, null, kitchen);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "110";

        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.Empty(vm.Prompts);
    }

    [Fact]
    public async Task Dismissing_a_prompt_removes_it_without_touching_the_readings()
    {
        var kitchen = AddMeter("Kitchen");
        Read(kitchen, new DateOnly(2026, 9, 26), 100m);
        AddContract("Water", 25, 28, null, kitchen);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "110";
        await vm.SaveAllCommand.ExecuteAsync(null);

        vm.Prompts[0].DismissCommand.Execute(null);

        Assert.Empty(vm.Prompts);
        Assert.Empty(_kit.Meters.UpdatedBatches);
    }

    [Fact]
    public async Task Leaving_without_typed_values_does_not_ask()
    {
        AddMeter("Kitchen");
        var vm = await LoadAsync();

        Assert.True(await vm.CanLeaveAsync());
        Assert.Equal(0, _kit.Dialogs.UnsavedAsked);
    }

    [Fact]
    public async Task Leaving_with_typed_values_asks_and_follows_the_answer()
    {
        var kitchen = AddMeter("Kitchen");
        Read(kitchen, new DateOnly(2026, 9, 26), 100m);
        var vm = await LoadAsync();
        var row = Card(vm, "Kitchen").Rows[0];
        row.ValueText = "110";

        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;
        Assert.False(await vm.CanLeaveAsync());
        Assert.Equal("110", row.ValueText);

        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;
        Assert.True(await vm.CanLeaveAsync());
        Assert.Single(_kit.Meters.AddedBatches);
        Assert.Equal(2, _kit.Dialogs.UnsavedAsked);

        Card(vm, "Kitchen").Rows[0].ValueText = "120";
        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.DontSave;
        Assert.True(await vm.CanLeaveAsync());
        Assert.False(vm.HasUnsavedValues);
        Assert.Single(_kit.Meters.AddedBatches);
    }

    [Fact]
    public async Task Leaving_with_Save_and_an_invalid_value_stays()
    {
        var kitchen = AddMeter("Kitchen");
        Read(kitchen, new DateOnly(2026, 9, 26), 100m);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "50";
        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Save;

        Assert.False(await vm.CanLeaveAsync());
        Assert.Empty(_kit.Meters.AddedBatches);
    }

    [Fact]
    public async Task Switching_property_with_typed_values_asks_first()
    {
        var kitchen = AddMeter("Kitchen");
        Read(kitchen, new DateOnly(2026, 9, 26), 100m);
        var second = new Property { Name = "Cottage", SortOrder = 5 };
        _kit.Properties.Stored.Add(second);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "110";

        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;
        vm.SelectedPropertyIndex = 1;
        await vm.PendingPropertyChange;
        Assert.Equal(_kit.Property.Id, vm.SelectedPropertyId);
        Assert.Equal("110", Card(vm, "Kitchen").Rows[0].ValueText);

        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.DontSave;
        vm.SelectedPropertyIndex = 1;
        await vm.PendingPropertyChange;
        Assert.Equal(second.Id, vm.SelectedPropertyId);
        Assert.Empty(vm.Groups);
        Assert.True(vm.ShowNoMeters);
    }

    // The history builds its rows off the UI thread, so opening it completes a moment after the tap.
    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var i = 0; i < 300 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Tapping_a_card_opens_the_history_page_on_a_phone_and_the_pane_on_a_tablet()
    {
        var kitchen = AddMeter("Kitchen");
        Read(kitchen, new DateOnly(2026, 9, 26), 100m);
        var vm = await LoadAsync();

        Card(vm, "Kitchen").OpenHistoryCommand.Execute(null);
        await WaitAsync(() => _kit.Navigation.Visited.Count > 0);

        var (route, parameters) = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.ReadingHistory, route);
        var history = Assert.IsType<ReadingHistoryViewModel>(parameters!["editor"]);
        Assert.Equal(kitchen.Id, history.MeterId);
        Assert.Null(vm.SelectedHistory);

        _kit.Layout.Change(LayoutMode.Expanded);
        Assert.True(vm.IsTwoPane);
        Assert.True(vm.ShowHistoryHint);
        Card(vm, "Kitchen").OpenHistoryCommand.Execute(null);
        await WaitAsync(() => vm.SelectedHistory is not null);

        Assert.Equal(kitchen.Id, vm.SelectedHistory!.MeterId);
        Assert.True(Card(vm, "Kitchen").IsSelected);
        Assert.Single(_kit.Navigation.Visited);
    }

    [Fact]
    public async Task Changing_a_reading_in_the_history_pane_refreshes_the_last_value()
    {
        var kitchen = AddMeter("Kitchen");
        var reading = Read(kitchen, new DateOnly(2026, 9, 26), 100m);
        _kit.Layout.Change(LayoutMode.Expanded);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").OpenHistoryCommand.Execute(null);
        await WaitAsync(() => vm.SelectedHistory is not null);
        Assert.Contains("100", Card(vm, "Kitchen").Rows[0].LastText);

        _kit.Dialogs.ConfirmResult = true;
        await vm.SelectedHistory!.Groups[0].Rows[0].DeleteCommand.ExecuteAsync(null);

        Assert.Equal([reading.Id], _kit.Meters.DeletedReadings);
        Assert.Contains("ReadingsNoLast", Card(vm, "Kitchen").Rows[0].LastText);
    }

    [Fact]
    public async Task Property_without_meters_and_no_properties_show_their_empty_states()
    {
        var vm = await LoadAsync();
        Assert.True(vm.ShowNoMeters);
        Assert.False(vm.ShowNoProperties);

        _kit.Properties.Stored.Clear();
        await vm.ReloadAsync();
        Assert.True(vm.ShowNoProperties);
        Assert.False(vm.ShowNoMeters);
    }
}
