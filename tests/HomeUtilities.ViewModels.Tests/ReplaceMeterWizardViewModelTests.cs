using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

public class ReplaceMeterWizardViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    private readonly ContractTestKit _kit = new(Today);

    [Fact]
    public void Starts_OnTheDateStepWithTodayAndPrefilledDigitsAndInterval()
    {
        var wizard = Create(StoredMeter());

        Assert.Equal(ReplaceMeterStep.Date, wizard.Step);
        Assert.Equal(Today, wizard.ReplacementDate);
        Assert.Equal("5", wizard.NewDigitsText);
        Assert.Equal("72", wizard.NewIntervalText);
        Assert.Empty(wizard.NewSerialNumber);
        Assert.False(wizard.CanGoBack);
        Assert.Equal("WizardStepOf 1|5 [en]", wizard.StepNumberText);
    }

    [Fact]
    public void DefaultDate_IsNotBeforeTheLastReading()
    {
        var meter = StoredMeter(lastReading: new DateOnly(2026, 11, 2));

        Assert.Equal(new DateOnly(2026, 11, 2), Create(meter).ReplacementDate);
    }

    [Fact]
    public void DateBeforeTheLastReading_IsRejectedAndBlocksNext()
    {
        var wizard = Create(StoredMeter());
        wizard.ReplacementDate = new DateOnly(2026, 9, 1);

        wizard.NextCommand.Execute(null);

        Assert.Equal(ReplaceMeterStep.Date, wizard.Step);
        Assert.Contains("Error_meter_wizard_date_before_last_reading", wizard.DateError);
    }

    [Fact]
    public void DateBeforeInstallation_IsRejected()
    {
        var wizard = Create(StoredMeter(withReading: false));
        wizard.ReplacementDate = new DateOnly(2020, 9, 30);

        wizard.NextCommand.Execute(null);

        Assert.Equal(ReplaceMeterStep.Date, wizard.Step);
        Assert.Contains("Error_meter_wizard_date_before_installed", wizard.DateError);
    }

    [Fact]
    public void DateEqualToTheLastReading_IsAccepted()
    {
        var wizard = Create(StoredMeter());
        wizard.ReplacementDate = new DateOnly(2026, 9, 26);

        wizard.NextCommand.Execute(null);

        Assert.Equal(ReplaceMeterStep.FinalReading, wizard.Step);
        Assert.Empty(wizard.DateError);
    }

    [Fact]
    public void FinalReadingBelowTheLastOne_IsRejectedWhenTheMeterHasNoDigits()
    {
        var wizard = Create(StoredMeter(digits: null));
        wizard.NextCommand.Execute(null);
        wizard.Registers[0].FinalText = "189";

        wizard.NextCommand.Execute(null);

        Assert.Equal(ReplaceMeterStep.FinalReading, wizard.Step);
        Assert.Contains("Error_meter_wizard_final_below_last", wizard.Registers[0].FinalError);
        Assert.Empty(wizard.Registers[0].FinalWarning);
    }

    [Fact]
    public void FinalReadingBelowTheLastOne_IsARolloverWarningWhenDigitsAreSet()
    {
        var wizard = Create(StoredMeter(digits: 5));
        wizard.NextCommand.Execute(null);
        wizard.Registers[0].FinalText = "12";

        wizard.NextCommand.Execute(null);

        Assert.Equal(ReplaceMeterStep.NewMeter, wizard.Step);
        Assert.Empty(wizard.Registers[0].FinalError);
        Assert.StartsWith("WizardRolloverWarning 190|5", wizard.Registers[0].FinalWarning);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("1,2,3")]
    public void BadFinalReading_BlocksNext(string text)
    {
        var wizard = Create(StoredMeter());
        wizard.NextCommand.Execute(null);
        wizard.Registers[0].FinalText = text;

        wizard.NextCommand.Execute(null);

        Assert.Equal(ReplaceMeterStep.FinalReading, wizard.Step);
        Assert.NotEmpty(wizard.Registers[0].FinalError);
    }

    [Theory]
    [InlineData("0", "72", true, false)]
    [InlineData("13", "72", true, false)]
    [InlineData("x", "72", true, false)]
    [InlineData("5", "0", false, true)]
    [InlineData("5", "999", false, true)]
    [InlineData("5", "72", false, false)]
    public void NewMeterStep_ChecksDigitsAndInterval(string digits, string interval, bool digitsBad, bool intervalBad)
    {
        var wizard = AtNewMeterStep();
        wizard.NewDigitsText = digits;
        wizard.NewIntervalText = interval;

        wizard.NextCommand.Execute(null);

        Assert.Equal(digitsBad || intervalBad ? ReplaceMeterStep.NewMeter : ReplaceMeterStep.Contracts, wizard.Step);
        Assert.Equal(digitsBad, wizard.DigitsError.Length > 0);
        Assert.Equal(intervalBad, wizard.IntervalError.Length > 0);
    }

    [Fact]
    public void BadInitialReading_BlocksNext()
    {
        var wizard = AtNewMeterStep();
        wizard.Registers[0].InitialText = "-1";

        wizard.NextCommand.Execute(null);

        Assert.Equal(ReplaceMeterStep.NewMeter, wizard.Step);
        Assert.NotEmpty(wizard.Registers[0].InitialError);
    }

    [Fact]
    public void ContractsStep_PreselectsTheContractsOfTheOldMeter()
    {
        var meter = StoredMeter();
        var water = Link("Water", meter, linked: true);
        var heating = Link("Heating", meter, linked: true);
        var electricity = Link("Electricity", meter, linked: false);

        var wizard = Create(meter, water, heating, electricity);

        Assert.Equal(["Electricity", "Heating", "Water"], wizard.ContractRows.Select(r => r.Title));
        Assert.Equal([false, true, true], wizard.ContractRows.Select(r => r.IsOn));
    }

    [Fact]
    public void Previous_GoesBackOneStep()
    {
        var wizard = AtNewMeterStep();

        wizard.PreviousCommand.Execute(null);

        Assert.Equal(ReplaceMeterStep.FinalReading, wizard.Step);
        Assert.True(wizard.CanGoBack);
    }

    [Fact]
    public void Cancel_AtAnyStep_ChangesNothing()
    {
        var wizard = AtNewMeterStep();
        var closed = 0;
        wizard.CloseRequested += () => closed++;

        wizard.CancelCommand.Execute(null);

        Assert.Equal(1, closed);
        Assert.False(wizard.Saved);
        Assert.Empty(_kit.Meters.Replacements);
        Assert.Empty(_kit.Meters.Saved);
    }

    [Fact]
    public async Task Replace_StoresTheExpectedGraphInOneCall()
    {
        var meter = StoredMeter();
        var water = Link("Water", meter, linked: true);
        var heating = Link("Heating", meter, linked: true);
        var wizard = Create(meter, water, heating);
        wizard.Registers[0].FinalText = "202";
        Advance(wizard);
        wizard.NewSerialNumber = " CW-2 ";
        wizard.HasVerifiedOn = true;
        wizard.VerifiedOn = new DateOnly(2026, 10, 1);
        wizard.ContractRows.First(r => r.Title == "Heating").IsOn = false;
        Advance(wizard);
        Advance(wizard);
        Advance(wizard);
        Assert.True(wizard.IsSummaryStep);
        Assert.NotEmpty(wizard.SummaryLines);

        await wizard.ReplaceCommand.ExecuteAsync(null);

        var (old, created, closed, newLinks) = Assert.Single(_kit.Meters.Replacements);
        Assert.True(wizard.Saved);
        Assert.Equal(created.Id, wizard.NewMeterId);

        Assert.Equal(Today, old.RemovedOn);
        var final = old.Registers[0].Readings.Single(r => r.Kind == ReadingKind.Final);
        Assert.Equal((Today, 202m), (final.Date, final.Value));
        Assert.Equal(190m, old.Registers[0].Readings.Single(r => r.Kind == ReadingKind.Regular).Value);

        Assert.Equal(("CW-2", 5, 72, Today, meter.Id), (created.SerialNumber, created.Digits, created.VerificationIntervalMonths, created.InstalledOn, created.ReplacedMeterId));
        Assert.Equal(new DateOnly(2026, 10, 1), created.VerifiedOn);
        var initial = Assert.Single(created.Registers[0].Readings);
        Assert.Equal((ReadingKind.Initial, 0m, Today), (initial.Kind, initial.Value, initial.Date));

        Assert.Equal(2, closed.Count);
        Assert.All(closed, l => Assert.Equal(Today.AddDays(-1), l.ValidTo));
        Assert.Equal(new[] { water.Contract.Id, heating.Contract.Id }.Order(), closed.Select(l => l.ContractId).Order());
        var link = Assert.Single(newLinks);
        Assert.Equal((water.Contract.Id, created.Id, Today, null), (link.ContractId, link.MeterId, link.ValidFrom, link.ValidTo));
    }

    [Fact]
    public async Task Replace_WithAnInvalidEarlierStep_JumpsBackToIt()
    {
        var wizard = AtNewMeterStep();
        wizard.NewDigitsText = "abc";
        wizard.Step = ReplaceMeterStep.Summary;

        await wizard.ReplaceCommand.ExecuteAsync(null);

        Assert.Equal(ReplaceMeterStep.NewMeter, wizard.Step);
        Assert.Empty(_kit.Meters.Replacements);
        Assert.False(wizard.Saved);
    }

    [Fact]
    public async Task Replace_WhenTheStoreFails_KeepsTheWizardOpenAndCanBeRetried()
    {
        var wizard = Create(StoredMeter());
        wizard.Registers[0].FinalText = "202";
        Advance(wizard);
        Advance(wizard);
        Advance(wizard);
        Advance(wizard);
        _kit.Meters.ReplacementFailure = new InvalidOperationException("disk full");

        await Assert.ThrowsAsync<InvalidOperationException>(() => wizard.ReplaceCommand.ExecuteAsync(null));
        Assert.False(wizard.Saved);
        Assert.False(wizard.IsBusy);

        _kit.Meters.ReplacementFailure = null;
        await wizard.ReplaceCommand.ExecuteAsync(null);

        Assert.True(wizard.Saved);
        Assert.Single(_kit.Meters.Replacements);
    }

    [Fact]
    public async Task Replace_HandlesEveryRegisterOfADualTariffMeter()
    {
        var meter = StoredMeter();
        meter.Registers.Clear();
        foreach (var (kind, last) in new[] { (RegisterKind.Day, 100m), (RegisterKind.Night, 50m) })
        {
            var register = new MeterRegister { MeterId = meter.Id, Kind = kind, Direction = EnergyDirection.Import };
            register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 9, 26), Value = last });
            meter.Registers.Add(register);
        }

        var wizard = Create(meter);
        Assert.Equal(2, wizard.Registers.Count);
        wizard.Registers[0].FinalText = "110";
        wizard.Registers[1].FinalText = "55";
        wizard.Registers[1].InitialText = "1,5";
        Advance(wizard);
        Advance(wizard);
        Advance(wizard);
        Advance(wizard);
        await wizard.ReplaceCommand.ExecuteAsync(null);

        var (old, created, _, _) = Assert.Single(_kit.Meters.Replacements);
        Assert.Equal([110m, 55m], old.Registers.Select(r => r.Readings.Single(x => x.Kind == ReadingKind.Final).Value));
        Assert.Equal([0m, 1.5m], created.Registers.Select(r => r.Readings.Single().Value));
        Assert.Equal([RegisterKind.Day, RegisterKind.Night], created.Registers.Select(r => r.Kind));
    }

    private ReplaceMeterWizardViewModel AtNewMeterStep()
    {
        var wizard = Create(StoredMeter());
        wizard.Registers[0].FinalText = "202";
        Advance(wizard);
        Advance(wizard);
        Assert.Equal(ReplaceMeterStep.NewMeter, wizard.Step);
        return wizard;
    }

    private ReplaceMeterWizardViewModel Create(Meter meter, params MeterContractLink[] contracts)
    {
        _kit.Meters.Stored.Add(meter);
        return new ReplaceMeterWizardViewModel(_kit.Localizer, _kit.Clock, _kit.Meters, meter, contracts);
    }

    private MeterContractLink Link(string title, Meter meter, bool linked)
    {
        var contract = new Contract { PropertyId = meter.PropertyId, Title = title, Service = ServiceKind.ColdWater };
        List<ContractMeter> open = linked
            ? [new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 10, 1) }]
            : [];
        return new MeterContractLink(contract, open);
    }

    private static void Advance(ReplaceMeterWizardViewModel wizard) => wizard.NextCommand.Execute(null);

    private Meter StoredMeter(bool withReading = true, DateOnly? lastReading = null, int? digits = 5)
    {
        var meter = new Meter
        {
            PropertyId = _kit.Property.Id,
            Resource = MeterResource.ColdWater,
            Unit = MeterUnit.M3,
            SerialNumber = "CW-1",
            Location = "Kitchen",
            Digits = digits,
            VerificationIntervalMonths = 72,
            InstalledOn = new DateOnly(2020, 10, 1),
        };
        var register = MeterRegister.CreateDefault(meter.Id);
        if (withReading)
        {
            register.Readings.Add(new MeterReading
            {
                RegisterId = register.Id,
                Date = lastReading ?? new DateOnly(2026, 9, 26),
                Value = 190m,
            });
        }

        meter.Registers.Add(register);
        return meter;
    }
}
