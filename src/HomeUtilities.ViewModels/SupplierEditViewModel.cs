using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Editor of one supplier with its web links and bank payment details.</summary>
public sealed partial class SupplierEditViewModel : PaneEditorViewModel
{
    private const string DetailsPrefix = "PaymentDetails.";
    private const string SamplePropertyName = "Sample";
    private const string SampleCustomerNumber = "12345";
    private const decimal SampleAmount = 59.49m;

    private static readonly Regex RowErrorField = new(@"^Links\[(\d+)\]\.", RegexOptions.Compiled);

    private readonly ISupplierStore _store;
    private readonly IClock _clock;
    private readonly ILocalizer _localizer;
    private readonly ILauncherService _launcher;
    private readonly IClipboardService _clipboard;
    private readonly IToastService _toast;
    private readonly List<LinkRowViewModel> _rows = [];

    private Supplier _supplier = new();
    private Supplier? _pending;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _registrationNumber = string.Empty;

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private string _recipientName = string.Empty;

    [ObservableProperty]
    private string _recipientRegistrationNumber = string.Empty;

    /// <summary>The IBAN as typed; shown grouped by four after loading, saving and <see cref="FormatIbanCommand"/>.</summary>
    [ObservableProperty]
    private string _ibanText = string.Empty;

    [ObservableProperty]
    private string _bic = string.Empty;

