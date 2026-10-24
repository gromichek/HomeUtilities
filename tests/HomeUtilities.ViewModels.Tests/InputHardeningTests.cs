using System.Reflection;
using HomeUtilities.Core.Domain;
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
