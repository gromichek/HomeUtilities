using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

public class MeterEditViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    private readonly ContractTestKit _kit = new(Today);

    [Fact]
    public async Task NewMeter_StartsWithOneTotalRegisterAndTheDefaultUnit()
    {
        var vm = await LoadNewAsync();

        Assert.True(vm.IsNew);
        Assert.Equal(MeterResource.ColdWater, vm.SelectedResource!.As<MeterResource>());
        Assert.Equal([MeterUnit.M3], vm.Units.Select(u => u.As<MeterUnit>()));
        var register = Assert.Single(vm.RegisterRows);
        Assert.Equal((RegisterKind.Total, EnergyDirection.Import), (register.Kind, register.Direction));
        Assert.Equal(Today, vm.InstalledOn);
    }

    [Theory]
    [InlineData(MeterResource.HotWater, MeterUnit.M3, new[] { MeterUnit.M3 })]
    [InlineData(MeterResource.Gas, MeterUnit.M3, new[] { MeterUnit.M3 })]
    [InlineData(MeterResource.Electricity, MeterUnit.KWh, new[] { MeterUnit.KWh })]
    [InlineData(MeterResource.Heat, MeterUnit.MWh, new[] { MeterUnit.MWh, MeterUnit.Gcal, MeterUnit.KWh })]
    [InlineData(MeterResource.Other, MeterUnit.M3, new[] { MeterUnit.M3, MeterUnit.KWh, MeterUnit.MWh, MeterUnit.Gcal })]
    public async Task ChangingTheResource_FiltersAndDefaultsTheUnit(MeterResource resource, MeterUnit expectedDefault, MeterUnit[] expectedUnits)
    {
        var vm = await LoadNewAsync();

        vm.SelectedResource = vm.Resources.First(r => r.Value.Equals(resource));

        Assert.Equal(expectedUnits, vm.Units.Select(u => u.As<MeterUnit>()));
        Assert.Equal(expectedDefault, vm.SelectedUnit!.As<MeterUnit>());
        Assert.Equal(vm.Units.ToList().IndexOf(vm.SelectedUnit), vm.SelectedUnitIndex);
    }

    [Fact]
    public async Task ChangingTheResource_KeepsAUnitThatIsStillValid()
    {
        var vm = await LoadNewAsync();
        vm.SelectedResource = vm.Resources.First(r => r.Value.Equals(MeterResource.Heat));
        vm.SelectedUnit = vm.Units.First(u => u.Value.Equals(MeterUnit.Gcal));

        vm.SelectedResource = vm.Resources.First(r => r.Value.Equals(MeterResource.Other));

        Assert.Equal(MeterUnit.Gcal, vm.SelectedUnit!.As<MeterUnit>());
    }

    [Fact]
    public async Task Save_StoresTheMeterWithTrimmedValuesAndStaysOpen()
    {
        var vm = await LoadNewAsync();
        vm.Location = "  Kitchen ";
        vm.SerialNumber = "CW-1";
        vm.DigitsText = "6";
        vm.IntervalText = "72";
        vm.InstalledOn = new DateOnly(2020, 10, 1);

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Meters.Saved);
        Assert.Equal(("Kitchen", "CW-1", 6, 72), (saved.Location, saved.SerialNumber, saved.Digits, saved.VerificationIntervalMonths));
        Assert.Equal(_kit.Property.Id, saved.PropertyId);
        Assert.Equal(new DateOnly(2020, 10, 1), saved.InstalledOn);
        Assert.Single(saved.Registers);
        Assert.False(vm.IsNew);
        Assert.False(vm.IsDirty);
        Assert.Equal("Kitchen", vm.Title);
        Assert.Equal(0, _kit.Navigation.BackCount);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-3")]
    [InlineData("1,5")]
    [InlineData("99999999999")]
    [InlineData("0")]
    [InlineData("13")]
    public async Task BadDigits_ShowAMessageAndDoNotSave(string digits)
    {
        var vm = await LoadNewAsync();
        vm.DigitsText = digits;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Meters.Saved);
        Assert.NotEmpty(vm.DigitsError);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("0")]
    [InlineData("601")]
    public async Task BadInterval_ShowsAMessageAndDoesNotSave(string interval)
    {
        var vm = await LoadNewAsync();
        vm.IntervalText = interval;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Meters.Saved);
        Assert.NotEmpty(vm.IntervalError);
    }

    [Fact]
    public async Task OverdueVerification_IsComputedFromInstallDateAndInterval()
    {
        var vm = await LoadNewAsync();
        vm.InstalledOn = new DateOnly(2020, 10, 1);
        vm.IntervalText = "72";

        Assert.Equal($"MeterNextVerification {new DateOnly(2026, 10, 1)} [en]", vm.VerificationLine);
        Assert.True(vm.IsVerificationOverdue);
        Assert.False(vm.IsVerificationDueSoon);
        Assert.StartsWith("MeterVerificationOverdue", vm.VerificationBadgeText);
    }

    [Fact]
    public async Task VerifiedDate_MovesTheDueDateFarAwayAndRemovesTheBadge()
    {
        var vm = await LoadNewAsync();
        vm.InstalledOn = new DateOnly(2020, 10, 1);
        vm.IntervalText = "72";
        vm.HasVerifiedOn = true;
        vm.VerifiedOn = new DateOnly(2021, 1, 15);

        Assert.Contains(new DateOnly(2027, 1, 15).ToString(), vm.VerificationLine);
        Assert.False(vm.IsVerificationOverdue);
        Assert.False(vm.IsVerificationDueSoon);
        Assert.Empty(vm.VerificationBadgeText);
    }

    [Fact]
    public async Task ExplicitDueDateWithin60Days_IsAmber()
    {
        var vm = await LoadNewAsync();
        vm.InstalledOn = new DateOnly(2020, 10, 1);
        vm.HasDueOverride = true;
        vm.DueOverride = new DateOnly(2026, 12, 1);

        Assert.False(vm.IsVerificationOverdue);
        Assert.True(vm.IsVerificationDueSoon);
        Assert.StartsWith("MeterVerificationDue", vm.VerificationBadgeText);
    }

    [Fact]
    public async Task NoIntervalAndNoOverride_HasNoVerificationLine()
    {
        var vm = await LoadNewAsync();

        Assert.Empty(vm.VerificationLine);
        Assert.False(vm.HasVerificationLine);
    }

    [Fact]
    public async Task DualTariff_ReplacesAnUnusedTotalRegisterByDayAndNight()
    {
        var vm = await LoadNewAsync();
        Assert.True(vm.CanApplyDualTariff);

        vm.ApplyDualTariffCommand.Execute(null);

        Assert.Equal([RegisterKind.Day, RegisterKind.Night], vm.RegisterRows.Select(r => r.Kind));
        Assert.True(vm.IsDirty);
        Assert.False(vm.CanApplyDualTariff);

        vm.SerialNumber = "E-1";
        vm.SelectedResource = vm.Resources.First(r => r.Value.Equals(MeterResource.Electricity));
        await vm.SaveCommand.ExecuteAsync(null);
        var saved = Assert.Single(_kit.Meters.Saved);
        Assert.Equal([RegisterKind.Day, RegisterKind.Night], saved.Registers.Select(r => r.Kind));
        Assert.All(saved.Registers, r => Assert.Equal(EnergyDirection.Import, r.Direction));
    }

    [Fact]
    public async Task DualTariff_IsNotOfferedWhenTheTotalRegisterHasReadings()
    {
        var meter = StoredMeter(withReading: true);

        var vm = await LoadAsync(meter);

        Assert.False(vm.CanApplyDualTariff);
        Assert.False(vm.ApplyDualTariffCommand.CanExecute(null));
        vm.ApplyDualTariffCommand.Execute(null);
        Assert.Equal([RegisterKind.Total], vm.RegisterRows.Select(r => r.Kind));
    }

    [Fact]
    public async Task AddRegister_OffersTheKindsNotYetPresentAndExportForcesTheDirection()
    {
        var vm = await LoadNewAsync();
        _kit.Dialogs.EditorScript = e =>
        {
            var editor = (RegisterEditorViewModel)e;
            Assert.Equal([RegisterKind.Day, RegisterKind.Night, RegisterKind.Peak, RegisterKind.Export], editor.Kinds.Select(k => k.As<RegisterKind>()));
            editor.SelectedKind = editor.Kinds.First(k => k.Value.Equals(RegisterKind.Export));
            editor.SaveCommand.Execute(null);
        };

        await vm.AddRegisterCommand.ExecuteAsync(null);

        Assert.Equal((RegisterKind.Export, EnergyDirection.Export), (vm.RegisterRows[^1].Kind, vm.RegisterRows[^1].Direction));
        Assert.Equal(2, vm.RegisterRows.Count);
        Assert.True(vm.IsDirty);
    }

    [Fact]
    public async Task AddRegister_WhenEveryKindExists_ShowsAMessageInsteadOfTheDialog()
    {
        var meter = StoredMeter(withReading: false);
        foreach (var kind in new[] { RegisterKind.Day, RegisterKind.Night, RegisterKind.Peak, RegisterKind.Export })
        {
            meter.Registers.Add(new MeterRegister { MeterId = meter.Id, Kind = kind, Direction = kind == RegisterKind.Export ? EnergyDirection.Export : EnergyDirection.Import });
        }

        var vm = await LoadAsync(meter);
        await vm.AddRegisterCommand.ExecuteAsync(null);

        Assert.Null(_kit.Dialogs.LastEditor);
        Assert.Single(_kit.Dialogs.Alerts);
    }

    [Fact]
    public async Task RemoveRegister_IsBlockedWhenItHasReadings()
    {
        var meter = StoredMeter(withReading: true);
        var vm = await LoadAsync(meter);

        await vm.RemoveRegisterCommand.ExecuteAsync(vm.RegisterRows[0]);

        Assert.Single(vm.RegisterRows);
        Assert.Single(_kit.Dialogs.Alerts);
        Assert.Empty(_kit.Dialogs.Confirmations);
    }

    [Fact]
    public async Task RemoveRegister_WithoutReadings_AsksAndRemoves()
    {
        var vm = await LoadNewAsync();
        vm.ApplyDualTariffCommand.Execute(null);

        await vm.RemoveRegisterCommand.ExecuteAsync(vm.RegisterRows[1]);

        Assert.Single(_kit.Dialogs.Confirmations);
        Assert.Equal([RegisterKind.Day], vm.RegisterRows.Select(r => r.Kind));
    }

    [Fact]
    public async Task RemovingEveryRegister_FailsValidation()
    {
        var vm = await LoadNewAsync();
        await vm.RemoveRegisterCommand.ExecuteAsync(vm.RegisterRows[0]);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Meters.Saved);
        Assert.NotEmpty(vm.RegistersError);
    }

    [Fact]
    public async Task SavingAnExistingMeter_KeepsTheReadingsOfItsRegisters()
    {
        var meter = StoredMeter(withReading: true);
        var vm = await LoadAsync(meter);
        vm.Notes = "behind the door";

        await vm.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(_kit.Meters.Saved);
        Assert.Equal(190m, Assert.Single(Assert.Single(saved.Registers).Readings).Value);
        Assert.Equal("behind the door", saved.Notes);
    }

    [Fact]
    public async Task MovingTheInstallDateAfterAReading_IsRejected()
    {
        var meter = StoredMeter(withReading: true);
        var vm = await LoadAsync(meter);
        vm.InstalledOn = new DateOnly(2026, 10, 1);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Meters.Saved);
        Assert.NotEmpty(vm.InstalledOnError);
    }

    [Fact]
    public async Task Delete_IsHiddenAndBlockedForAMeterWithReadings()
    {
        var vm = await LoadAsync(StoredMeter(withReading: true));

        Assert.False(vm.CanShowDelete);
        Assert.False(vm.ShowFormDelete);
        Assert.True(vm.ShowDeleteBlocked);
    }

    [Fact]
    public async Task Delete_IsAvailableForAMeterWithoutReadings()
    {
        var meter = StoredMeter(withReading: false);
        var vm = await LoadAsync(meter);
        vm.IsHostedInPage = true;

        Assert.True(vm.CanShowDelete);
        Assert.False(vm.ShowDeleteBlocked);
        await vm.DeleteCommand.ExecuteAsync(null);

        Assert.Equal([meter.Id], _kit.Meters.Deleted);
        Assert.Equal(1, _kit.Navigation.BackCount);
    }

    [Fact]
    public async Task ContractSwitches_ListTheContractsOfTheProperty()
    {
        var water = AddContract("Water");
        AddContract("Elsewhere", propertyId: Guid.NewGuid());
        var archived = AddContract("Archived");
        archived.IsArchived = true;
        var ended = AddContract("Ended");
        ended.ValidTo = new DateOnly(2025, 12, 31);

        var vm = await LoadNewAsync();

        var row = Assert.Single(vm.ContractSwitches);
        Assert.Equal((water.Id, "Water", false), (row.ContractId, row.Title, row.IsOn));
    }

    [Fact]
    public async Task TurningAContractSwitchOn_CreatesALinkFromTheLaterOfInstallDateAndToday()
    {
        var water = AddContract("Water");
        var vm = await LoadNewAsync();
        vm.InstalledOn = new DateOnly(2020, 10, 1);
        vm.ContractSwitches[0].IsOn = true;
        Assert.True(vm.IsDirty);

        await vm.SaveCommand.ExecuteAsync(null);

        var link = Assert.Single(_kit.Contracts.SavedLinks);
        Assert.Equal((water.Id, vm.EntityId, Today, null), (link.ContractId, link.MeterId, link.ValidFrom, link.ValidTo));
        Assert.True(vm.ContractSwitches[0].IsOn);
    }

    [Fact]
    public async Task ALinkWithAFutureInstallDate_StartsOnTheInstallDate()
    {
        AddContract("Water");
        var vm = await LoadNewAsync();
        vm.InstalledOn = new DateOnly(2026, 11, 15);
        vm.ContractSwitches[0].IsOn = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 11, 15), Assert.Single(_kit.Contracts.SavedLinks).ValidFrom);
    }

    [Fact]
    public async Task TurningAContractSwitchOff_EndsTheLinkYesterdayAndNeverDeletesIt()
    {
        var water = AddContract("Water");
        var meter = StoredMeter(withReading: false);
        var link = new ContractMeter { ContractId = water.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 10, 1) };
        _kit.Contracts.Links.Add(link);
        var vm = await LoadAsync(meter);
        Assert.True(vm.ContractSwitches[0].IsOn);

        vm.ContractSwitches[0].IsOn = false;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new DateOnly(2026, 10, 22), link.ValidTo);
        Assert.False(link.IsDeleted);
        Assert.Single(_kit.Contracts.Links);
        Assert.False(vm.ContractSwitches[0].IsOn);
    }

    [Fact]
    public async Task AnAlreadyLinkedContract_ProducesNoNewLinkOnSave()
    {
        var water = AddContract("Water");
        var meter = StoredMeter(withReading: false);
        _kit.Contracts.Links.Add(new ContractMeter { ContractId = water.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 10, 1) });
        var vm = await LoadAsync(meter);
        vm.Notes = "x";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Contracts.SavedLinks);
    }

    [Fact]
    public async Task History_ShowsTheReplacedAndTheReplacingMeterAndOpensThem()
    {
        var first = StoredMeter(withReading: false, serial: "CW-1");
        first.RemovedOn = new DateOnly(2026, 10, 23);
        var second = StoredMeter(withReading: false, serial: "CW-2");
        second.ReplacedMeterId = first.Id;
        second.InstalledOn = new DateOnly(2026, 10, 23);

        var vm = await LoadAsync(second);
        var older = await LoadAsync(first);

        var replaced = Assert.Single(vm.HistoryRows);
        Assert.Contains("MeterHistoryReplaced CW-1", replaced.Text);
        var replacedBy = Assert.Single(older.HistoryRows);
        Assert.Contains("MeterHistoryReplacedBy CW-2", replacedBy.Text);

        replacedBy.SelectCommand.Execute(null);
        var (route, parameters) = Assert.Single(_kit.Navigation.Visited);
        Assert.Equal(Routes.MeterEdit, route);
        var opened = Assert.IsType<MeterEditViewModel>(parameters![EditorNavigation.EditorParameter]);
        Assert.Equal(second.Id, opened.EntityId);
    }

    [Fact]
    public async Task ANewMeter_HasNoHistoryAndCannotBeReplaced()
    {
        var vm = await LoadNewAsync();

        Assert.False(vm.HasHistory);
        Assert.False(vm.CanReplace);
    }

    [Fact]
    public async Task ARemovedMeter_CannotBeReplaced()
    {
        var meter = StoredMeter(withReading: false);
        meter.RemovedOn = new DateOnly(2026, 10, 1);

        var vm = await LoadAsync(meter);

        Assert.False(vm.CanReplace);
    }

    [Fact]
    public async Task Replace_OpensTheWizardAndLeavesTheEditorAfterwards()
    {
        var meter = StoredMeter(withReading: true);
        var finished = new List<EditOutcome>();
        var vm = await LoadAsync(meter);
        vm.IsHostedInPage = true;
        vm.Finished = outcome =>
        {
            finished.Add(outcome);
            return Task.CompletedTask;
        };
        _kit.Dialogs.EditorScript = e =>
        {
            var wizard = (ReplaceMeterWizardViewModel)e;
            wizard.Registers[0].FinalText = "202";
            wizard.NextCommand.Execute(null);
            wizard.NextCommand.Execute(null);
            wizard.NextCommand.Execute(null);
            wizard.NextCommand.Execute(null);
            wizard.ReplaceCommand.Execute(null);
        };

        await vm.ReplaceCommand.ExecuteAsync(null);

        Assert.Single(_kit.Meters.Replacements);
        Assert.Equal(1, _kit.Navigation.BackCount);
        Assert.Equal([EditOutcome.Saved], finished);
    }

    [Fact]
    public async Task Replace_CancelledWizard_ChangesNothing()
    {
        var vm = await LoadAsync(StoredMeter(withReading: true));
        vm.IsHostedInPage = true;

        await vm.ReplaceCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Meters.Replacements);
        Assert.Equal(0, _kit.Navigation.BackCount);
    }

    [Fact]
    public async Task Replace_WithUnsavedChanges_AsksFirstAndStopsOnCancel()
    {
        var vm = await LoadAsync(StoredMeter(withReading: true));
        vm.Notes = "changed";
        _kit.Dialogs.UnsavedChoice = UnsavedChangesChoice.Cancel;

        await vm.ReplaceCommand.ExecuteAsync(null);

        Assert.Equal(1, _kit.Dialogs.UnsavedAsked);
        Assert.Null(_kit.Dialogs.LastEditor);
    }

    [Fact]
    public async Task Cancel_WithUnsavedChanges_AsksAndLeaves()
    {
        var vm = await LoadNewAsync();
        vm.IsHostedInPage = true;
        vm.Location = "Bath";

        await vm.CancelCommand.ExecuteAsync(null);

        Assert.Equal(1, _kit.Dialogs.UnsavedAsked);
        Assert.Equal(1, _kit.Navigation.BackCount);
        Assert.Empty(_kit.Meters.Saved);
    }

    [Fact]
    public async Task LanguageChange_RefreshesTextsWithoutLosingInput()
    {
        var vm = await LoadNewAsync();
        vm.Location = "Kitchen";
        vm.SelectedResource = vm.Resources.First(r => r.Value.Equals(MeterResource.HotWater));

        _kit.Language.SetLanguage("ru");

        Assert.Equal("MeterGeneral [ru]", vm.Texts.GeneralHeader);
        Assert.Equal("Kitchen", vm.Location);
        Assert.Equal(MeterResource.HotWater, vm.SelectedResource!.As<MeterResource>());
        Assert.Equal("Enum_MeterResource_HotWater [ru]", vm.SelectedResource.DisplayName);
    }

    private Contract AddContract(string title, Guid? propertyId = null)
    {
        var contract = new Contract
        {
            PropertyId = propertyId ?? _kit.Property.Id,
            Title = title,
            Service = ServiceKind.ColdWater,
            ValidFrom = new DateOnly(2020, 1, 1),
        };
        _kit.Contracts.Stored.Add(contract);
        return contract;
    }

    private Meter StoredMeter(bool withReading, string serial = "CW-1")
    {
        var meter = new Meter
        {
            PropertyId = _kit.Property.Id,
            Resource = MeterResource.ColdWater,
            Unit = MeterUnit.M3,
            SerialNumber = serial,
            Location = "Kitchen",
            Digits = 5,
            InstalledOn = new DateOnly(2020, 10, 1),
        };
        var register = MeterRegister.CreateDefault(meter.Id);
        if (withReading)
        {
            register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 9, 26), Value = 190m });
        }

        meter.Registers.Add(register);
        _kit.Meters.Stored.Add(meter);
        return meter;
    }

    private async Task<MeterEditViewModel> LoadNewAsync()
    {
        var vm = _kit.CreateMeterEditor();
        await vm.LoadAsync(_kit.Property.Id, null);
        return vm;
    }

    private async Task<MeterEditViewModel> LoadAsync(Meter meter)
    {
        var vm = _kit.CreateMeterEditor();
        await vm.LoadAsync(meter.PropertyId, meter.Id);
        return vm;
    }
}
