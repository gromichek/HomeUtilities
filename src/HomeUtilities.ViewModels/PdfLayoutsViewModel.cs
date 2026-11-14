using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>A PDF bill layout in the list: name, its markers in one line and the number of fields, with Delete.</summary>
public sealed partial class PdfLayoutItemViewModel : ObservableObject
{
    private readonly Func<PdfLayoutItemViewModel, Task> _delete;

    public PdfLayoutItemViewModel(PdfBillLayout layout, ILocalizer localizer, Func<PdfLayoutItemViewModel, Task> delete)
    {
        Layout = layout;
        _delete = delete;
        Name = layout.Name;
        MarkersText = localizer.Get("PdfLayoutMarkers", string.Join(" · ", layout.Markers));
        FieldsText = localizer.Get("PdfLayoutFields", layout.Fields.Count);
        DeleteText = localizer["PdfLayoutDelete"];
    }

    public PdfBillLayout Layout { get; }

    public string Name { get; }

    public string MarkersText { get; }

    public string FieldsText { get; }

    public string DeleteText { get; }

    [RelayCommand]
    private Task DeleteAsync() => _delete(this);
}

/// <summary>
/// The "PDF bill layouts" section of the Imports screen: the list, Import layouts… / Export all… (one JSON file) and
/// Show PDF text… Layouts are user data and come as a file; there is no editor.
/// </summary>
public sealed partial class PdfLayoutsViewModel : ViewModelBase
{
    private readonly IPdfBillLayoutStore _layouts;
    private readonly IImportFileService _files;
    private readonly IFileSaveService _saver;
    private readonly IMediaService _media;
    private readonly IPdfTextReader _reader;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly ISnackbarService _snackbar;
    private readonly ILocalizer _localizer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLayouts), nameof(HasNoLayouts))]
    private IReadOnlyList<PdfLayoutItemViewModel> _items = [];

    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _hint = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private string _importText = string.Empty;

    [ObservableProperty]
    private string _exportText = string.Empty;

    [ObservableProperty]
    private string _showTextText = string.Empty;

    public PdfLayoutsViewModel(
        IPdfBillLayoutStore layouts,
        IImportFileService files,
        IFileSaveService saver,
        IMediaService media,
        IPdfTextReader reader,
        IDialogService dialogs,
        INavigationService navigation,
        ISnackbarService snackbar,
        ILocalizer localizer)
    {
        _layouts = layouts;
        _files = files;
        _saver = saver;
        _media = media;
        _reader = reader;
        _dialogs = dialogs;
        _navigation = navigation;
        _snackbar = snackbar;
        _localizer = localizer;
        RefreshTexts();
    }

    public bool HasLayouts => Items.Count > 0;

    public bool HasNoLayouts => Items.Count == 0;

    /// <summary>Reloads the list.</summary>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var layouts = await OffUiAsync(async () => await _layouts.ListAsync(ct), ct);
        ct.ThrowIfCancellationRequested();
        Items = [.. layouts.Select(l => new PdfLayoutItemViewModel(l, _localizer, DeleteAsync))];
    }

    /// <summary>Picks a layouts file; layouts with the name of a stored one replace it, the others are added.</summary>
    [RelayCommand]
    private Task ImportAsync() => RunSafeAsync(async () =>
    {
        var picked = await _files.PickProfileAsync();
        if (picked is null)
        {
            return;
        }

        string json;
        await using (picked.Content)
        {
            using var reader = new StreamReader(picked.Content, Encoding.UTF8);
            json = await reader.ReadToEndAsync();
        }

        var file = PdfBillLayoutFile.Read(json);
        if (!file.Succeeded)
        {
            await _dialogs.AlertAsync(_localizer["PdfLayoutsHeader"], string.Join(Environment.NewLine, file.Errors.Select(e => ValidationText.For(_localizer, e))));
            return;
        }

        var (updated, added) = PdfBillLayoutFile.Merge(await _layouts.ListAsync(), file.Layouts);
        foreach (var layout in updated.Concat(added))
        {
            await _layouts.SaveAsync(layout);
        }

        await ReloadAsync();
        _snackbar.Notify(_localizer.Get("PdfLayoutsImported", added.Count, updated.Count));
    });

    /// <summary>Saves all layouts as one JSON file in the same format.</summary>
    [RelayCommand]
    private Task ExportAsync() => RunSafeAsync(async () =>
    {
        var layouts = await _layouts.ListAsync();
        if (layouts.Count == 0)
        {
            await _dialogs.AlertAsync(_localizer["PdfLayoutsHeader"], _localizer["PdfLayoutsNothingToExport"]);
            return;
        }

        var json = PdfBillLayoutFile.Write(layouts);
        var result = await _saver.SaveAsync(
            PdfBillLayoutFile.SuggestedFileName,
            PdfBillLayoutFile.ContentType,
            "Imports",
            async stream => await stream.WriteAsync(new UTF8Encoding(false).GetBytes(json)));
        if (result.Saved)
        {
            _snackbar.Notify(result.Location is { } location ? _localizer.Get("ImportProfileSavedTo", location) : _localizer["ImportProfileSaved"]);
        }
    });

    /// <summary>Picks a PDF and shows the text the app extracts from it.</summary>
    [RelayCommand]
    private Task ShowTextAsync() => RunSafeAsync(async () =>
    {
        var (file, error) = await PhotoPicking.TryPickAsync(() => _media.PickFileAsync([AttachmentContentTypes.Pdf]), _localizer);
        if (error.Length > 0)
        {
            await _dialogs.AlertAsync(_localizer["PdfTextTitle"], error);
            return;
        }

        if (file is null)
        {
            return;
        }

        PdfTextResult read;
        await using (var content = await file.OpenAsync())
        {
            read = await _reader.ReadAsync(content);
        }

        if (read.Error is { } readError)
        {
            await _dialogs.AlertAsync(_localizer["PdfTextTitle"], _localizer.Get("PdfCannotRead", ValidationText.For(_localizer, readError)));
            return;
        }

        await _navigation.GoToAsync(Routes.PdfText, new Dictionary<string, object> { ["text"] = read.Text });
    });

    protected override void RefreshTexts()
    {
        Header = _localizer["PdfLayoutsHeader"];
        Hint = _localizer["PdfLayoutsHint"];
        EmptyText = _localizer["PdfLayoutsEmpty"];
        ImportText = _localizer["PdfLayoutsImport"];
        ExportText = _localizer["PdfLayoutsExport"];
        ShowTextText = _localizer["PdfLayoutsShowText"];
    }

    private Task DeleteAsync(PdfLayoutItemViewModel item) => RunSafeAsync(async () =>
    {
        var confirmed = await _dialogs.ConfirmAsync(
            _localizer.Get("Dialog_Delete_Title", item.Layout.Name),
            _localizer["Dialog_Delete_Message"],
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        await _layouts.DeleteAsync(item.Layout.Id);
        await ReloadAsync();
    });
}
