using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One finished import in the list: what it created and the Undo button.</summary>
public sealed partial class ImportBatchItemViewModel : ObservableObject
{
    private readonly Func<ImportBatchItemViewModel, Task> _undo;

    public ImportBatchItemViewModel(ImportBatch batch, ILocalizer localizer, Func<ImportBatchItemViewModel, Task> undo)
    {
        Batch = batch;
        _undo = undo;
        TitleText = localizer.Get("ImportBatchTitle", batch.FileName, batch.SheetName);
        DetailText = localizer.Get(
            "ImportBatchDetail",
            batch.ImportedAt.ToLocalTime(),
            batch.ReadingCount,
            batch.BillCount,
            batch.PaymentCount,
            batch.SkippedCount,
            batch.ReplacementCount);
        UndoText = localizer["ImportUndo"];
    }

    public ImportBatch Batch { get; }

    public string TitleText { get; }

    public string DetailText { get; }

    public string UndoText { get; }

    [RelayCommand]
    private Task UndoAsync() => _undo(this);
}

/// <summary>A saved import profile in the list: rename, export and delete.</summary>
public sealed partial class ImportProfileItemViewModel : ObservableObject
{
    private readonly Func<ImportProfileItemViewModel, Task> _save;
    private readonly Func<ImportProfileItemViewModel, Task> _export;
    private readonly Func<ImportProfileItemViewModel, Task> _delete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged))]
    private string _name;

    public ImportProfileItemViewModel(
        SavedImportProfile profile,
        ILocalizer localizer,
        Func<ImportProfileItemViewModel, Task> save,
        Func<ImportProfileItemViewModel, Task> export,
        Func<ImportProfileItemViewModel, Task> delete)
    {
        Profile = profile;
        _name = profile.Name;
        _save = save;
        _export = export;
        _delete = delete;
        SheetText = localizer.Get("ImportProfileSheet", profile.Profile.SheetName);
        SaveText = localizer["ImportProfileSave"];
        ExportText = localizer["ImportProfileExport"];
        DeleteText = localizer["ImportProfileDelete"];
    }

    public SavedImportProfile Profile { get; }

    public string SheetText { get; }

    public string SaveText { get; }

    public string ExportText { get; }

    public string DeleteText { get; }

    /// <summary>True while the typed name differs from the saved one (the save button shows).</summary>
    public bool IsChanged => Name.Trim() != Profile.Name && Name.Trim().Length > 0;

    [RelayCommand]
    private Task SaveAsync() => _save(this);

    [RelayCommand]
    private Task ExportAsync() => _export(this);

    [RelayCommand]
    private Task DeleteAsync() => _delete(this);
}

/// <summary>Settings → Imports: the finished imports (each can be undone as a whole) and the saved import profiles.</summary>
public sealed partial class ImportsViewModel : ViewModelBase
{
    private readonly IImportBatchStore _batches;
    private readonly IImportProfileStore _profiles;
    private readonly IImportFileService _files;
    private readonly IFileSaveService _saver;
    private readonly IMeterStore _meters;
    private readonly IContractStore _contracts;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly ISnackbarService _snackbar;
    private readonly ILocalizer _localizer;
    private readonly WorkbookExportFlow _export;
    private IReadOnlyList<MeterLinkRepairItem> _repairItems = [];

    [ObservableProperty]
    private IReadOnlyList<ImportBatchItemViewModel> _batchItems = [];

    [ObservableProperty]
    private IReadOnlyList<ImportProfileItemViewModel> _profileItems = [];

    [ObservableProperty]
    private string _batchesHeader = string.Empty;

    [ObservableProperty]
    private string _batchesEmpty = string.Empty;

    [ObservableProperty]
    private string _profilesHeader = string.Empty;

    [ObservableProperty]
    private string _profilesEmpty = string.Empty;

    [ObservableProperty]
    private string _profilesHint = string.Empty;

    [ObservableProperty]
    private string _importProfileText = string.Empty;

    [ObservableProperty]
    private string _startImportText = string.Empty;

    [ObservableProperty]
    private string _exportExcelText = string.Empty;

    [ObservableProperty]
    private string _exportExcelHint = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _linkRepairLines = [];

    [ObservableProperty]
    private string _linkRepairHeader = string.Empty;

    [ObservableProperty]
    private string _linkRepairHint = string.Empty;

