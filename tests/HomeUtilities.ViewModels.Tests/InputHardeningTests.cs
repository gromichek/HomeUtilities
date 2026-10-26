using System.Reflection;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// Feeds nasty text into every string property of every editor and dialog view model, then saves and reads every
/// property (previews, error texts): whatever the user types may only produce validation messages, never an exception.
/// </summary>
public class InputHardeningTests
{
    private static readonly string[] Nasty =
    [
        string.Empty,
        "   ",
        "abc",
        "12abc",
        "1,2,3",
        "1.234,56",
        "99999999999999999999999",
        "-99999999999999999999999",
        "-5",
        "-0,5",
        "5,5",
        "5.5",
        "0",
        "32",
        "-1",
        "2147483648",
        "😀🏠",
        new string('x', 5000),
        "  padded  ",
        "#12",
        "#GGGGGG",
        "#1E88E5",
        "{",
        "}",
        "{}",
        "{Period:mm.yyyy}",
        "{Period:HH}{Foo}{Period:",
        "Abon. Nr. {CustomerNumber}, {Period:mm.yyyy}",
        "https://",
        "http://x",
        "javascript:alert(1)",
        "LV80BANK0000435195001",
        "LV00BANK0000435195001",
        "ZZ99",
        "\0\u0001\r\n\t",
    ];

    public static TheoryData<string> Inputs => new(Nasty);

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task SupplierEditor_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var vm = kit.CreateSupplierEditor();
        await vm.LoadAsync(null);

        await HammerAsync(vm, input, () => vm.SaveCommand.ExecuteAsync(null));
        vm.FormatIbanCommand.Execute(null);
        await vm.CopyPurposeCommand.ExecuteAsync(null);
        await vm.CopyIbanCommand.ExecuteAsync(null);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task ContractEditor_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var supplier = new Supplier { Name = "Water utility" };
        kit.Suppliers.Stored.Add(supplier);
        var vm = kit.CreateContractEditor();
        await vm.LoadAsync(kit.Property.Id, null);
        vm.ContractTitle = "Water";
        vm.SelectedSupplier = new ChoiceOption(supplier.Id, supplier.Name, string.Empty);
        vm.HasReadingWindow = true;

        await HammerAsync(vm, input, () => vm.SaveCommand.ExecuteAsync(null));
        vm.FormatIbanCommand.Execute(null);
        await vm.CopyPurposeCommand.ExecuteAsync(null);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task PropertyEditor_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var vm = kit.CreatePropertyEditor();
        await vm.LoadAsync(null);

        await HammerAsync(vm, input, () => vm.SaveCommand.ExecuteAsync(null));
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task CategoryEditor_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var vm = new CategoryEditViewModel(kit.Categories, kit.Localizer, kit.Dialogs, kit.Navigation, kit.Toast);
        await vm.LoadAsync(null);

        await HammerAsync(vm, input, () => vm.SaveCommand.ExecuteAsync(null));
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task MeterEditor_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        kit.Contracts.Stored.Add(new Contract { PropertyId = kit.Property.Id, Title = "Water", ValidFrom = new DateOnly(2020, 1, 1) });
        var vm = kit.CreateMeterEditor();
        await vm.LoadAsync(kit.Property.Id, null);
        vm.ContractSwitches[0].IsOn = true;

        await HammerAsync(vm, input, () => vm.SaveCommand.ExecuteAsync(null));
        vm.ApplyDualTariffCommand.Execute(null);
        await vm.ReplaceCommand.ExecuteAsync(null);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task ReplaceWizard_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var meter = new Meter { PropertyId = kit.Property.Id, Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, InstalledOn = new DateOnly(2020, 10, 1), Digits = 5 };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        kit.Meters.Stored.Add(meter);
        var wizard = new ReplaceMeterWizardViewModel(kit.Localizer, kit.Clock, kit.Meters, meter, []);

