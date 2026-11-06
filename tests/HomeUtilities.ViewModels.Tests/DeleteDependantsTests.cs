using System.Globalization;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>Delete confirmations list the dependants (UI_GUIDELINES §5.1, T-036c); Undo after the delete where it is exact.</summary>
public sealed class DeleteDependantsTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;
    private readonly ContractTestKit _kit = new(Today);
    private readonly FakeDeleteUndoService _undoService = new();
    private readonly FakeDeleteImpactService _impact = new();

    public void Dispose() => CultureInfo.CurrentUICulture = _uiCulture;

    // ---- the text ----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, "One")]
    [InlineData(2, "Few")]
    [InlineData(4, "Few")]
    [InlineData(5, "Many")]
    [InlineData(11, "Many")]
    [InlineData(12, "Many")]
    [InlineData(14, "Many")]
    [InlineData(21, "One")]
    [InlineData(22, "Few")]
    [InlineData(25, "Many")]
    [InlineData(101, "One")]
    [InlineData(111, "Many")]
    public void RussianPluralForm(int count, string expected) => Assert.Equal(expected, DependantsText.Form("ru", count));

    [Theory]
    [InlineData(1, "One")]
    [InlineData(0, "Many")]
    [InlineData(2, "Many")]
    [InlineData(21, "Many")]
    public void EnglishPluralForm(int count, string expected) => Assert.Equal(expected, DependantsText.Form("en", count));

    [Fact]
    public void List_Russian_12Bills_3Payments_1Tariff()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
        IReadOnlyList<Dependant> dependants =
            [new(DependantKind.Bills, 12), new(DependantKind.Payments, 3), new(DependantKind.Tariffs, 1)];

        Assert.Equal("12 счетов, 3 платежа, 1 тариф", DependantsText.List(ResxLocalizer.Russian, dependants));
    }

    [Fact]
    public void List_English_AndEmptyCountsAreLeftOut()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        IReadOnlyList<Dependant> dependants =
            [new(DependantKind.Bills, 1), new(DependantKind.Payments, 0), new(DependantKind.Tariffs, 2)];

        Assert.Equal("1 bill, 2 tariffs", DependantsText.List(ResxLocalizer.English, dependants));
        Assert.Equal(string.Empty, DependantsText.List(ResxLocalizer.English, []));
    }

    [Fact]
    public void Sentence_Russian_MatchesThePaperExample()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("ru-RU");
        var loc = ResxLocalizer.Russian;
        var list = DependantsText.List(loc, [new(DependantKind.Bills, 12), new(DependantKind.Payments, 3), new(DependantKind.Tariffs, 1)]);

        var sentence = $"{loc.Get("Dialog_DeleteDependants_Message", loc["DeleteKind_Contract"], "Газ", list)} {loc["Dialog_DeleteUndoHint"]}";

        Assert.Equal("Договор «Газ» будет удалён вместе с: 12 счетов, 3 платежа, 1 тариф. Это можно отменить сразу после удаления.", sentence);
    }

    // ---- the editors -------------------------------------------------------------------------------------------------

    private Contract StoredContract()
    {
        var supplier = new Supplier { Name = "Gas utility" };
        _kit.Suppliers.Stored.Add(supplier);
        var contract = new Contract
        {
            PropertyId = _kit.Property.Id,
            SupplierId = supplier.Id,
            CategoryId = _kit.CategoryIdFor(ServiceKind.Gas),
            Service = ServiceKind.Gas,
            Title = "Gas",
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        _kit.Contracts.Stored.Add(contract);
        return contract;
    }

    [Fact]
    public async Task Contract_WithDependants_AsksWithTheListAndOffersUndoAfterwards()
    {
        var contract = StoredContract();
        _impact.Dependants.AddRange([new(DependantKind.Bills, 12), new(DependantKind.Payments, 3)]);
        var undo = _kit.CreateUndoSupport(_undoService);
        _undoService.Touched.Add(new DeletedRow("Contracts", contract.Id));
        _kit.Toast.ActionResult = (_, _) => true;
        var vm = _kit.CreateContractEditor(undo, _impact);
        await vm.LoadAsync(contract.PropertyId, contract.Id);

        await vm.DeleteCommand.ExecuteAsync(null);
        await vm.UndoOffer;

        var (title, message) = Assert.Single(_kit.Dialogs.Confirmations);
        Assert.Equal("Dialog_Delete_Title Gas [en]", title);
        Assert.StartsWith("Dialog_DeleteDependants_Message DeleteKind_Contract [en]|Gas|", message, StringComparison.Ordinal);
        Assert.Contains("Dependant_Bills_Many", message, StringComparison.Ordinal);
        Assert.Contains("Dependant_Payments_Many", message, StringComparison.Ordinal);
        Assert.Contains("Dialog_DeleteUndoHint", message, StringComparison.Ordinal);
        Assert.Equal([contract.Id], _kit.Contracts.Deleted);
        Assert.Equal([new DeletedRow("Contracts", contract.Id)], Assert.Single(_undoService.Restored));
    }

    [Fact]
    public async Task Contract_NotConfirmed_IsKept()
    {
        var contract = StoredContract();
        _impact.Dependants.Add(new(DependantKind.Bills, 2));
        _kit.Dialogs.ConfirmResult = false;
        var vm = _kit.CreateContractEditor(_kit.CreateUndoSupport(_undoService), _impact);
        await vm.LoadAsync(contract.PropertyId, contract.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Contracts.Deleted);
        Assert.Empty(_undoService.Restored);
    }

    [Fact]
    public async Task Contract_WithoutDependants_IsDeletedAtOnceWithUndo()
    {
        var contract = StoredContract();
        _undoService.Touched.Add(new DeletedRow("Contracts", contract.Id));
        var vm = _kit.CreateContractEditor(_kit.CreateUndoSupport(_undoService), _impact);
        await vm.LoadAsync(contract.PropertyId, contract.Id);

        await vm.DeleteCommand.ExecuteAsync(null);
        await vm.UndoOffer;

        Assert.Empty(_kit.Dialogs.Confirmations);
        Assert.Equal([contract.Id], _kit.Contracts.Deleted);
        Assert.Contains(_kit.Toast.Calls, c => c.Message == "DeletedUndoMessage Gas [en]" && c.Action == "Undo [en]");
    }

    [Fact]
    public async Task Supplier_WithContracts_ListsThemInTheConfirmation()
    {
        var supplier = new Supplier { Name = "Gas utility" };
        _kit.Suppliers.Stored.Add(supplier);
        _impact.Dependants.AddRange([new(DependantKind.Contracts, 2), new(DependantKind.Bills, 14)]);
        var vm = _kit.CreateSupplierEditor(_kit.CreateUndoSupport(_undoService), _impact);
        await vm.LoadAsync(supplier.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        var (_, message) = Assert.Single(_kit.Dialogs.Confirmations);
        Assert.StartsWith("Dialog_DeleteDependants_Message DeleteKind_Supplier [en]|Gas utility|", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Meter_ConfirmationSaysItCannotBeUndoneExceptFromABackup_AndOffersNoUndo()
    {
        var meter = new Meter { PropertyId = _kit.Property.Id, Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, Location = "Kitchen", SerialNumber = "CW-1", Digits = 5, InstalledOn = Today };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        _kit.Meters.Stored.Add(meter);
        _impact.Dependants.Add(new(DependantKind.Links, 1));
        var vm = _kit.CreateMeterEditor(_impact);
        await vm.LoadAsync(_kit.Property.Id, meter.Id);

        await vm.DeleteCommand.ExecuteAsync(null);

        var (_, message) = Assert.Single(_kit.Dialogs.Confirmations);
        Assert.Contains("Dialog_DeleteNoUndoHint", message, StringComparison.Ordinal);
        Assert.Equal([meter.Id], _kit.Meters.Deleted);
        Assert.Empty(_undoService.Restored);
    }
}
