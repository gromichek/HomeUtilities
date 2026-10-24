using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

public class ReadingSubmitLinkTests
{
    private static readonly DateOnly Today = new(2026, 10, 24);

    private readonly ContractTestKit _kit = new(Today);

    private Meter AddMeter(string location, MeterResource resource = MeterResource.ColdWater)
    {
        var meter = new Meter
        {
            PropertyId = _kit.Property.Id,
            Resource = resource,
            Unit = MeterUnit.M3,
            Location = location,
            InstalledOn = new DateOnly(2020, 1, 1),
        };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        var previous = new MeterReading { RegisterId = meter.Registers[0].Id, Date = new DateOnly(2026, 9, 26), Value = 100m, Kind = ReadingKind.Regular };
        meter.Registers[0].Readings.Add(previous);
        _kit.Meters.Stored.Add(meter);
        return meter;
    }

    private Contract AddContract(string title, string supplierName, IEnumerable<Link> supplierLinks, IEnumerable<Link> contractLinks, params Meter[] meters)
    {
        var supplier = new Supplier { Name = supplierName };
        foreach (var link in supplierLinks)
        {
            supplier.Links.Add(link);
        }

        _kit.Suppliers.Stored.Add(supplier);
        var contract = new Contract
        {
            PropertyId = _kit.Property.Id,
            SupplierId = supplier.Id,
            Title = title,
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        foreach (var link in contractLinks)
        {
            contract.Links.Add(link);
        }

        _kit.Contracts.Stored.Add(contract);
        foreach (var meter in meters)
        {
            _kit.Contracts.Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        }

        return contract;
    }

    private static Link Submit(string url) => new() { Kind = LinkKind.SubmitReadings, Url = url };

    private static Link Portal(string url) => new() { Kind = LinkKind.CustomerPortal, Url = url };

    private async Task<ReadingsViewModel> LoadAsync()
    {
        var vm = _kit.CreateReadings();
        await vm.ReloadAsync();
        return vm;
    }

    private static MeterCardViewModel Card(ReadingsViewModel vm, string title) =>
        vm.Groups.SelectMany(g => g.Cards).Single(c => c.Title == title);

    [Fact]
    public async Task One_button_per_distinct_page_of_the_linked_contracts()
    {
        var hot = AddMeter("Bathroom", MeterResource.HotWater);
        AddContract("Water", "Water utility", [Submit("https://water.example/readings")], [], hot);
        AddContract("Heating", "Heat supplier", [Submit("https://heat.example/readings")], [], hot);

        var vm = await LoadAsync();

        var links = Card(vm, "Bathroom").SubmitLinks;
        Assert.Equal(2, links.Count);
        Assert.Equal(["https://water.example/readings", "https://heat.example/readings"], links.Select(l => l.Uri.AbsoluteUri));
        Assert.Contains("Water utility", links[0].Text);
        Assert.Contains("Heat supplier", links[1].Text);
        Assert.True(Card(vm, "Bathroom").HasSubmitLinks);
    }

    [Fact]
    public async Task Submit_readings_page_is_preferred_over_the_customer_portal()
    {
        var meter = AddMeter("Kitchen");
        AddContract("Water", "Water utility", [Portal("https://water.example/portal"), Submit("https://water.example/readings")], [], meter);

        var vm = await LoadAsync();

        Assert.Equal("https://water.example/readings", Assert.Single(Card(vm, "Kitchen").SubmitLinks).Uri.AbsoluteUri);
    }

    [Fact]
    public async Task Customer_portal_is_used_when_there_is_no_submit_page()
    {
        var meter = AddMeter("Kitchen");
        AddContract("Water", "Water utility", [Portal("https://water.example/portal")], [], meter);

        var vm = await LoadAsync();

        Assert.Equal("https://water.example/portal", Assert.Single(Card(vm, "Kitchen").SubmitLinks).Uri.AbsoluteUri);
    }

    [Fact]
    public async Task The_contract_link_overrides_the_supplier_link()
    {
        var meter = AddMeter("Kitchen");
        AddContract("Water", "Water utility", [Submit("https://supplier.example/readings")], [Submit("https://contract.example/readings")], meter);

        var vm = await LoadAsync();

        Assert.Equal("https://contract.example/readings", Assert.Single(Card(vm, "Kitchen").SubmitLinks).Uri.AbsoluteUri);
    }

    [Fact]
    public async Task Contracts_with_the_same_page_share_one_button()
    {
        var hot = AddMeter("Bathroom", MeterResource.HotWater);
        var water = AddContract("Water", "Municipal services", [Submit("https://city.example/readings")], [], hot);
        var heat = AddContract("Heating", "Municipal services", [Submit("https://city.example/readings")], [], hot);

        var vm = await LoadAsync();

        var link = Assert.Single(Card(vm, "Bathroom").SubmitLinks);
        Assert.Equal(2, link.ContractIds.Count);
        Assert.Contains(water.Id, link.ContractIds);
        Assert.Contains(heat.Id, link.ContractIds);
        Assert.Contains("Municipal services", link.Text);
        Assert.DoesNotContain(",", link.Text);
    }

    [Fact]
    public async Task No_button_without_links_or_without_an_active_contract_link()
    {
        var plain = AddMeter("Kitchen");
        var ended = AddMeter("Bathroom");
        var unlinked = AddMeter("Garage");
        AddContract("Plain", "Water utility", [], [], plain);
        var old = AddContract("Old", "Old supplier", [Submit("https://old.example/readings")], [], ended);
        _kit.Contracts.Links.Single(l => l.ContractId == old.Id).ValidTo = new DateOnly(2026, 1, 1);
        AddContract("Elsewhere", "Other", [Submit("https://other.example/readings")], [], unlinked);
        _kit.Contracts.Links.RemoveAll(l => l.MeterId == unlinked.Id);

        var vm = await LoadAsync();

        Assert.All(vm.Groups.SelectMany(g => g.Cards), c => Assert.False(c.HasSubmitLinks));
    }

    [Fact]
    public async Task A_link_that_is_not_a_valid_address_gives_no_button()
    {
        var meter = AddMeter("Kitchen");
        AddContract("Water", "Water utility", [Submit("javascript:alert(1)")], [], meter);

        var vm = await LoadAsync();

        Assert.Empty(Card(vm, "Kitchen").SubmitLinks);
    }

    [Fact]
    public async Task Tapping_the_button_opens_the_page_in_the_browser()
    {
        var meter = AddMeter("Kitchen");
        AddContract("Water", "Water utility", [Submit("https://water.example/readings")], [], meter);
        var vm = await LoadAsync();

        await Card(vm, "Kitchen").SubmitLinks[0].Command.ExecuteAsync(null);

        Assert.Equal(["https://water.example/readings"], _kit.Launcher.Opened.Select(u => u.AbsoluteUri));
    }

    private async Task<(ReadingsViewModel Vm, Contract Contract)> SavedReadingWithLinkAsync()
    {
        var meter = AddMeter("Kitchen");
        var contract = AddContract("Water", "Water utility", [Submit("https://water.example/readings")], [], meter);
        var vm = await LoadAsync();
        Card(vm, "Kitchen").Rows[0].ValueText = "110";
        await vm.SaveAllCommand.ExecuteAsync(null);
        return (vm, contract);
    }

    [Fact]
    public async Task Coming_back_after_a_save_asks_once_and_marks_the_readings_on_yes()
    {
        var (vm, _) = await SavedReadingWithLinkAsync();
        await Card(vm, "Kitchen").SubmitLinks[0].Command.ExecuteAsync(null);

        await vm.OnAppearingAsync();

        var asked = Assert.Single(_kit.Dialogs.Confirmations);
        Assert.Contains("Water utility", asked.Message);
        var update = Assert.Single(Assert.Single(_kit.Meters.UpdatedBatches));
        Assert.Equal((110m, Today), (update.Value, update.SubmittedOn));
        Assert.Empty(vm.Prompts);

        await vm.OnAppearingAsync();
        await vm.CheckReturnFromSubmitPageAsync();
        Assert.Single(_kit.Dialogs.Confirmations);
    }

    [Fact]
    public async Task Answering_no_leaves_the_readings_unmarked_and_does_not_ask_again()
    {
        var (vm, _) = await SavedReadingWithLinkAsync();
        _kit.Dialogs.ConfirmResult = false;
        await Card(vm, "Kitchen").SubmitLinks[0].Command.ExecuteAsync(null);

        await vm.OnAppearingAsync();
        await vm.OnAppearingAsync();

        Assert.Single(_kit.Dialogs.Confirmations);
        Assert.Empty(_kit.Meters.UpdatedBatches);
    }

    [Fact]
    public async Task No_question_when_the_page_was_not_opened_from_a_card()
    {
        var (vm, _) = await SavedReadingWithLinkAsync();

        await vm.OnAppearingAsync();

        Assert.Empty(_kit.Dialogs.Confirmations);
    }

    [Fact]
    public async Task No_question_when_nothing_was_saved_in_this_session()
    {
        var meter = AddMeter("Kitchen");
        AddContract("Water", "Water utility", [Submit("https://water.example/readings")], [], meter);
        var vm = await LoadAsync();
        await Card(vm, "Kitchen").SubmitLinks[0].Command.ExecuteAsync(null);

        await vm.OnAppearingAsync();

        Assert.Empty(_kit.Dialogs.Confirmations);
    }

    [Fact]
    public async Task No_question_when_the_readings_were_already_marked_as_submitted()
    {
        var (vm, _) = await SavedReadingWithLinkAsync();
        await vm.Prompts[0].MarkCommand.ExecuteAsync(null);
        await Card(vm, "Kitchen").SubmitLinks[0].Command.ExecuteAsync(null);

        await vm.OnAppearingAsync();

        Assert.Empty(_kit.Dialogs.Confirmations);
    }
}
