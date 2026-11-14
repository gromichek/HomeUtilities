using System.Text;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The "PDF bill layouts" section of the Imports screen and the PDF text page (T-055); invented layouts only.</summary>
public class PdfLayoutsViewModelTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakePdfLayoutStore _store = new();
    private readonly FakePdfTextReader _reader = new();
    private readonly FakeMediaService _media = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeNavigationService _navigation = new();
    private readonly FakeSnackbarService _snackbar = new();
    private readonly LayoutFilePicker _files = new();
    private readonly LayoutSaver _saver = new();

    private PdfLayoutsViewModel Create() =>
        new(_store, _files, _saver, _media, _reader, _dialogs, _navigation, _snackbar, new FakeLocalizer(_language));

    private static string FileWith(string name, string pattern = @"(\d+)") =>
        "{ \"version\": 1, \"layouts\": [ { \"name\": \"" + name + "\", \"markers\": [\"Marker\"], \"fields\": [ { \"field\": \"Gross\", \"pattern\": \"" + pattern.Replace("\\", "\\\\") + "\" } ] } ] }";

    private void Offer(string json) => _files.Next = new ImportPickedFile("layouts.json", new MemoryStream(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public async Task TheList_ShowsNameMarkersAndNumberOfFields()
    {
        _store.Stored.Add(new PdfBillLayout { Name = "Example Water", Markers = ["SIA X", "BILL"], Fields = [new(), new()] });
        var vm = Create();

        await vm.ReloadAsync();

        var item = Assert.Single(vm.Items);
        Assert.Equal("Example Water", item.Name);
        Assert.Equal("PdfLayoutMarkers SIA X · BILL [en]", item.MarkersText);
        Assert.Equal("PdfLayoutFields 2 [en]", item.FieldsText);
        Assert.True(vm.HasLayouts);
        Assert.False(vm.HasNoLayouts);
    }

    [Fact]
    public async Task Import_AddsNewLayouts_AndReplacesOnesWithTheSameName()
    {
        var existing = new PdfBillLayout { Name = "Example Water", Markers = ["old"] };
        _store.Stored.Add(existing);
        var vm = Create();
        Offer("{ \"version\": 1, \"layouts\": [ { \"name\": \"example water\", \"markers\": [\"new\"] }, { \"name\": \"Example Heat\", \"markers\": [\"h\"] } ] }");

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Equal(2, _store.Stored.Count);
        Assert.Equal(["new"], _store.Stored.Single(l => l.Id == existing.Id).Markers);
        Assert.Contains(_store.Stored, l => l.Name == "Example Heat");
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal("PdfLayoutsImported 1|1 [en]", Assert.Single(_snackbar.Messages));
    }

    [Fact]
    public async Task Import_OfAnInvalidPattern_SavesNothing_AndNamesTheProblem()
    {
        var vm = Create();
        Offer(FileWith("Bad", "(unclosed"));

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Empty(_store.Stored);
        var (_, message) = Assert.Single(_dialogs.Alerts);
        Assert.Contains("Error_pdf_layout_pattern_invalid", message);
    }

    [Fact]
    public async Task Import_OfAnotherFile_IsRefused()
    {
        var vm = Create();
        Offer("not json at all");

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Empty(_store.Stored);
        Assert.Contains("Error_pdf_layout_file_invalid", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task Import_Cancelled_DoesNothing()
    {
        var vm = Create();

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Empty(_store.Stored);
        Assert.Empty(_dialogs.Alerts);
    }

    [Fact]
    public async Task Export_WritesAllLayoutsAsOneFile_InTheImportFormat()
    {
        _store.Stored.Add(new PdfBillLayout { Name = "One", Markers = ["a"] });
        _store.Stored.Add(new PdfBillLayout { Name = "Two", Markers = ["b"] });
        var vm = Create();

        await vm.ExportCommand.ExecuteAsync(null);

        var saved = Assert.Single(_saver.Saved);
        Assert.Equal(PdfBillLayoutFile.SuggestedFileName, saved.FileName);
        var back = PdfBillLayoutFile.Read(saved.Content);
        Assert.Equal(["One", "Two"], back.Layouts.Select(l => l.Name));
    }

    [Fact]
    public async Task Export_WithoutLayouts_SaysSo()
    {
        var vm = Create();

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.Empty(_saver.Saved);
        Assert.Contains("PdfLayoutsNothingToExport", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task Delete_AsksFirst_ThenRemovesTheLayout()
    {
        var layout = new PdfBillLayout { Name = "Gone", Markers = ["g"] };
        _store.Stored.Add(layout);
        var vm = Create();
        await vm.ReloadAsync();

        _dialogs.ConfirmResult = false;
        await vm.Items[0].DeleteCommand.ExecuteAsync(null);
        Assert.Single(_store.Stored);

        _dialogs.ConfirmResult = true;
        await vm.Items[0].DeleteCommand.ExecuteAsync(null);
        Assert.Empty(_store.Stored);
        Assert.Empty(vm.Items);
    }

    [Fact]
    public async Task ShowText_ReadsThePdf_AndOpensThePageWithTheText()
    {
        _media.Next = FakeMediaService.Photo("bill.pdf", "application/pdf");
        _reader.Text = "line one\nline two\n";
        var vm = Create();

        await vm.ShowTextCommand.ExecuteAsync(null);

        var (route, parameters) = Assert.Single(_navigation.Visited);
        Assert.Equal(Routes.PdfText, route);
        Assert.Equal("line one\nline two\n", parameters!["text"]);
    }

    [Fact]
    public async Task ShowText_OfAnUnreadablePdf_ShowsTheReason()
    {
        _media.Next = FakeMediaService.Photo("bill.pdf", "application/pdf");
        _reader.Error = new ValidationError(ErrorCodes.PdfNoText);
        var vm = Create();

        await vm.ShowTextCommand.ExecuteAsync(null);

        Assert.Empty(_navigation.Visited);
        Assert.Contains("Error_pdf_no_text", Assert.Single(_dialogs.Alerts).Message);
    }

    [Fact]
    public async Task ShowText_Cancelled_DoesNothing()
    {
        var vm = Create();

        await vm.ShowTextCommand.ExecuteAsync(null);

        Assert.Empty(_navigation.Visited);
        Assert.Equal(0, _reader.Calls);
    }

    [Fact]
    public async Task ThePdfTextPage_CopiesTheWholeText()
    {
        var clipboard = new FakeClipboardService();
        var vm = new PdfTextViewModel(new FakeLocalizer(_language), clipboard, _snackbar, _navigation);
        vm.Show("some text\nsecond line");

        await vm.CopyCommand.ExecuteAsync(null);

        Assert.Equal("some text\nsecond line", Assert.Single(clipboard.Texts));
        Assert.Equal("PdfTextCopied [en]", Assert.Single(_snackbar.Messages));
    }

    private sealed class LayoutFilePicker : IImportFileService
    {
        public ImportPickedFile? Next { get; set; }

        public Task<ImportPickedFile?> PickWorkbookAsync() => Task.FromResult<ImportPickedFile?>(null);

        public Task<ImportPickedFile?> PickProfileAsync() => Task.FromResult(Next);
    }

    private sealed class LayoutSaver : IFileSaveService
    {
        public List<(string FileName, string Content)> Saved { get; } = [];

        public async Task<FileSaveResult> SaveAsync(string fileName, string contentType, string folder, Func<Stream, Task> write)
        {
            using var stream = new MemoryStream();
            await write(stream);
            Saved.Add((fileName, Encoding.UTF8.GetString(stream.ToArray())));
            return new FileSaveResult(true, null);
        }
    }
}