    [ObservableProperty]
    private string _bankName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PurposePreview), nameof(HasPurposePreview))]
    private string _purposeTemplate = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoLinks))]
    private IReadOnlyList<LinkRowViewModel> _links = [];

    [ObservableProperty]
    private string _generalHeader = string.Empty;

    [ObservableProperty]
    private string _nameLabel = string.Empty;

    [ObservableProperty]
    private string _registrationNumberLabel = string.Empty;

    [ObservableProperty]
    private string _phoneLabel = string.Empty;

    [ObservableProperty]
    private string _emailLabel = string.Empty;

    [ObservableProperty]
    private string _notesLabel = string.Empty;

    [ObservableProperty]
    private string _linksHeader = string.Empty;

    [ObservableProperty]
    private string _linksEmptyText = string.Empty;

    [ObservableProperty]
    private string _addLinkText = string.Empty;

    [ObservableProperty]
    private string _paymentHeader = string.Empty;

    [ObservableProperty]
    private string _recipientNameLabel = string.Empty;

    [ObservableProperty]
    private string _recipientRegistrationNumberLabel = string.Empty;

    [ObservableProperty]
    private string _ibanLabel = string.Empty;

    [ObservableProperty]
    private string _bicLabel = string.Empty;

    [ObservableProperty]
    private string _bankNameLabel = string.Empty;

    [ObservableProperty]
    private string _purposeTemplateLabel = string.Empty;

    [ObservableProperty]
    private string _purposeHintText = string.Empty;

    [ObservableProperty]
    private string _exampleLabel = string.Empty;

    [ObservableProperty]
    private string _copyIbanText = string.Empty;

    [ObservableProperty]
    private string _copyRecipientText = string.Empty;

    [ObservableProperty]
    private string _copyPurposeText = string.Empty;

    public SupplierEditViewModel(
        ISupplierStore store,
        IClock clock,
        ILocalizer localizer,
        IDialogService dialogs,
        INavigationService navigation,
        ILauncherService launcher,
        IClipboardService clipboard,
        IToastService toast)
        : base(localizer, dialogs, navigation)
    {
        _store = store;
        _clock = clock;
        _localizer = localizer;
        _launcher = launcher;
        _clipboard = clipboard;
        _toast = toast;
        RefreshTexts();
    }

    public override Guid EntityId => _supplier.Id;

    public bool HasNoLinks => Links.Count == 0;

    public string NameError => Lookup(nameof(Supplier.Name));

    public string NotesError => Lookup(nameof(Supplier.Notes));

    public string RecipientNameError => Lookup(DetailsPrefix + nameof(PaymentDetails.RecipientName));

    public string RecipientRegistrationNumberError => Lookup(DetailsPrefix + nameof(PaymentDetails.RecipientRegistrationNumber));

    public string IbanError => Lookup(DetailsPrefix + nameof(PaymentDetails.Iban));

    public string BicError => Lookup(DetailsPrefix + nameof(PaymentDetails.Bic));

    public string BankNameError => Lookup(DetailsPrefix + nameof(PaymentDetails.BankName));

    public string PurposeTemplateError => Lookup(DetailsPrefix + nameof(PaymentDetails.PurposeTemplate));

    /// <summary>The template rendered with sample values (customer "12345", this month, 59.49, "Sample").</summary>
    public string PurposePreview => PaymentPurpose.Render(
        PurposeTemplate,
        new PaymentPurposeContext(SampleCustomerNumber, null, null, YearMonth.From(_clock.Today), SampleAmount, SamplePropertyName));

    public bool HasPurposePreview => PurposeTemplate.Trim().Length > 0;

    public override async Task LoadAsync(Guid? id)
    {
        var previous = SuppressDirty;
        SuppressDirty = true;
        try
        {
            var existing = id is { } value ? await _store.GetAsync(value) : null;
            _supplier = existing ?? new Supplier();
            IsNew = existing is null;
            Name = _supplier.Name;
            RegistrationNumber = _supplier.RegistrationNumber ?? string.Empty;
            Phone = _supplier.Phone ?? string.Empty;
            Email = _supplier.Email ?? string.Empty;
            Notes = _supplier.Notes ?? string.Empty;

            var details = _supplier.PaymentDetails;
            RecipientName = details?.RecipientName ?? string.Empty;
            RecipientRegistrationNumber = details?.RecipientRegistrationNumber ?? string.Empty;
            IbanText = details?.Iban is { Length: > 0 } iban ? Iban.Format(iban) : string.Empty;
            Bic = details?.Bic ?? string.Empty;
            BankName = details?.BankName ?? string.Empty;
            PurposeTemplate = details?.PurposeTemplate ?? string.Empty;

            _rows.Clear();
            foreach (var link in _supplier.Links.Where(l => !l.IsDeleted).OrderBy(l => l.SortOrder))
            {
                _rows.Add(CreateRow(link));
            }

            ClearErrors();
            RebuildLinks();
            Title = TitleText();
            IsDirty = false;
        }
        finally
        {
            SuppressDirty = previous;
        }
    }

    /// <summary>Groups the typed IBAN by four characters; bound to the field losing focus.</summary>
    [RelayCommand]
    private void FormatIban()
    {
        var formatted = Iban.Format(IbanText);
        if (formatted != IbanText)
        {
            IbanText = formatted;
        }
    }

    [RelayCommand]
    private Task CopyIbanAsync() => CopyAsync(Iban.Normalize(IbanText));

    [RelayCommand]
    private Task CopyRecipientAsync() => CopyAsync(RecipientName.Trim());

    [RelayCommand]
    private Task CopyPurposeAsync() => CopyAsync(PurposePreview);

    [RelayCommand]
    private async Task AddLinkAsync()
    {
        var editor = new LinkEditorViewModel(_localizer, null);
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        _rows.Add(CreateRow(result));
        RebuildLinks();
        MarkDirty();
    }

    [RelayCommand]
    private async Task EditLinkAsync(LinkRowViewModel row)
    {
        var current = new Link { Kind = row.Kind, Title = row.Title, Url = row.Url };
        var editor = new LinkEditorViewModel(_localizer, current);
        if (!await Dialogs.ShowEditorAsync(editor) || editor.Result is not { } result)
        {
            return;
        }

        row.Kind = result.Kind;
        row.Title = result.Title;
        row.Url = result.Url;
        row.ErrorText = string.Empty;
        RebuildLinks();
        MarkDirty();
    }

    [RelayCommand]
    private async Task RemoveLinkAsync(LinkRowViewModel row)
    {
        var confirmed = await Dialogs.ConfirmAsync(
            _localizer["Dialog_DeleteRow_Title"],
            _localizer.Get("Dialog_DeleteRow_Message", row.DisplayText),
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _rows.Remove(row);
        RebuildLinks();
        MarkDirty();
    }

    /// <summary>Opens the link in the system browser; a URL that is not http/https shows the validation message instead.</summary>
    [RelayCommand]
    private async Task OpenLinkAsync(LinkRowViewModel row)
    {
        var uri = new Link { Url = row.Url.Trim() }.TryGetUri();
        if (uri is null)
        {
            row.ErrorText = ValidationText.For(_localizer, new ValidationError(ErrorCodes.LinkUrlInvalid, nameof(Link.Url)));
            return;
        }

        row.ErrorText = string.Empty;
        await _launcher.OpenAsync(uri);
    }

    protected override IReadOnlyList<ValidationError> Validate()
    {
        _pending = BuildSupplier();
        return SupplierValidator.Validate(_pending).Errors;
    }

    protected override async Task SaveCoreAsync()
    {
        var pending = _pending ?? BuildSupplier();
        await _store.SaveAsync(pending);
        _supplier = pending;
        IsNew = false;
        Title = TitleText();
        FormatIban();
    }

    protected override Task DeleteCoreAsync() => _store.DeleteAsync(_supplier.Id);

    protected override string DeleteName() => _supplier.Name;

    protected override void OnErrorsChanged()
    {
        OnPropertyChanged(nameof(NameError));
        OnPropertyChanged(nameof(NotesError));
        OnPropertyChanged(nameof(RecipientNameError));
        OnPropertyChanged(nameof(RecipientRegistrationNumberError));
        OnPropertyChanged(nameof(IbanError));
        OnPropertyChanged(nameof(BicError));
        OnPropertyChanged(nameof(BankNameError));
        OnPropertyChanged(nameof(PurposeTemplateError));

        foreach (var row in _rows)
        {
            row.ErrorText = string.Empty;
        }

        foreach (var (field, message) in Errors)
        {
            var match = RowErrorField.Match(field);
            if (match.Success && int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) is var index && index < _rows.Count)
            {
                var row = _rows[index];
                row.ErrorText = row.ErrorText.Length == 0 ? message : row.ErrorText + "\n" + message;
            }
        }
    }

    partial void OnNameChanged(string value) => MarkDirty();

    partial void OnRegistrationNumberChanged(string value) => MarkDirty();

    partial void OnPhoneChanged(string value) => MarkDirty();

    partial void OnEmailChanged(string value) => MarkDirty();

    partial void OnNotesChanged(string value) => MarkDirty();

    partial void OnRecipientNameChanged(string value) => MarkDirty();

    partial void OnRecipientRegistrationNumberChanged(string value) => MarkDirty();

    partial void OnIbanTextChanged(string value) => MarkDirty();

    partial void OnBicChanged(string value) => MarkDirty();

    partial void OnBankNameChanged(string value) => MarkDirty();

    partial void OnPurposeTemplateChanged(string value) => MarkDirty();

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        GeneralHeader = _localizer["SupplierGeneral"];
        NameLabel = _localizer["SupplierName"];
        RegistrationNumberLabel = _localizer["SupplierRegistrationNumber"];
        PhoneLabel = _localizer["SupplierPhone"];
        EmailLabel = _localizer["SupplierEmail"];
        NotesLabel = _localizer["SupplierNotes"];
        LinksHeader = _localizer["LinksHeader"];
        LinksEmptyText = _localizer["LinksEmpty"];
        AddLinkText = _localizer["LinkAdd"];
        PaymentHeader = _localizer["PaymentDetailsHeader"];
        RecipientNameLabel = _localizer["PaymentRecipient"];
        RecipientRegistrationNumberLabel = _localizer["PaymentRecipientRegistrationNumber"];
        IbanLabel = _localizer["PaymentIban"];
        BicLabel = _localizer["PaymentBic"];
        BankNameLabel = _localizer["PaymentBank"];
        PurposeTemplateLabel = _localizer["PaymentPurposeTemplate"];
        PurposeHintText = _localizer["PaymentPurposeHint"];
        ExampleLabel = _localizer["PaymentExample"];
        CopyIbanText = _localizer["PaymentCopyIban"];
        CopyRecipientText = _localizer["PaymentCopyRecipient"];
        CopyPurposeText = _localizer["PaymentCopyPurpose"];
        DeleteWholeText = _localizer["SupplierDelete"];
        Title = TitleText();
        RebuildLinks();
    }

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;

    private string TitleText() => IsNew ? _localizer["SupplierNew"] : _supplier.Name;

    private async Task CopyAsync(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        await _clipboard.SetTextAsync(text);
        await _toast.ShowAsync(_localizer["Copied"]);
    }

    private LinkRowViewModel CreateRow(Link link)
    {
        var row = new LinkRowViewModel
        {
            Id = link.Id,
            CreatedAt = link.CreatedAt,
            Kind = link.Kind,
            Title = link.Title,
            Url = link.Url,
        };
        row.EditCommand = new RelayCommand(() => EditLinkCommand.Execute(row));
        row.RemoveCommand = new RelayCommand(() => RemoveLinkCommand.Execute(row));
        row.OpenCommand = new RelayCommand(() => OpenLinkCommand.Execute(row));
        return row;
    }

    private void RebuildLinks()
    {
        foreach (var row in _rows)
        {
            row.DisplayText = string.IsNullOrWhiteSpace(row.Title) ? EnumText.Get(_localizer, row.Kind) : row.Title;
            row.DeleteText = _localizer["Delete"];
            row.OpenText = _localizer["LinkOpen"];
        }

        Links = [.. _rows];
    }

    private Supplier BuildSupplier()
    {
        var supplier = new Supplier
        {
            Id = _supplier.Id,
            CreatedAt = _supplier.CreatedAt,
            UpdatedAt = _supplier.UpdatedAt,
            Name = Name.Trim(),
            RegistrationNumber = NullIfBlank(RegistrationNumber),
            Phone = NullIfBlank(Phone),
            Email = NullIfBlank(Email),
            Notes = NullIfBlank(Notes),
            PaymentDetails = BuildPaymentDetails(),
        };

        for (var i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            supplier.Links.Add(new Link
            {
                Id = row.Id,
                CreatedAt = row.CreatedAt,
                OwnerId = supplier.Id,
                Kind = row.Kind,
                Title = NullIfBlank(row.Title ?? string.Empty),
                Url = row.Url.Trim(),
                SortOrder = i,
            });
        }

        return supplier;
    }

    /// <summary>All fields blank means "no payment details" (null); the IBAN is stored without spaces.</summary>
    private PaymentDetails? BuildPaymentDetails()
    {
        var details = new PaymentDetails(
            NullIfBlank(RecipientName),
            NullIfBlank(RecipientRegistrationNumber),
            NullIfBlank(Iban.Normalize(IbanText)),
            NullIfBlank(Iban.Normalize(Bic)),
            NullIfBlank(BankName),
            NullIfBlank(PurposeTemplate));
        return details == PaymentDetails.Empty ? null : details;
    }

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
