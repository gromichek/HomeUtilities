using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The reading history of one meter: per register the readings with consumption, edit and delete.</summary>
public sealed partial class ReadingHistoryViewModel : ViewModelBase
{
    private readonly IMeterStore _store;
    private readonly IAttachmentStore _attachments;
    private readonly IFileViewer _viewer;
    private readonly IMediaService _media;
    private readonly ILocalizer _localizer;
    private readonly IDialogService _dialogs;

    private Meter? _meter;

    [ObservableProperty]
    private IReadOnlyList<ReadingHistoryGroupViewModel> _groups = [];

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _emptyText = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingText = string.Empty;

    public ReadingHistoryViewModel(
        IMeterStore store,
        IAttachmentStore attachments,
        IFileViewer viewer,
        IMediaService media,
        ILocalizer localizer,
        IDialogService dialogs)
    {
        _store = store;
        _attachments = attachments;
        _viewer = viewer;
        _media = media;
        _localizer = localizer;
        _dialogs = dialogs;
        RefreshTexts();
    }

    public Guid MeterId { get; private set; }

    /// <summary>Called after a reading was edited or deleted, so that the screen that opened the history can refresh.</summary>
    public Func<Task>? Changed { get; set; }

    public bool IsEmpty => !IsLoading && Groups.All(g => g.Rows.Count == 0);

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsEmpty));

    public Task LoadAsync(Guid meterId)
    {
        MeterId = meterId;
        return RunSafeAsync(ReloadAsync);
    }

    /// <summary>Back on the page (e.g. from the meter editor): the readings may have changed meanwhile.</summary>
    public override Task OnAppearingAsync() => MeterId == Guid.Empty ? Task.CompletedTask : RunSafeAsync(ReloadAsync);

    /// <summary>Reads the meter again and rebuilds the rows.</summary>
    public async Task ReloadAsync()
    {
        IsLoading = true;
        try
        {
            _meter = MeterId == Guid.Empty ? null : await _store.GetAsync(MeterId);
            await RebuildAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected override void RefreshTexts()
    {
        EmptyText = _localizer["ReadingHistoryEmpty"];
        LoadingText = _localizer["Loading"];
        if (_meter is not null)
        {
            _ = RunSafeAsync(RebuildAsync);
        }
    }

    private async Task RebuildAsync()
    {
        if (_meter is not { } meter)
        {
            Groups = [];
            Title = _localizer["ReadingHistoryHeader"];
            Subtitle = string.Empty;
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        Title = _localizer.Get("ReadingHistoryTitle", MeterNames.Title(_localizer, meter));
        Subtitle = string.IsNullOrWhiteSpace(meter.SerialNumber) ? string.Empty : MeterNames.Serial(_localizer, meter);
        var chain = new MeterChain([meter]);
        var unit = EnumText.Get(_localizer, meter.Unit);

        // Twenty years of monthly readings per register: the rows are built off the UI thread, and the screen shows a page
        // of them at a time (ReadingHistoryGroupViewModel), so only a few dozen row views are ever created.
        var groups = await Task.Run(() =>
            meter.Registers
                .Where(r => !r.IsDeleted)
                .OrderBy(r => r.Kind)
                .ThenBy(r => r.Direction)
                .Select(register => new ReadingHistoryGroupViewModel(
                    RegisterLabel.For(_localizer, meter, register),
                    BuildRows(meter, register, chain, unit),
                    _localizer))
                .ToList());
        Groups = groups;
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>The rows of a register, newest first, each with the consumption since the reading before it.</summary>
    private List<ReadingHistoryRowViewModel> BuildRows(Meter meter, MeterRegister register, MeterChain chain, string unit)
    {
        var readings = register.Readings.Where(r => !r.IsDeleted).OrderBy(r => r.Date).ThenBy(r => r.CreatedAt).ToList();
        var rows = new List<ReadingHistoryRowViewModel>();
        for (var i = 0; i < readings.Count; i++)
        {
            var reading = readings[i];
            decimal? consumption = null;
            if (i > 0 && reading.Kind != ReadingKind.Initial && readings[i - 1].Date < reading.Date)
            {
                var result = ConsumptionCalculator.Calculate(chain, register.Kind, readings[i - 1].Date, reading.Date);
                consumption = result.IsComplete ? result.Quantity : null;
            }

            var (readingId, registerId) = (reading.Id, register.Id);
            rows.Add(new ReadingHistoryRowViewModel(
                reading,
                consumption is { } q ? $"+ {DecimalInput.Format(q, CultureInfo.CurrentCulture, 3)} {unit}" : string.Empty,
                _localizer,
                new AsyncRelayCommand(() => RunSafeAsync(() => EditAsync(registerId, readingId))),
                new AsyncRelayCommand(() => RunSafeAsync(() => DeleteAsync(registerId, readingId))),
                new AsyncRelayCommand(() => RunSafeAsync(() => OpenPhotoAsync(reading.PhotoAttachmentId)))));
        }

        rows.Reverse();
        return rows;
    }

    private async Task EditAsync(Guid registerId, Guid readingId)
    {
        // Edit a fresh copy so that cancelling leaves the displayed rows untouched.
        var meter = await _store.GetAsync(MeterId);
        var register = meter?.Registers.FirstOrDefault(r => r.Id == registerId);
        var reading = register?.Readings.FirstOrDefault(r => r.Id == readingId && !r.IsDeleted);
        if (meter is null || register is null || reading is null)
        {
            await ReloadAsync();
            return;
        }

        var editor = new ReadingEditorViewModel(_localizer, meter, register, reading, _media);
        if (!await _dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        var previousPhoto = reading.PhotoAttachmentId;
        Guid? addedPhoto = null;
        if (editor.NewPhoto is { } photo)
        {
            try
            {
                await using var content = await photo.OpenAsync();
                var attachment = await _attachments.AddAsync(PhotoPicking.ToAttachment(photo, AttachmentOwnerKind.MeterReading, result.Id), content);
                addedPhoto = attachment.Id;
                result.PhotoAttachmentId = attachment.Id;
            }
            catch (AttachmentRejectedException ex)
            {
                await _dialogs.AlertAsync(_localizer["Dialog_Error_Title"], PhotoPicking.Describe(ex.Result.Errors, _localizer));
                return;
            }
        }

        try
        {
            await _store.UpdateReadingsAsync([result]);
        }
        catch
        {
            if (addedPhoto is { } added)
            {
                await _attachments.DeleteAsync(added);
            }

            throw;
        }

        // A replaced or removed photo is soft-deleted; its file stays for undo and sync.
        if (previousPhoto is { } old && result.PhotoAttachmentId != old)
        {
            await _attachments.DeleteAsync(old);
        }

        await ChangedAsync();
    }

    private async Task OpenPhotoAsync(Guid? attachmentId)
    {
        if (attachmentId is not { } id)
        {
            return;
        }

        var attachment = await _attachments.GetAsync(id);
        var path = attachment is null ? null : await _attachments.GetFilePathAsync(id);
        if (attachment is null || path is null)
        {
            await _dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["PhotoMissing"]);
            return;
        }

        await _viewer.OpenAsync(path, attachment.ContentType);
    }

    private async Task DeleteAsync(Guid registerId, Guid readingId)
    {
        var reading = _meter?.Registers.FirstOrDefault(r => r.Id == registerId)?.Readings.FirstOrDefault(r => r.Id == readingId);
        if (reading is null)
        {
            return;
        }

        if (reading.Kind is ReadingKind.Initial or ReadingKind.Final)
        {
            await _dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["ReadingDeleteBlocked"]);
            return;
        }

        var confirmed = await _dialogs.ConfirmAsync(
            _localizer["Dialog_DeleteRow_Title"],
            _localizer.Get("Dialog_DeleteRow_Message", $"{reading.Date.ToString("d", CultureInfo.CurrentCulture)}: {DecimalInput.Format(reading.Value, CultureInfo.CurrentCulture, 3)}"),
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        await _store.DeleteReadingAsync(readingId);
        await ChangedAsync();
    }

    private async Task ChangedAsync()
    {
        await ReloadAsync();
        if (Changed is { } changed)
        {
            await changed();
        }
    }
}

/// <summary>The readings of one register in the history; <see cref="VisibleRows"/> grows by a page on "Show more".</summary>
public sealed partial class ReadingHistoryGroupViewModel : ObservableObject
{
    /// <summary>How many rows are shown at first and added by each "Show more".</summary>
    public const int PageSize = 30;

    private readonly ILocalizer _localizer;

    public ReadingHistoryGroupViewModel(string header, IReadOnlyList<ReadingHistoryRowViewModel> rows, ILocalizer localizer)
    {
        Header = header;
        Rows = rows;
        _localizer = localizer;
        ShowMoreCommand = new RelayCommand(ShowMore);
        ShowMore();
    }

    public string Header { get; }

    public bool HasHeader => Header.Length > 0;

    /// <summary>Every reading of the register, newest first.</summary>
    public IReadOnlyList<ReadingHistoryRowViewModel> Rows { get; }

    /// <summary>The rows on screen: the newest <see cref="PageSize"/>, then more on request.</summary>
    public ObservableCollection<ReadingHistoryRowViewModel> VisibleRows { get; } = [];

    public bool HasMore => VisibleRows.Count < Rows.Count;

    [ObservableProperty]
    private string _showMoreText = string.Empty;

    public IRelayCommand ShowMoreCommand { get; }

    private void ShowMore()
    {
        foreach (var row in Rows.Skip(VisibleRows.Count).Take(PageSize))
        {
            VisibleRows.Add(row);
        }

        OnPropertyChanged(nameof(HasMore));
        ShowMoreText = _localizer.Get("ReadingHistoryShowMore", Rows.Count - VisibleRows.Count);
    }
}

/// <summary>One reading in the history list.</summary>
public sealed class ReadingHistoryRowViewModel
{
    public ReadingHistoryRowViewModel(
        MeterReading reading,
        string consumptionText,
        ILocalizer localizer,
        IAsyncRelayCommand editCommand,
        IAsyncRelayCommand deleteCommand,
        IAsyncRelayCommand openPhotoCommand)
    {
        Id = reading.Id;
        Kind = reading.Kind;
        DateText = reading.Date.ToString("d", CultureInfo.CurrentCulture);
        ValueText = DecimalInput.Format(reading.Value, CultureInfo.CurrentCulture, 3);
        ConsumptionText = consumptionText;
        KindText = reading.Kind == ReadingKind.Regular ? string.Empty : EnumText.Get(localizer, reading.Kind);
        SubmittedText = reading.SubmittedOn is { } submitted
            ? localizer.Get("ReadingSubmittedOn", submitted.ToString("d", CultureInfo.CurrentCulture))
            : string.Empty;
        Note = reading.Note ?? string.Empty;
        CanDelete = reading.Kind is not (ReadingKind.Initial or ReadingKind.Final);
        DeleteTooltip = localizer["Delete"];
        EditCommand = editCommand;
        DeleteCommand = deleteCommand;
        HasPhoto = reading.PhotoAttachmentId is not null;
        PhotoTooltip = localizer["PhotoOpenTooltip"];
        OpenPhotoCommand = openPhotoCommand;
    }

    public Guid Id { get; }

    public ReadingKind Kind { get; }

    public string DateText { get; }

    public string ValueText { get; }

    /// <summary>"+ 7,903 m³" since the previous reading; empty for the first reading.</summary>
    public string ConsumptionText { get; }

    public bool HasConsumption => ConsumptionText.Length > 0;

    /// <summary>Badge for Initial / Final / Control / Estimated; empty for Regular.</summary>
    public string KindText { get; }

    public bool HasKind => KindText.Length > 0;

    /// <summary>"submitted 23.10.2026"; empty when not submitted.</summary>
    public string SubmittedText { get; }

    public bool HasSubmitted => SubmittedText.Length > 0;

    public string Note { get; }

    public bool HasNote => Note.Length > 0;

    /// <summary>Initial and final readings belong to the meter replacement and are not deleted here.</summary>
    public bool CanDelete { get; }

    public string DeleteTooltip { get; }

    public IAsyncRelayCommand EditCommand { get; }

    public IAsyncRelayCommand DeleteCommand { get; }

    /// <summary>The reading has a photo (photo icon in the row).</summary>
    public bool HasPhoto { get; }

    public string PhotoTooltip { get; }

    /// <summary>Opens the photo in the system viewer.</summary>
    public IAsyncRelayCommand OpenPhotoCommand { get; }
}