    [ObservableProperty]
    private string _linkRepairNone = string.Empty;

    [ObservableProperty]
    private string _linkRepairButtonText = string.Empty;

    public ImportsViewModel(
        IImportBatchStore batches,
        IImportProfileStore profiles,
        IImportFileService files,
        IFileSaveService saver,
        IMeterStore meters,
        IContractStore contracts,
        IDialogService dialogs,
        INavigationService navigation,
        ISnackbarService snackbar,
        ILocalizer localizer,
        WorkbookExportFlow export)
    {
        _export = export;
        _batches = batches;
        _profiles = profiles;
        _files = files;
        _saver = saver;
        _meters = meters;
        _contracts = contracts;
        _dialogs = dialogs;
        _navigation = navigation;
        _snackbar = snackbar;
        _localizer = localizer;
        RefreshTexts();
    }

    public bool HasBatches => BatchItems.Count > 0;

    public bool HasNoBatches => BatchItems.Count == 0;

    public bool HasProfiles => ProfileItems.Count > 0;

    public bool HasNoProfiles => ProfileItems.Count == 0;

    /// <summary>True while some meter links start after their meter was installed (the repair card offers the fix).</summary>
    public bool HasLinkRepair => LinkRepairLines.Count > 0;

    public bool HasNoLinkRepair => LinkRepairLines.Count == 0;

    partial void OnLinkRepairLinesChanged(IReadOnlyList<string> value)
    {
        OnPropertyChanged(nameof(HasLinkRepair));
        OnPropertyChanged(nameof(HasNoLinkRepair));
    }

