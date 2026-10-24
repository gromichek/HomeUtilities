using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Dialog that edits one stored reading: date, value, kind (Regular / Control / Estimated), submission date and note.
/// Initial and final readings keep their kind. The result is a new <see cref="MeterReading"/>; nothing is stored here.
/// </summary>
public sealed partial class ReadingEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;
    private readonly Meter _meter;
    private readonly MeterRegister _register;
    private readonly MeterReading _original;
    private readonly IMediaService _media;

    [ObservableProperty]
    private DateOnly _date;

    [ObservableProperty]
    private string _valueText;

    [ObservableProperty]
    private bool _hasSubmitted;

    [ObservableProperty]
    private DateOnly _submittedOn;

    [ObservableProperty]
    private string _note;

    [ObservableProperty]
    private string _dateError = string.Empty;

    [ObservableProperty]
    private string _valueError = string.Empty;

    [ObservableProperty]
    private string _kindError = string.Empty;

    [ObservableProperty]
    private string _submittedError = string.Empty;

    [ObservableProperty]
    private string _noteError = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private EnumOption? _selectedKind;

    /// <summary>The photo chosen in this dialog; the caller stores it after Save. Null when the photo was kept or removed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto), nameof(PhotoText))]
    private PickedFile? _newPhoto;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto), nameof(PhotoText))]
    private bool _photoRemoved;

    [ObservableProperty]
    private string _photoError = string.Empty;

    public ReadingEditorViewModel(ILocalizer localizer, Meter meter, MeterRegister register, MeterReading original, IMediaService media)
        : base(localizer)
    {
        _localizer = localizer;
        _media = media;
        _meter = meter;
        _register = register;
        _original = original;

        Title = localizer["ReadingEditTitle"];
        _date = original.Date;
        _valueText = DecimalInput.Format(original.Value, CultureInfo.CurrentCulture, 3);
        _hasSubmitted = original.SubmittedOn is not null;
        _submittedOn = original.SubmittedOn ?? original.Date;
        _note = original.Note ?? string.Empty;

        Kinds =
        [
            .. EnumText.Options<ReadingKind>(localizer)
                .Where(o => o.Value is ReadingKind.Regular or ReadingKind.Control or ReadingKind.Estimated),
        ];
        _selectedKind = Kinds.FirstOrDefault(o => o.Value.Equals(original.Kind));
        FixedKindText = EnumText.Get(localizer, original.Kind);
        RegisterText = RegisterLabel.For(localizer, meter, register);
        DateLabel = localizer["ReadingDateLabel"];
        ValueLabel = localizer["ReadingValueLabel"];
        KindLabel = localizer["ReadingKindLabel"];
        SubmittedLabel = localizer["ReadingSubmittedLabel"];
        SubmittedOnLabel = localizer["ReadingSubmittedOnLabel"];
        NoteLabel = localizer["ReadingNoteLabel"];
        PhotoLabel = localizer["PhotoLabel"];
        TakePhotoText = localizer["PhotoTake"];
        ChoosePhotoText = localizer["PhotoChooseFile"];
        RemovePhotoText = localizer["PhotoRemove"];
    }

    /// <summary>True while the reading has a photo: the stored one (not removed) or a newly chosen one.</summary>
    public bool HasPhoto => NewPhoto is not null || (_original.PhotoAttachmentId is not null && !PhotoRemoved);

    public bool CanTakePhoto => _media.IsCaptureSupported;

    public string PhotoText => NewPhoto is { } photo
        ? _localizer.Get("PhotoNew", photo.FileName)
        : HasPhoto ? _localizer["PhotoAttached"] : _localizer["PhotoNone"];

    public string PhotoLabel { get; }

    public string TakePhotoText { get; }

    public string ChoosePhotoText { get; }

    public string RemovePhotoText { get; }

    /// <summary>Takes a photo with the camera (Android).</summary>
    [RelayCommand]
    private Task TakePhotoAsync() => RunSafeAsync(() => PickAsync(_media.CapturePhotoAsync));

    /// <summary>Picks an image from the gallery or the file system.</summary>
    [RelayCommand]
    private Task ChoosePhotoAsync() => RunSafeAsync(() => PickAsync(_media.PickPhotoAsync));

    [RelayCommand]
    private void RemovePhoto()
    {
        PhotoError = string.Empty;
        NewPhoto = null;
        PhotoRemoved = _original.PhotoAttachmentId is not null;
    }

    private async Task PickAsync(Func<Task<PickedFile?>> pick)
    {
        var (file, error) = await PhotoPicking.TryPickAsync(pick, _localizer);
        PhotoError = error;
        if (file is not null)
        {
            NewPhoto = file;
        }
    }

    /// <summary>The edited reading after a successful Save.</summary>
    public MeterReading? Result { get; private set; }

    public IReadOnlyList<EnumOption> Kinds { get; }

    /// <summary>Initial and final readings keep their kind; the picker is replaced by a fixed text.</summary>
    public bool IsKindEditable => _original.Kind is not (ReadingKind.Initial or ReadingKind.Final);

    public bool IsKindFixed => !IsKindEditable;

    public string FixedKindText { get; }

    /// <summary>"Day" / "Night"; empty for a single Total register.</summary>
    public string RegisterText { get; }

    public bool HasRegisterText => RegisterText.Length > 0;

    public string DateLabel { get; }

    public string ValueLabel { get; }

    public string KindLabel { get; }

    public string SubmittedLabel { get; }

    public string SubmittedOnLabel { get; }

    public string NoteLabel { get; }

    public int SelectedKindIndex
    {
        get => SelectedKind is null ? -1 : Kinds.ToList().IndexOf(SelectedKind);
        set
        {
            if (value >= 0 && value < Kinds.Count)
            {
                SelectedKind = Kinds[value];
            }
        }
    }

    protected override bool TryApply()
    {
        DateError = ValueError = KindError = SubmittedError = NoteError = string.Empty;

        if (!DecimalInput.TryParse(ValueText, CultureInfo.CurrentCulture, out var value))
        {
            ValueError = ValidationText.For(_localizer, new ValidationError(CommonErrorCodes.NumberInvalid, nameof(MeterReading.Value)));
            return false;
        }

        var reading = new MeterReading
        {
            Id = _original.Id,
            CreatedAt = _original.CreatedAt,
            RegisterId = _original.RegisterId,
            Date = Date,
            Value = value,
            Kind = IsKindEditable ? SelectedKind?.As<ReadingKind>() ?? _original.Kind : _original.Kind,
            SubmittedOn = HasSubmitted ? SubmittedOn : null,
            PhotoAttachmentId = PhotoRemoved || NewPhoto is not null ? null : _original.PhotoAttachmentId,
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
        };

        var errors = MeterReadingValidator.Validate(reading, _register, _meter).Errors.ToList();
        if (reading.SubmittedOn is { } submitted && submitted < reading.Date)
        {
            errors.Add(new ValidationError(CommonErrorCodes.ReadingSubmittedBeforeDate, nameof(MeterReading.SubmittedOn)));
        }

        foreach (var group in errors.GroupBy(e => e.Field))
        {
            var text = string.Join("\n", group.Select(e => ValidationText.For(_localizer, e)));
            switch (group.Key)
            {
                case nameof(MeterReading.Date):
                    DateError = text;
                    break;
                case nameof(MeterReading.Kind):
                    KindError = text;
                    break;
                case nameof(MeterReading.SubmittedOn):
                    SubmittedError = text;
                    break;
                case nameof(MeterReading.Note):
                    NoteError = text;
                    break;
                default:
                    ValueError = text;
                    break;
            }
        }

        if (errors.Count > 0)
        {
            return false;
        }

        Result = reading;
        return true;
    }
}