        Func<Task> walk = async () =>
        {
            wizard.Step = ReplaceMeterStep.Date;
            for (var i = 0; i < 5; i++)
            {
                wizard.NextCommand.Execute(null);
            }

            await wizard.ReplaceCommand.ExecuteAsync(null);
            wizard.PreviousCommand.Execute(null);
        };
        await HammerAsync(wizard, input, walk);
        foreach (var row in wizard.Registers)
        {
            await HammerAsync(row, input, walk);
        }
    }

    [Fact]
    public async Task ReplaceWizard_HammeringReallyReachesTheReplaceLogic()
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var meter = new Meter { PropertyId = kit.Property.Id, Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, InstalledOn = new DateOnly(2020, 10, 1) };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        kit.Meters.Stored.Add(meter);
        var wizard = new ReplaceMeterWizardViewModel(kit.Localizer, kit.Clock, kit.Meters, meter, []);
        wizard.Registers[0].FinalText = "10";
        for (var i = 0; i < 4; i++)
        {
            wizard.NextCommand.Execute(null);
        }

        await wizard.ReplaceCommand.ExecuteAsync(null);

        Assert.Single(kit.Meters.Replacements);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task TariffComponentEditor_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var contract = new Contract { PropertyId = kit.Property.Id, Title = "Water", Service = ServiceKind.ColdWater };
        kit.Contracts.Stored.Add(contract);
        var meter = new Meter { PropertyId = kit.Property.Id, Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, InstalledOn = new DateOnly(2020, 1, 1) };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        kit.Meters.Stored.Add(meter);
        kit.Contracts.Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        kit.Property.Parameters.Add(new PropertyParameter { PropertyId = kit.Property.Id, Kind = PropertyParameterKind.TotalArea, Value = 52.3m, ValidFrom = new DateOnly(2020, 1, 1) });

        foreach (var basis in Enum.GetValues<ChargeBasis>())
        {
            var vm = kit.CreateTariffEditor();
            vm.ContractId = contract.Id;
            await vm.LoadAsync(null);
            vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(basis));
            kit.Dialogs.EditorScript = editor =>
            {
                var dialog = (TariffRateEditorViewModel)editor;
                dialog.PriceText = "1,41";
                dialog.SaveCommand.Execute(null);
            };
            await vm.AddRateCommand.ExecuteAsync(null);
            kit.Dialogs.EditorScript = null;

            vm.SelectedVatOption = vm.VatOptions.Single(o => o.IsCustom);
            if (vm.Parameters.FirstOrDefault(o => o.Value.Equals(PropertyParameterKind.Custom)) is { } custom)
            {
                vm.SelectedParameter = custom;
            }

            await HammerAsync(vm, input, () => vm.SaveCommand.ExecuteAsync(null));
        }
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task TariffRateDialog_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var rate = new TariffRateEditorViewModel(kit.Localizer, kit.Clock, null, Currency.EUR, true, null, _ => false);
        await HammerAsync(rate, input, () => { rate.SaveCommand.Execute(null); return Task.CompletedTask; });

        rate.IsStepped = true;
        rate.AddTierCommand.Execute(null);
        await HammerAsync(rate, input, () => { rate.SaveCommand.Execute(null); return Task.CompletedTask; });
        foreach (var row in rate.TierRows)
        {
            await HammerAsync(row, input, () => { rate.SaveCommand.Execute(null); return Task.CompletedTask; });
        }
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task VatRateEditorAndVersionDialog_NeverThrow(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var vm = kit.CreateVatRateEditor();
        await vm.LoadAsync(null);
        await HammerAsync(vm, input, () => vm.SaveCommand.ExecuteAsync(null));

        var seeded = kit.CreateVatRateEditor();
        await seeded.LoadAsync(kit.VatRates.Stored[0].Id);
        await HammerAsync(seeded, input, () => seeded.SaveCommand.ExecuteAsync(null));

        var version = new VatVersionEditorViewModel(kit.Localizer, kit.Clock, null, _ => false);
        await HammerAsync(version, input, () => { version.SaveCommand.Execute(null); return Task.CompletedTask; });
    }

    [Fact]
    public async Task TariffEditorHammering_ReallyReachesTheSaveLogic()
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var contract = new Contract { PropertyId = kit.Property.Id, Title = "Water", Service = ServiceKind.ColdWater };
        kit.Contracts.Stored.Add(contract);
        var vm = kit.CreateTariffEditor();
        vm.ContractId = contract.Id;
        await vm.LoadAsync(null);
        vm.SelectedBasis = vm.Bases.Single(o => o.Value.Equals(ChargeBasis.FromBill));
        vm.ComponentName = "Heating";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(kit.Tariffs.Saved);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task ChildDialogs_NeverThrow(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));

        var link = new LinkEditorViewModel(kit.Localizer, null);
        await HammerAsync(link, input, () => { link.SaveCommand.Execute(null); return Task.CompletedTask; });

        var parameter = new ParameterEditorViewModel(kit.Localizer, kit.Clock, null, _ => false);
        await HammerAsync(parameter, input, () => { parameter.SaveCommand.Execute(null); return Task.CompletedTask; });

        var register = new RegisterEditorViewModel(kit.Localizer, new HashSet<RegisterKind>());
        await HammerAsync(register, input, () => { register.SaveCommand.Execute(null); return Task.CompletedTask; });

        var field = new CustomFieldEditorViewModel(kit.Localizer, null, _ => false);
        await HammerAsync(field, input, () => { field.SaveCommand.Execute(null); return Task.CompletedTask; });
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task BillEditor_NeverThrows(string input)
    {
        var kit = new BillTestKit(new DateOnly(2026, 10, 27));
        var contract = kit.AddWaterContract();
        contract.CustomFields.Add(new HomeUtilities.Core.Billing.CustomFieldDefinition { OwnerId = contract.Id, Name = "Losses", Kind = HomeUtilities.Core.Billing.CustomFieldKind.Number, Target = HomeUtilities.Core.Billing.CustomFieldTarget.Bill });
        contract.CustomFields.Add(new HomeUtilities.Core.Billing.CustomFieldDefinition { OwnerId = contract.Id, Name = "Reference", Kind = HomeUtilities.Core.Billing.CustomFieldKind.Text, Target = HomeUtilities.Core.Billing.CustomFieldTarget.Bill });
        var vm = kit.CreateEditor();
        await vm.LoadAsync(null);
        kit.Kit.Dialogs.ChooseScript = o => o.Contains("Water") ? "Water" : o.FirstOrDefault();
        await vm.ChooseContractCommand.ExecuteAsync(null);
        await vm.ChooseCategoryCommand.ExecuteAsync(null);

        Func<Task> save = async () =>
        {
            await vm.SaveCommand.ExecuteAsync(null);
            foreach (var row in vm.FieldRows)
            {
                row.Text = input;
            }

            await vm.SaveCommand.ExecuteAsync(null);
        };
        await HammerAsync(vm, input, save);
        foreach (var amounts in new[] { "NetText", "VatPercentText", "GrossText" })
        {
            typeof(AmountEditor).GetProperty(amounts)!.SetValue(vm.Amounts, input);
            await save();
            ReadAll(vm);
            ReadAll(vm.Amounts);
        }

        await vm.MarkPaidCommand.ExecuteAsync(null);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task PaymentEditor_NeverThrows(string input)
    {
        var kit = new BillTestKit(new DateOnly(2026, 11, 3));
        var contract = kit.AddWaterContract(tariff: false);
        var bill = kit.AddBill(contract, new HomeUtilities.Core.Common.YearMonth(2026, 10), 46.02m);
        kit.AddBill(contract, new HomeUtilities.Core.Common.YearMonth(2026, 9), 30m);
        var vm = kit.CreatePaymentEditor();
        vm.PresetBillId = bill.Id;
        await vm.LoadAsync(null);

        Func<Task> save = async () =>
        {
            await vm.SaveCommand.ExecuteAsync(null);
            await vm.FillReferenceCommand.ExecuteAsync(null);
            await vm.AutoAllocateCommand.ExecuteAsync(null);
        };
        await HammerAsync(vm, input, save);
        vm.AmountText = input;
        await save();
        ReadAll(vm);

        // the allocation dialog gets the same input
        kit.Kit.Dialogs.ChooseScript = options => options.FirstOrDefault();
        kit.Kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (AllocationAmountEditorViewModel)editor;
            dialog.AmountText = input;
            dialog.Note = input;
            dialog.SaveCommand.Execute(null);
            ReadAll(dialog);
        };
        await vm.AddAllocationCommand.ExecuteAsync(null);
        foreach (var row in vm.AllocationRows.ToList())
        {
            await row.EditCommand.ExecuteAsync(null);
        }

        await vm.SaveCommand.ExecuteAsync(null);
        ReadAll(vm);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task PaymentsList_NeverThrows(string input)
    {
        var kit = new BillTestKit(new DateOnly(2026, 11, 3));
        var contract = kit.AddWaterContract(tariff: false);
        var bill = kit.AddBill(contract, new HomeUtilities.Core.Common.YearMonth(2026, 10), 46.02m);
        kit.AddPayment(70m, (bill, 46.02m));
        var vm = kit.CreatePayments();
        await vm.ReloadAsync();
        kit.Kit.Dialogs.ChooseScript = _ => input;

        await vm.ChoosePropertyFilterCommand.ExecuteAsync(null);
        await vm.ChooseContractFilterCommand.ExecuteAsync(null);
        vm.Period.PreviousCommand.Execute(null);
        vm.Period.ShowMonthCommand.Execute(null);
        vm.Period.PreviousCommand.Execute(null);
        vm.Period.ShowAllYearsCommand.Execute(null);
        ReadAll(vm);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task BillLineDialog_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 27));
        var definitions = new[]
        {
            new HomeUtilities.Core.Billing.CustomFieldDefinition { Name = "Per line", Kind = HomeUtilities.Core.Billing.CustomFieldKind.Money, Target = HomeUtilities.Core.Billing.CustomFieldTarget.BillLine },
        };
        var dialog = new BillLineEditorViewModel(kit.Localizer, null, kit.Categories.Stored, definitions, new DateOnly(2026, 10, 27));

        Func<Task> save = () =>
        {
            dialog.SaveCommand.Execute(null);
            return Task.CompletedTask;
        };
        await HammerAsync(dialog, input, save);
        foreach (var amounts in new[] { "NetText", "VatPercentText", "GrossText" })
        {
            typeof(AmountEditor).GetProperty(amounts)!.SetValue(dialog.Amounts, input);
            await save();
            ReadAll(dialog);
        }

        dialog.FieldRows[0].Text = input;
        await save();
        ReadAll(dialog);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task ReadingsScreen_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var meter = new Meter { PropertyId = kit.Property.Id, Resource = MeterResource.Electricity, Unit = MeterUnit.KWh, InstalledOn = new DateOnly(2020, 10, 1), Digits = 5 };
        meter.Registers.Add(new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Day, Direction = EnergyDirection.Import });
        meter.Registers.Add(new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Night, Direction = EnergyDirection.Import });
        foreach (var register in meter.Registers)
        {
            register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 9, 26), Value = 1000m });
        }

        kit.Meters.Stored.Add(meter);
        var vm = kit.CreateReadings();
        await vm.ReloadAsync();

        foreach (var row in vm.Groups.SelectMany(g => g.Cards).SelectMany(c => c.Rows))
        {
            await HammerAsync(row, input, () => vm.SaveAllCommand.ExecuteAsync(null));
            row.ToggleNoteCommand.Execute(null);
            row.Clear();
        }

        await vm.CanLeaveAsync();
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public async Task ReadingEditDialog_NeverThrows(string input)
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var meter = new Meter { PropertyId = kit.Property.Id, Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, InstalledOn = new DateOnly(2020, 10, 1) };
        var register = MeterRegister.CreateDefault(meter.Id);
        meter.Registers.Add(register);
        var reading = new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 9, 26), Value = 100m };
        register.Readings.Add(reading);

        var dialog = new ReadingEditorViewModel(kit.Localizer, meter, register, reading, kit.Media);
        await HammerAsync(dialog, input, () => { dialog.SaveCommand.Execute(null); return Task.CompletedTask; });
    }

    [Fact]
    public async Task ReadingsScreen_HammeringReallyReachesTheSaveLogic()
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var meter = new Meter { PropertyId = kit.Property.Id, Resource = MeterResource.ColdWater, Unit = MeterUnit.M3, InstalledOn = new DateOnly(2020, 10, 1) };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        kit.Meters.Stored.Add(meter);
        var vm = kit.CreateReadings();
        await vm.ReloadAsync();

        vm.Groups[0].Cards[0].Rows[0].ValueText = "12,5";
        await vm.SaveAllCommand.ExecuteAsync(null);

        Assert.Single(kit.Meters.AddedBatches);
    }

    [Fact]
    public async Task Hammering_ReallyReachesTheSaveLogic()
    {
        // Guards the harness: a valid name must save, so a silent early exit cannot make the tests pass vacuously.
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var vm = new CategoryEditViewModel(kit.Categories, kit.Localizer, kit.Dialogs, kit.Navigation, kit.Toast);
        await vm.LoadAsync(null);
        vm.Name = "Kindergarten";
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Single(kit.Categories.Saved);
    }

    [Fact]
    public async Task ChargesMonthPicker_ClampsExtremeDates_AndTheButtonsStopAtTheEnds()
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var vm = kit.CreateCharges();
        await vm.ReloadAsync();

        foreach (var date in new[] { DateOnly.MinValue, new DateOnly(1, 1, 1), new DateOnly(1899, 12, 31), DateOnly.MaxValue, new DateOnly(9999, 12, 31), new DateOnly(2200, 12, 31), new DateOnly(1900, 1, 1) })
        {
            vm.PickerDate = date;
            await vm.PendingMonthChange;
            Assert.True(vm.Month >= ChargesViewModel.EarliestMonth && vm.Month <= ChargesViewModel.LatestMonth);
            Assert.False(string.IsNullOrEmpty(vm.MonthText));
        }

        vm.PickerDate = DateOnly.MinValue;
        await vm.PendingMonthChange;
        Assert.Equal(new YearMonth(1900, 1), vm.Month);
        Assert.False(vm.PreviousMonthCommand.CanExecute(null));
        Assert.True(vm.NextMonthCommand.CanExecute(null));

        vm.PickerDate = DateOnly.MaxValue;
        await vm.PendingMonthChange;
        Assert.Equal(new YearMonth(2200, 12), vm.Month);
        Assert.False(vm.NextMonthCommand.CanExecute(null));
        await vm.NextMonthCommand.ExecuteAsync(null);
        Assert.Equal(new YearMonth(2200, 12), vm.Month);
        ReadAll(vm);
    }

    /// <summary>Sets each settable string property to <paramref name="input"/>, runs <paramref name="act"/> and reads all properties.</summary>
    private static async Task HammerAsync(object viewModel, string input, Func<Task> act)
    {
        var properties = viewModel.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.CanWrite && p.GetIndexParameters().Length == 0)
            .ToList();
        Assert.NotEmpty(properties);

        foreach (var property in properties)
        {
            property.SetValue(viewModel, input);
            await act();
            ReadAll(viewModel);
        }
    }

    private static void ReadAll(object viewModel)
    {
        foreach (var property in viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.CanRead && property.GetIndexParameters().Length == 0)
            {
                _ = property.GetValue(viewModel);
            }
        }
    }
}