    partial void OnBatchItemsChanged(IReadOnlyList<ImportBatchItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasBatches));
        OnPropertyChanged(nameof(HasNoBatches));
    }

    partial void OnProfileItemsChanged(IReadOnlyList<ImportProfileItemViewModel> value)
    {
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(HasNoProfiles));
    }

    public override Task OnAppearingAsync() => RunSafeAsync(ReloadAsync);

    /// <summary>Reloads both lists.</summary>
    public async Task ReloadAsync()
    {
        var batches = await _batches.ListAsync();
        var profiles = await _profiles.ListAsync();
        BatchItems = [.. batches.Select(b => new ImportBatchItemViewModel(b, _localizer, UndoAsync))];
        ProfileItems = [.. profiles.Select(p => new ImportProfileItemViewModel(p, _localizer, SaveNameAsync, ExportAsync, DeleteProfileAsync))];
        await LoadLinkRepairAsync();
    }

    private async Task LoadLinkRepairAsync()
    {
        var contracts = await _contracts.ListAsync();
        var links = new List<ContractMeter>();
        foreach (var contract in contracts)
        {
            links.AddRange(await _contracts.ListContractMetersAsync(contract.Id));
        }

        _repairItems = MeterLinkRepair.Find(await _meters.ListAsync(), contracts, links);
        LinkRepairLines =
        [
            .. _repairItems.Select(i => _localizer.Get(
                "ImportsLinkRepairLine", i.Contract.Title, MeterNames.Title(_localizer, i.Meter), i.Link.ValidFrom, i.NewValidFrom)),
        ];
    }

    /// <summary>After a confirmation moves the start of the listed links to the installation of their meters (or the contract's start).</summary>
    [RelayCommand]
    private Task RepairLinksAsync() => RunSafeAsync(async () =>
    {
        var items = _repairItems;
        if (items.Count == 0)
        {
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            _localizer["ImportsLinkRepairHeader"],
            _localizer["ImportsLinkRepairQuestion"] + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, LinkRepairLines),
            _localizer["Save"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        foreach (var item in items)
        {
            item.Link.ValidFrom = item.NewValidFrom;
            await _contracts.SaveContractMeterAsync(item.Link);
        }

        await LoadLinkRepairAsync();
        _snackbar.Notify(_localizer.Get("ImportsLinkRepairDone", items.Count));
    });

    /// <summary>Opens the import wizard.</summary>
    [RelayCommand]
    private Task StartImportAsync() => RunSafeAsync(() => _navigation.GoToAsync(Routes.ImportWizard));

    /// <summary>Exports the data like the owner's own workbook (one sheet per contract).</summary>
    [RelayCommand]
    private Task ExportExcelAsync() => RunSafeAsync(_export.RunAsync);

    /// <summary>Picks an exported profile file and saves it as a new profile.</summary>
    [RelayCommand]
    private Task ImportProfileAsync() => RunSafeAsync(async () =>
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

        var (name, profile, error) = ImportProfileFile.Read(json);
        if (error is not null)
        {
            await _dialogs.AlertAsync(_localizer["ImportProfilesHeader"], ValidationText.For(_localizer, error));
            return;
        }

        await _profiles.SaveAsync(new SavedImportProfile { Name = name, Profile = profile });
        await ReloadAsync();
        _snackbar.Notify(_localizer.Get("ImportProfileImported", name));
    });

    protected override void RefreshTexts()
    {
        Title = _localizer["ImportsTitle"];
        BatchesHeader = _localizer["ImportBatchesHeader"];
        BatchesEmpty = _localizer["ImportBatchesEmpty"];
        ProfilesHeader = _localizer["ImportProfilesHeader"];
        ProfilesEmpty = _localizer["ImportProfilesEmpty"];
        ProfilesHint = _localizer["ImportProfilesHint"];
        ImportProfileText = _localizer["ImportProfileImport"];
        StartImportText = _localizer["SectionImport"];
        ExportExcelText = _localizer["ExportExcel"];
        ExportExcelHint = _localizer["ExportExcelHint"];
        LinkRepairHeader = _localizer["ImportsLinkRepairHeader"];
        LinkRepairHint = _localizer["ImportsLinkRepairHint"];
        LinkRepairNone = _localizer["ImportsLinkRepairNone"];
        LinkRepairButtonText = _localizer["ImportsLinkRepairButton"];
    }

    private Task UndoAsync(ImportBatchItemViewModel item) => RunSafeAsync(async () =>
    {
        var batch = item.Batch;
        var confirmed = await _dialogs.ConfirmAsync(
            _localizer["ImportUndo"],
            _localizer.Get("ImportUndoConfirm", batch.FileName, batch.SheetName, batch.ReadingCount, batch.BillCount, batch.PaymentCount, batch.ReplacementCount),
            _localizer["ImportUndo"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        var result = await _batches.UndoAsync(batch.Id, confirmEdited: false);
        if (result.Outcome == UndoImportOutcome.NeedsConfirmation)
        {
            var anyway = await _dialogs.ConfirmAsync(
                _localizer["ImportUndo"],
                _localizer["ImportUndoEdited"],
                _localizer["ImportUndoAnyway"],
                _localizer["Cancel"]);
            if (!anyway)
            {
                return;
            }

            result = await _batches.UndoAsync(batch.Id, confirmEdited: true);
        }

        await ReloadAsync();
        if (result.Outcome == UndoImportOutcome.Done)
        {
            _snackbar.Notify(_localizer["ImportUndone"]);
        }
    });

    private Task SaveNameAsync(ImportProfileItemViewModel item) => RunSafeAsync(async () =>
    {
        var name = item.Name.Trim();
        if (name.Length == 0)
        {
            return;
        }

        item.Profile.Name = name.Length > InputLimits.Name ? name[..InputLimits.Name] : name;
        await _profiles.SaveAsync(item.Profile);
        await ReloadAsync();
    });

    private Task ExportAsync(ImportProfileItemViewModel item) => RunSafeAsync(async () =>
    {
        var json = ImportProfileFile.Write(item.Profile.Name, item.Profile.Profile);
        var result = await _saver.SaveAsync(
            ImportProfileFile.SuggestFileName(item.Profile.Name),
            ImportProfileFile.ContentType,
            "Imports",
            async stream =>
            {
                var bytes = new UTF8Encoding(false).GetBytes(json);
                await stream.WriteAsync(bytes);
            });
        if (result.Saved)
        {
            _snackbar.Notify(result.Location is { } location ? _localizer.Get("ImportProfileSavedTo", location) : _localizer["ImportProfileSaved"]);
        }
    });

    private Task DeleteProfileAsync(ImportProfileItemViewModel item) => RunSafeAsync(async () =>
    {
        var confirmed = await _dialogs.ConfirmAsync(
            _localizer.Get("Dialog_Delete_Title", item.Profile.Name),
            _localizer["Dialog_Delete_Message"],
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        await _profiles.DeleteAsync(item.Profile.Id);
        await ReloadAsync();
    });
}
