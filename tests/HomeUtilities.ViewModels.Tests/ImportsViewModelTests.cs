using System.Text;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Import;

namespace HomeUtilities.ViewModels.Tests;

public class ImportsViewModelTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeImportBatchStore _batches = new();
    private readonly FakeImportProfileStore _profiles = new();
    private readonly FakeImportFilePicker _files = new();
    private readonly FakeSaver _saver = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeSnackbarService _snackbar = new();

    private ImportsViewModel Create() =>
        new(_batches, _profiles, _files, _saver, _dialogs, _navigation, _snackbar, new FakeLocalizer(_language));

    private ImportBatch AddBatch(string file = "book.xlsx", string sheet = "Water")
    {
        var batch = new ImportBatch { FileName = file, SheetName = sheet, ReadingCount = 8, BillCount = 3, PaymentCount = 2, SkippedCount = 1, ImportedAt = new DateTimeOffset(2026, 10, 29, 8, 0, 0, TimeSpan.Zero) };
        _batches.Batches.Add(batch);
        return batch;
    }

    private SavedImportProfile AddProfile(string name = "Water sheet")
    {
        var profile = new SavedImportProfile { Name = name, Profile = new ImportProfile { SheetName = "Water", PeriodColumn = 1, TotalColumn = 4, ContractId = Guid.NewGuid() } };
        _profiles.Stored.Add(profile);
        return profile;
    }

    [Fact]
    public async Task The_lists_show_the_batches_and_profiles()
    {
        AddBatch();
        AddProfile();
        var vm = Create();

        await vm.OnAppearingAsync();

        var batch = Assert.Single(vm.BatchItems);
        Assert.Equal("ImportBatchTitle book.xlsx|Water [en]", batch.TitleText);
        Assert.Contains("|8|3|2|1", batch.DetailText);
        Assert.Equal("Water sheet", Assert.Single(vm.ProfileItems).Name);
        Assert.True(vm.HasBatches);
        Assert.True(vm.HasProfiles);
        Assert.False(vm.HasNoBatches);
    }

    [Fact]
    public async Task Empty_lists_say_so()
    {
        var vm = Create();

        await vm.OnAppearingAsync();

        Assert.True(vm.HasNoBatches);
        Assert.True(vm.HasNoProfiles);
    }

    [Fact]
    public async Task Undo_asks_first_and_undoes_the_batch()
    {
        var batch = AddBatch();
        var vm = Create();
        await vm.OnAppearingAsync();

        await vm.BatchItems[0].UndoCommand.ExecuteAsync(null);

        Assert.Equal("ImportUndoConfirm book.xlsx|Water|8|3|2 [en]", Assert.Single(_dialogs.Confirmations).Message);
        Assert.Equal([(batch.Id, false)], _batches.UndoCalls);
        Assert.Empty(vm.BatchItems);
        Assert.Equal(["ImportUndone [en]"], _snackbar.Messages);
    }

    [Fact]
    public async Task Cancelling_the_undo_confirmation_changes_nothing()
    {
        AddBatch();
        var vm = Create();
        await vm.OnAppearingAsync();
        _dialogs.ConfirmResult = false;

        await vm.BatchItems[0].UndoCommand.ExecuteAsync(null);

        Assert.Empty(_batches.UndoCalls);
        Assert.Single(vm.BatchItems);
    }

    [Fact]
    public async Task Edited_records_ask_a_second_question_and_undo_anyway_on_yes()
    {
        var batch = AddBatch();
        _batches.UndoResults.Enqueue(new UndoImportResult(UndoImportOutcome.NeedsConfirmation, 2));
        var vm = Create();
        await vm.OnAppearingAsync();

        await vm.BatchItems[0].UndoCommand.ExecuteAsync(null);

        Assert.Equal(2, _dialogs.Confirmations.Count);
        Assert.Equal("ImportUndoEdited [en]", _dialogs.Confirmations[1].Message);
        Assert.Equal([(batch.Id, false), (batch.Id, true)], _batches.UndoCalls);
        Assert.Empty(vm.BatchItems);
    }

    [Fact]
    public async Task Edited_records_and_a_no_keep_the_batch()
    {
        AddBatch();
        _batches.UndoResults.Enqueue(new UndoImportResult(UndoImportOutcome.NeedsConfirmation, 1));
        _dialogs.ConfirmAnswers.Enqueue(true);
        _dialogs.ConfirmAnswers.Enqueue(false);
        var vm = Create();
        await vm.OnAppearingAsync();

        await vm.BatchItems[0].UndoCommand.ExecuteAsync(null);

        Assert.Single(_batches.UndoCalls);
        Assert.Single(vm.BatchItems);
        Assert.Empty(_snackbar.Messages);
    }

    [Fact]
    public async Task Renaming_a_profile_saves_the_new_name_and_the_button_shows_only_when_changed()
    {
        var profile = AddProfile();
        var vm = Create();
        await vm.OnAppearingAsync();
        var item = vm.ProfileItems[0];
        Assert.False(item.IsChanged);

        item.Name = "  Water 2014  ";
        Assert.True(item.IsChanged);
        await item.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Water 2014", profile.Name);
        Assert.Equal("Water 2014", vm.ProfileItems[0].Name);
        Assert.False(vm.ProfileItems[0].IsChanged);
    }

    [Fact]
    public async Task A_blank_name_is_not_saved()
    {
        var profile = AddProfile();
        var vm = Create();
        await vm.OnAppearingAsync();
        vm.ProfileItems[0].Name = "   ";

        Assert.False(vm.ProfileItems[0].IsChanged);
        await vm.ProfileItems[0].SaveCommand.ExecuteAsync(null);

        Assert.Equal("Water sheet", profile.Name);
    }

    [Fact]
    public async Task Deleting_a_profile_asks_and_removes_it()
    {
        AddProfile();
        var vm = Create();
        await vm.OnAppearingAsync();

        await vm.ProfileItems[0].DeleteCommand.ExecuteAsync(null);

        Assert.Single(_dialogs.Confirmations);
        Assert.Empty(_profiles.Stored);
        Assert.Empty(vm.ProfileItems);
    }

    [Fact]
    public async Task Cancelling_the_profile_delete_keeps_it()
    {
        AddProfile();
        _dialogs.ConfirmResult = false;
        var vm = Create();
        await vm.OnAppearingAsync();

        await vm.ProfileItems[0].DeleteCommand.ExecuteAsync(null);

        Assert.Single(_profiles.Stored);
    }

    [Fact]
    public async Task Exporting_a_profile_writes_json_without_amounts_to_the_save_dialog()
    {
        AddProfile("Water sheet");
        var vm = Create();
        await vm.OnAppearingAsync();

        await vm.ProfileItems[0].ExportCommand.ExecuteAsync(null);

        var saved = Assert.Single(_saver.Saved);
        Assert.Equal(("Water sheet.import-profile.json", "application/json", "Imports"), (saved.FileName, saved.ContentType, saved.Folder));
        var (name, profile, error) = ImportProfileFile.Read(saved.Content);
        Assert.Null(error);
        Assert.Equal(("Water sheet", 1, 4), (name, profile.PeriodColumn, profile.TotalColumn));
        Assert.Equal(["ImportProfileSaved [en]"], _snackbar.Messages);
    }

    [Fact]
    public async Task Importing_a_profile_file_saves_it_as_a_new_profile()
    {
        var json = ImportProfileFile.Write("Shared water", new ImportProfile { SheetName = "Water", PeriodColumn = 2, TotalColumn = 5 });
        _files.Profile = new ImportPickedFile("p.json", new MemoryStream(Encoding.UTF8.GetBytes(json)));
        var vm = Create();

        await vm.ImportProfileCommand.ExecuteAsync(null);

        var saved = Assert.Single(_profiles.Stored);
        Assert.Equal(("Shared water", 2, 5), (saved.Name, saved.Profile.PeriodColumn, saved.Profile.TotalColumn));
        Assert.Single(vm.ProfileItems);
    }

    [Fact]
    public async Task A_file_that_is_not_a_profile_is_reported_and_nothing_is_saved()
    {
        _files.Profile = new ImportPickedFile("p.json", new MemoryStream("{ nonsense"u8.ToArray()));
        var vm = Create();

        await vm.ImportProfileCommand.ExecuteAsync(null);

        Assert.Empty(_profiles.Stored);
        Assert.Contains("Error_import_profile_file_invalid", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task Start_import_opens_the_wizard()
    {
        var vm = Create();

        await vm.StartImportCommand.ExecuteAsync(null);

        Assert.Equal(Routes.ImportWizard, Assert.Single(_navigation.Visited).Route);
    }

    private sealed class FakeImportFilePicker : IImportFileService
    {
        public ImportPickedFile? Profile { get; set; }

        public Task<ImportPickedFile?> PickWorkbookAsync() => Task.FromResult<ImportPickedFile?>(null);

        public Task<ImportPickedFile?> PickProfileAsync() => Task.FromResult(Profile);
    }

    private sealed class FakeSaver : IFileSaveService
    {
        public List<(string FileName, string ContentType, string Folder, string Content)> Saved { get; } = [];

        public async Task<FileSaveResult> SaveAsync(string fileName, string contentType, string folder, Func<Stream, Task> write)
        {
            using var stream = new MemoryStream();
            await write(stream);
            Saved.Add((fileName, contentType, folder, Encoding.UTF8.GetString(stream.ToArray())));
            return new FileSaveResult(true, null);
        }
    }
}
