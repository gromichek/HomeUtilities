using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

public class ReadingHistoryViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 23);

    private readonly ContractTestKit _kit = new(Today);
    private readonly Meter _meter;
    private readonly MeterReading _august;
    private readonly MeterReading _september;
    private readonly MeterReading _october;

    public ReadingHistoryViewModelTests()
    {
        _meter = new Meter
        {
            PropertyId = _kit.Property.Id,
            Resource = MeterResource.ColdWater,
            Unit = MeterUnit.M3,
            Location = "Kitchen",
            Digits = 5,
            InstalledOn = new DateOnly(2020, 1, 1),
        };
        var register = MeterRegister.CreateDefault(_meter.Id);
        _meter.Registers.Add(register);
        _august = Add(register, new DateOnly(2026, 8, 26), 100m);
        _september = Add(register, new DateOnly(2026, 9, 26), 110.5m);
        _october = Add(register, new DateOnly(2026, 10, 20), 125m);
        _kit.Meters.Stored.Add(_meter);
    }

    private static MeterReading Add(MeterRegister register, DateOnly date, decimal value, ReadingKind kind = ReadingKind.Regular)
    {
        var reading = new MeterReading { RegisterId = register.Id, Date = date, Value = value, Kind = kind };
        register.Readings.Add(reading);
        return reading;
    }

    private async Task<ReadingHistoryViewModel> LoadAsync()
    {
        var vm = _kit.CreateHistory();
        await vm.LoadAsync(_meter.Id);
        return vm;
    }

    [Fact]
    public async Task Rows_are_newest_first_with_the_consumption_since_the_previous_reading()
    {
        var vm = await LoadAsync();

        var group = Assert.Single(vm.Groups);
        Assert.False(group.HasHeader);
        Assert.Equal([_october.Id, _september.Id, _august.Id], group.Rows.Select(r => r.Id));
        // 125 - 110.5 = 14.5; 110.5 - 100 = 10.5; the first reading has no predecessor
        Assert.Contains("14", group.Rows[0].ConsumptionText);
        Assert.Contains("10", group.Rows[1].ConsumptionText);
        Assert.False(group.Rows[2].HasConsumption);
        Assert.Contains("Kitchen", vm.Title);
    }

    [Fact]
    public async Task Consumption_uses_the_rollover_of_a_meter_with_digits()
    {
        var register = _meter.Registers[0];
        Add(register, new DateOnly(2026, 10, 22), 4m);
        var vm = await LoadAsync();

        // 4 + 10^5 - 125 = 99879
        var newest = vm.Groups[0].Rows[0];
        Assert.Contains("99879", newest.ConsumptionText);
    }

    [Fact]
    public async Task Consumption_is_empty_when_it_cannot_be_determined()
    {
        _meter.Digits = null;
        Add(_meter.Registers[0], new DateOnly(2026, 10, 22), 4m);
        var vm = await LoadAsync();

        Assert.False(vm.Groups[0].Rows[0].HasConsumption);
    }

    [Fact]
    public async Task Initial_final_kind_and_submission_are_marked()
    {
        var register = _meter.Registers[0];
        _september.SubmittedOn = new DateOnly(2026, 9, 27);
        _september.Note = "after repair";
        Add(register, new DateOnly(2020, 1, 1), 0m, ReadingKind.Initial);
        Add(register, new DateOnly(2026, 10, 21), 126m, ReadingKind.Control);
        var vm = await LoadAsync();

        var rows = vm.Groups[0].Rows;
        Assert.Equal("Enum_ReadingKind_Initial [en]", rows[^1].KindText);
        Assert.Equal("Enum_ReadingKind_Control [en]", rows[0].KindText);
        Assert.False(rows.Single(r => r.Id == _august.Id).HasKind);
        var september = rows.Single(r => r.Id == _september.Id);
        Assert.True(september.HasSubmitted);
        Assert.True(september.HasNote);
        Assert.Equal("after repair", september.Note);
        Assert.False(rows.Single(r => r.Id == _august.Id).HasSubmitted);
    }

    [Fact]
    public async Task Registers_get_a_group_each_labelled_by_kind()
    {
        var night = new MeterRegister { MeterId = _meter.Id, Kind = RegisterKind.Night, Direction = EnergyDirection.Import };
        _meter.Registers.Add(night);
        Add(night, new DateOnly(2026, 9, 26), 50m);
        var vm = await LoadAsync();

        Assert.Equal(["Enum_RegisterKind_Total [en]", "Enum_RegisterKind_Night [en]"], vm.Groups.Select(g => g.Header));
        Assert.Equal(3, vm.Groups[0].Rows.Count);
        Assert.Single(vm.Groups[1].Rows);
    }

    [Fact]
    public async Task Empty_history_says_so()
    {
        _meter.Registers[0].Readings.Clear();
        var vm = await LoadAsync();

        Assert.True(vm.IsEmpty);
    }

    [Fact]
    public async Task Edit_saves_the_changed_reading_and_tells_the_screen_behind()
    {
        var vm = await LoadAsync();
        var changed = 0;
        vm.Changed = () =>
        {
            changed++;
            return Task.CompletedTask;
        };
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = Assert.IsType<ReadingEditorViewModel>(editor);
            dialog.ValueText = "111,25";
            dialog.Note = "  corrected ";
            dialog.HasSubmitted = true;
            dialog.SubmittedOn = new DateOnly(2026, 9, 28);
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        var update = Assert.Single(Assert.Single(_kit.Meters.UpdatedBatches));
        Assert.Equal((_september.Id, 111.25m, "corrected", new DateOnly(2026, 9, 28)), (update.Id, update.Value, update.Note, update.SubmittedOn));
        Assert.Equal(1, changed);
        Assert.Contains("111", vm.Groups[0].Rows[1].ValueText);
    }

    [Fact]
    public async Task Edit_cancelled_changes_nothing()
    {
        var vm = await LoadAsync();

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.Empty(_kit.Meters.UpdatedBatches);
    }

    [Fact]
    public async Task Edit_shows_the_validation_error_for_a_duplicate_date_and_kind_and_does_not_close()
    {
        var vm = await LoadAsync();
        ReadingEditorViewModel? dialog = null;
        _kit.Dialogs.EditorScript = editor =>
        {
            dialog = (ReadingEditorViewModel)editor;
            dialog.Date = _august.Date;
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.NotNull(dialog);
        Assert.False(dialog.Saved);
        Assert.Contains("Error_meter_reading_duplicate_on_date", dialog.DateError);
        Assert.Empty(_kit.Meters.UpdatedBatches);
    }

    [Fact]
    public async Task Edit_keeps_the_own_date_without_a_false_duplicate_error()
    {
        var vm = await LoadAsync();
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (ReadingEditorViewModel)editor;
            dialog.ValueText = "111";
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.Single(_kit.Meters.UpdatedBatches);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-5")]
    public async Task Edit_with_a_bad_value_shows_a_message(string text)
    {
        var vm = await LoadAsync();
        ReadingEditorViewModel? dialog = null;
        _kit.Dialogs.EditorScript = editor =>
        {
            dialog = (ReadingEditorViewModel)editor;
            dialog.ValueText = text;
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.False(dialog!.Saved);
        Assert.NotEmpty(dialog.ValueError);
    }

    [Fact]
    public async Task Edit_rejects_a_submission_date_before_the_reading_date()
    {
        var vm = await LoadAsync();
        ReadingEditorViewModel? dialog = null;
        _kit.Dialogs.EditorScript = editor =>
        {
            dialog = (ReadingEditorViewModel)editor;
            dialog.HasSubmitted = true;
            dialog.SubmittedOn = _september.Date.AddDays(-1);
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.False(dialog!.Saved);
        Assert.Contains("Error_reading_edit_submitted_before_date", dialog.SubmittedError);
    }

    [Fact]
    public async Task Edit_can_change_the_kind_of_a_regular_reading()
    {
        var vm = await LoadAsync();
        _kit.Dialogs.EditorScript = editor =>
        {
            var dialog = (ReadingEditorViewModel)editor;
            Assert.True(dialog.IsKindEditable);
            Assert.Equal([ReadingKind.Regular, ReadingKind.Control, ReadingKind.Estimated], dialog.Kinds.Select(k => (ReadingKind)k.Value));
            dialog.SelectedKindIndex = 1;
            dialog.SaveCommand.Execute(null);
        };

        await vm.Groups[0].Rows[1].EditCommand.ExecuteAsync(null);

        Assert.Equal(ReadingKind.Control, Assert.Single(Assert.Single(_kit.Meters.UpdatedBatches)).Kind);
    }

    [Fact]
    public async Task Initial_and_final_readings_keep_their_kind_and_cannot_be_deleted()
    {
        var register = _meter.Registers[0];
        var initial = Add(register, new DateOnly(2020, 1, 1), 0m, ReadingKind.Initial);
        var vm = await LoadAsync();
        var row = vm.Groups[0].Rows[^1];
        Assert.Equal(initial.Id, row.Id);
        Assert.False(row.CanDelete);

        ReadingEditorViewModel? dialog = null;
        _kit.Dialogs.EditorScript = editor =>
        {
            dialog = (ReadingEditorViewModel)editor;
            dialog.ValueText = "1";
            dialog.SaveCommand.Execute(null);
        };
        await row.EditCommand.ExecuteAsync(null);
        Assert.False(dialog!.IsKindEditable);
        Assert.True(dialog.IsKindFixed);
        Assert.Equal(ReadingKind.Initial, Assert.Single(Assert.Single(_kit.Meters.UpdatedBatches)).Kind);

        await row.DeleteCommand.ExecuteAsync(null);
        Assert.Empty(_kit.Meters.DeletedReadings);
        Assert.Single(_kit.Dialogs.Alerts);
        Assert.Empty(_kit.Dialogs.Confirmations);
    }

    [Fact]
    public async Task Delete_asks_first_and_only_deletes_when_confirmed()
    {
        var vm = await LoadAsync();
        _kit.Dialogs.ConfirmResult = false;

        await vm.Groups[0].Rows[1].DeleteCommand.ExecuteAsync(null);

        Assert.Single(_kit.Dialogs.Confirmations);
        Assert.Empty(_kit.Meters.DeletedReadings);

        _kit.Dialogs.ConfirmResult = true;
        await vm.Groups[0].Rows[1].DeleteCommand.ExecuteAsync(null);

        Assert.Equal([_september.Id], _kit.Meters.DeletedReadings);
        Assert.Equal([_october.Id, _august.Id], vm.Groups[0].Rows.Select(r => r.Id));
        // the consumption of the row after the deleted one is recalculated: 125 - 100 = 25
        Assert.Contains("25", vm.Groups[0].Rows[0].ConsumptionText);
    }
}
