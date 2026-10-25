using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Backup;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

internal sealed class FakeLanguageService : ILanguageService
{
    public IReadOnlyList<string> SupportedLanguages { get; } = ["en", "ru"];

    public string CurrentLanguage { get; private set; } = "en";

    public List<string> SetCalls { get; } = [];

    public void SetLanguage(string code)
    {
        SetCalls.Add(code);
        CurrentLanguage = code;
        WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(code));
    }
}

internal sealed class FakeLocalizer(FakeLanguageService language) : ILocalizer
{
    /// <summary>Keys for which <see cref="TryGet"/> reports a missing resource.</summary>
    public HashSet<string> MissingKeys { get; } = [];

    public string this[string key] => $"{key} [{language.CurrentLanguage}]";

    public bool TryGet(string key, out string value)
    {
        value = this[key];
        return !MissingKeys.Contains(key);
    }

    public string Get(string key, params object[] args) =>
        $"{key} {string.Join("|", args)} [{language.CurrentLanguage}]";
}

internal sealed class FakeLayoutModeService : ILayoutModeService
{
    public LayoutMode Current { get; private set; } = LayoutMode.Compact;

    public void Change(LayoutMode mode)
    {
        Current = mode;
        WeakReferenceMessenger.Default.Send(new LayoutModeChangedMessage(mode));
    }
}

internal sealed class FakeThemeService : IThemeService
{
    public AppThemeChoice Current { get; private set; } = AppThemeChoice.System;

    public List<AppThemeChoice> SetCalls { get; } = [];

    public void Set(AppThemeChoice theme)
    {
        SetCalls.Add(theme);
        Current = theme;
    }
}

internal sealed class TestClock(DateOnly today) : IClock
{
    public DateOnly Today { get; set; } = today;

    public DateTimeOffset UtcNow => new(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero);
}

internal sealed class FakeDialogService : IDialogService
{
    public bool ConfirmResult { get; set; } = true;

    /// <summary>Answers to the next confirmations, in order; when empty <see cref="ConfirmResult"/> is used.</summary>
    public Queue<bool> ConfirmAnswers { get; } = new();

    public List<(string Title, string Message)> Confirmations { get; } = [];

    /// <summary>The answer to the "Save changes?" dialog.</summary>
    public UnsavedChangesChoice UnsavedChoice { get; set; } = UnsavedChangesChoice.DontSave;

    public int UnsavedAsked { get; private set; }

    /// <summary>The answers to <see cref="ChooseAsync"/>: gets the options, returns the picked one (null = cancelled).</summary>
    public Func<IReadOnlyList<string>, string?>? ChooseScript { get; set; }

    /// <summary>What the "user" does in the next child-item editor dialog; null = just cancel.</summary>
    public Action<EditorDialogViewModel>? EditorScript { get; set; }

    public EditorDialogViewModel? LastEditor { get; private set; }

    public List<(string Title, string Message)> Alerts { get; } = [];

    public Task AlertAsync(string title, string message)
    {
        Alerts.Add((title, message));
        return Task.CompletedTask;
    }

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        Confirmations.Add((title, message));
        return Task.FromResult(ConfirmAnswers.Count > 0 ? ConfirmAnswers.Dequeue() : ConfirmResult);
    }

    public Task<string?> ChooseAsync(string title, IReadOnlyList<string> options) =>
        Task.FromResult(ChooseScript?.Invoke(options));

    public Task<UnsavedChangesChoice> AskUnsavedChangesAsync()
    {
        UnsavedAsked++;
        return Task.FromResult(UnsavedChoice);
    }

    public Task<bool> ShowEditorAsync(EditorDialogViewModel editor)
    {
        LastEditor = editor;
        EditorScript?.Invoke(editor);
        return Task.FromResult(editor.Saved);
    }
}

internal sealed class FakeNavigationService : INavigationService
{
    public List<(string Route, IReadOnlyDictionary<string, object>? Parameters)> Visited { get; } = [];

    public int BackCount { get; private set; }

    /// <summary>The view model whose page is on top; null = none (then <see cref="IsShowing"/> says yes for any editor).</summary>
    public object? Top { get; set; }

    public bool IsShowing(object viewModel) => Top is null || ReferenceEquals(Top, viewModel);

    public Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)
    {
        Visited.Add((route, parameters));
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        BackCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakePropertyStore : IPropertyStore
{
    public List<Property> Stored { get; } = [];

    public List<Property> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<Property>> ListAsync(bool includeArchived, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Property>>([.. Stored.Where(p => includeArchived || !p.IsArchived)]);

    public Task<IReadOnlyList<Property>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Property>>([.. Stored]);

    public Task<Property?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(p => p.Id == id));

    public Task SaveAsync(Property aggregate, CancellationToken ct = default)
    {
        Saved.Add(aggregate);
        Stored.RemoveAll(p => p.Id == aggregate.Id);
        Stored.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        Stored.RemoveAll(p => p.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSupplierStore : ISupplierStore
{
    public List<Supplier> Stored { get; } = [];

    public List<Supplier> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<Supplier>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Supplier>>([.. Stored]);

    public Task<Supplier?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(s => s.Id == id));

    public Task SaveAsync(Supplier aggregate, CancellationToken ct = default)
    {
        Saved.Add(aggregate);
        Stored.RemoveAll(s => s.Id == aggregate.Id);
        Stored.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        Stored.RemoveAll(s => s.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCategoryStore : ICategoryStore
{
    public List<Category> Stored { get; } = [];

    public List<Category> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Category>>([.. Stored]);

    public Task<Category?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(c => c.Id == id));

    public Task SaveAsync(Category aggregate, CancellationToken ct = default)
    {
        Saved.Add(aggregate);
        Stored.RemoveAll(c => c.Id == aggregate.Id);
        Stored.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        Stored.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeTariffStore : ITariffStore
{
    public List<TariffComponent> Stored { get; } = [];

    public List<TariffComponent> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<TariffComponent>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TariffComponent>>([.. Stored.OrderBy(c => c.SortOrder)]);

    public Task<IReadOnlyList<TariffComponent>> ListByContractAsync(Guid contractId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TariffComponent>>([.. Stored.Where(c => c.ContractId == contractId).OrderBy(c => c.SortOrder)]);

    public Task<TariffComponent?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(c => c.Id == id));

    public Task SaveAsync(TariffComponent aggregate, CancellationToken ct = default)
    {
        Saved.Add(aggregate);
        Stored.RemoveAll(c => c.Id == aggregate.Id);
        Stored.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        Stored.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeVatRateStore : IVatRateStore
{
    public FakeVatRateStore()
    {
        Stored.AddRange(LatvianVatDefaults.Create());
    }

    public List<VatRate> Stored { get; } = [];

    public List<VatRate> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<VatRate>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VatRate>>([.. Stored.OrderBy(v => v.Code, StringComparer.Ordinal)]);

    public Task<VatRate?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(v => v.Id == id));

    public Task SaveAsync(VatRate aggregate, CancellationToken ct = default)
    {
        Saved.Add(aggregate);
        Stored.RemoveAll(v => v.Id == aggregate.Id);
        Stored.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        Stored.RemoveAll(v => v.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeLauncherService : ILauncherService
{
    public List<Uri> Opened { get; } = [];

    public Task OpenAsync(Uri uri)
    {
        Opened.Add(uri);
        return Task.CompletedTask;
    }
}

internal sealed class FakeClipboardService : IClipboardService
{
    public List<string> Texts { get; } = [];

    public Task SetTextAsync(string text)
    {
        Texts.Add(text);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSnackbarService : ISnackbarService
{
    /// <summary>The messages shown, in order.</summary>
    public List<string> Messages { get; } = [];

    /// <summary>Every call with its action text and duration.</summary>
    public List<(string Message, string? Action, TimeSpan? Duration)> Calls { get; } = [];

    /// <summary>What a message with an action returns: true = the user tapped the action.</summary>
    public Func<string, string?, bool> ActionResult { get; set; } = (_, _) => false;

    public Task<bool> ShowAsync(string message, string? actionText = null, TimeSpan? duration = null)
    {
        Messages.Add(message);
        Calls.Add((message, actionText, duration));
        return Task.FromResult(actionText is not null && ActionResult(message, actionText));
    }
}

internal sealed class FakeContractStore : IContractStore
{
    public List<Contract> Stored { get; } = [];

    public List<Contract> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public Task<IReadOnlyList<Contract>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Contract>>([.. Stored]);

    public Task<IReadOnlyList<Contract>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Contract>>([.. Stored.Where(c => c.PropertyId == propertyId)]);

    public List<ContractMeter> Links { get; } = [];

    public List<ContractMeter> SavedLinks { get; } = [];

    public Task<IReadOnlyList<ContractMeter>> ListContractMetersAsync(Guid contractId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ContractMeter>>([.. Links.Where(l => l.ContractId == contractId)]);

    public Task SaveContractMeterAsync(ContractMeter contractMeter, CancellationToken ct = default)
    {
        SavedLinks.Add(contractMeter);
        if (!Links.Contains(contractMeter))
        {
            Links.RemoveAll(l => l.Id == contractMeter.Id);
            Links.Add(contractMeter);
        }

        return Task.CompletedTask;
    }

    public Task<Contract?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(c => c.Id == id));

    public Task SaveAsync(Contract aggregate, CancellationToken ct = default)
    {
        Saved.Add(aggregate);
        Stored.RemoveAll(c => c.Id == aggregate.Id);
        Stored.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        Stored.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeMeterStore : IMeterStore
{
    public List<Meter> Stored { get; } = [];

    public List<Meter> Saved { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public List<(Meter Old, Meter New, IReadOnlyList<ContractMeter> Closed, IReadOnlyList<ContractMeter> Created)> Replacements { get; } = [];

    /// <summary>One entry per <see cref="AddReadingsAsync"/> call with the readings it stored.</summary>
    public List<IReadOnlyList<(Guid RegisterId, MeterReading Reading)>> AddedBatches { get; } = [];

    /// <summary>One entry per <see cref="UpdateReadingsAsync"/> call.</summary>
    public List<IReadOnlyList<MeterReading>> UpdatedBatches { get; } = [];

    public List<Guid> DeletedReadings { get; } = [];

    /// <summary>The readings of a stored register; empty if the register is unknown.</summary>
    public IEnumerable<MeterReading> ReadingsOf(Guid registerId) =>
        Stored.SelectMany(m => m.Registers).Where(r => r.Id == registerId).SelectMany(r => r.Readings);

    /// <summary>When set, <see cref="AddReadingsAsync"/> throws it instead of storing.</summary>
    public Exception? AddReadingsFailure { get; set; }

    public Task AddReadingsAsync(IEnumerable<(Guid RegisterId, MeterReading Reading)> readings, CancellationToken ct = default)
    {
        if (AddReadingsFailure is { } failure)
        {
            throw failure;
        }

        var list = readings.ToList();
        AddedBatches.Add(list);
        foreach (var (registerId, reading) in list)
        {
            reading.RegisterId = registerId;
            Stored.SelectMany(m => m.Registers).First(r => r.Id == registerId).Readings.Add(reading);
        }

        return Task.CompletedTask;
    }

    public Task UpdateReadingsAsync(IEnumerable<MeterReading> readings, CancellationToken ct = default)
    {
        var list = readings.ToList();
        UpdatedBatches.Add(list);
        foreach (var incoming in list)
        {
            var stored = Stored.SelectMany(m => m.Registers).SelectMany(r => r.Readings).First(r => r.Id == incoming.Id);
            stored.Date = incoming.Date;
            stored.Value = incoming.Value;
            stored.Kind = incoming.Kind;
            stored.SubmittedOn = incoming.SubmittedOn;
            stored.PhotoAttachmentId = incoming.PhotoAttachmentId;
            stored.Note = incoming.Note;
        }

        return Task.CompletedTask;
    }

    public Task DeleteReadingAsync(Guid readingId, CancellationToken ct = default)
    {
        DeletedReadings.Add(readingId);
        foreach (var register in Stored.SelectMany(m => m.Registers))
        {
            register.Readings.RemoveAll(r => r.Id == readingId);
        }

        return Task.CompletedTask;
    }

    /// <summary>When set, <see cref="SaveReplacementAsync"/> throws it instead of storing.</summary>
    public Exception? ReplacementFailure { get; set; }

    public Task<IReadOnlyList<Meter>> ListAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Meter>>([.. Stored]);

    public Task<IReadOnlyList<Meter>> ListByPropertyAsync(Guid propertyId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Meter>>([.. Stored.Where(m => m.PropertyId == propertyId)]);

    /// <summary>Returns a copy of the stored graph, like a real store: the caller may change it freely.</summary>
    public Task<Meter?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Stored.FirstOrDefault(m => m.Id == id) is { } meter ? Copy(meter) : null);

    public Task SaveAsync(Meter aggregate, CancellationToken ct = default)
    {
        Saved.Add(aggregate);
        Stored.RemoveAll(m => m.Id == aggregate.Id);
        Stored.Add(aggregate);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        Stored.RemoveAll(m => m.Id == id);
        return Task.CompletedTask;
    }

    public Task SaveReplacementAsync(
        Meter oldMeter,
        Meter newMeter,
        IReadOnlyList<ContractMeter> linksToClose,
        IReadOnlyList<ContractMeter> linksToCreate,
        CancellationToken ct = default)
    {
        if (ReplacementFailure is { } failure)
        {
            throw failure;
        }

        Replacements.Add((oldMeter, newMeter, linksToClose, linksToCreate));
        Stored.RemoveAll(m => m.Id == oldMeter.Id);
        Stored.Add(oldMeter);
        Stored.Add(newMeter);
        return Task.CompletedTask;
    }

    private static Meter Copy(Meter source)
    {
        var copy = new Meter
        {
            Id = source.Id,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt,
            PropertyId = source.PropertyId,
            Resource = source.Resource,
            Unit = source.Unit,
            SerialNumber = source.SerialNumber,
            Location = source.Location,
            Digits = source.Digits,
            InstalledOn = source.InstalledOn,
            RemovedOn = source.RemovedOn,
            ReplacedMeterId = source.ReplacedMeterId,
            VerifiedOn = source.VerifiedOn,
            VerificationIntervalMonths = source.VerificationIntervalMonths,
            NextVerificationDue = source.NextVerificationDue,
            Notes = source.Notes,
        };
        foreach (var register in source.Registers)
        {
            var registerCopy = new MeterRegister
            {
                Id = register.Id,
                CreatedAt = register.CreatedAt,
                MeterId = register.MeterId,
                Kind = register.Kind,
                Direction = register.Direction,
                Name = register.Name,
            };
            foreach (var reading in register.Readings)
            {
                registerCopy.Readings.Add(new MeterReading
                {
                    Id = reading.Id,
                    CreatedAt = reading.CreatedAt,
                    RegisterId = reading.RegisterId,
                    Date = reading.Date,
                    Value = reading.Value,
                    Kind = reading.Kind,
                    SubmittedOn = reading.SubmittedOn,
                    PhotoAttachmentId = reading.PhotoAttachmentId,
                    Note = reading.Note,
                });
            }

            copy.Registers.Add(registerCopy);
        }

        return copy;
    }
}

internal sealed class FakeSettingsStore : ISettingsStore
{
    public Dictionary<string, string> Values { get; } = [];

    public string? GetString(string key) => Values.GetValueOrDefault(key);

    public void SetString(string key, string? value)
    {
        if (value is null)
        {
            Values.Remove(key);
        }
        else
        {
            Values[key] = value;
        }
    }
}

/// <summary>All the fakes a contract editor needs, with the built-in categories and one property in place.</summary>
internal sealed class ContractTestKit
{
    public ContractTestKit(DateOnly today)
    {
        Clock = new TestClock(today);
        Localizer = new FakeLocalizer(Language);
        ChargeLoader = new FakeChargeInputLoader(this);
        foreach (var category in BuiltInCategories.Create())
        {
            Categories.Stored.Add(category);
        }

        Properties.Stored.Add(Property);
        Payments.BillContractResolver = id => Bills.All.FirstOrDefault(b => b.Id == id)?.ContractId;
    }

    public FakeLanguageService Language { get; } = new();

    public FakeLocalizer Localizer { get; }

    public TestClock Clock { get; }

    public FakeContractStore Contracts { get; } = new();

    public FakeMeterStore Meters { get; } = new();

    public FakeTariffStore Tariffs { get; } = new();

    public FakeVatRateStore VatRates { get; } = new();

    public FakeSupplierStore Suppliers { get; } = new();

    public FakeCategoryStore Categories { get; } = new();

    public FakePropertyStore Properties { get; } = new();

    public FakeNavigationService Navigation { get; } = new();

    public FakeDialogService Dialogs { get; } = new();

    public FakeLauncherService Launcher { get; } = new();

    public FakeClipboardService Clipboard { get; } = new();

    public FakeSnackbarService Toast { get; } = new();

    public FakeSettingsStore Settings { get; } = new();

    public FakeLayoutModeService Layout { get; } = new();

    public FakeAttachmentStore Attachments { get; } = new();

    public FakeMediaService Media { get; } = new();

    public FakeErrorLog ErrorLog { get; } = new();

    public FakeBillStore Bills { get; } = new();

    public FakePaymentStore Payments { get; } = new();

    public FakeChargeInputLoader ChargeLoader { get; }

    public FakeFileViewer Viewer { get; } = new();

    public Property Property { get; } = new() { Name = "Sample flat", Kind = PropertyKind.Apartment };

    public SupplierEditViewModel CreateSupplierEditor() =>
        new(Suppliers, Clock, Localizer, Dialogs, Navigation, Launcher, Clipboard, Toast);

    public ContractEditViewModel CreateContractEditor() =>
        new(Contracts, Suppliers, Categories, Properties, Tariffs, VatRates, Meters, Clock, Localizer, Dialogs, Navigation, Launcher, Clipboard, Toast, CreateSupplierEditor);

    public TariffComponentEditViewModel CreateTariffEditor() =>
        new(Tariffs, Contracts, Meters, Properties, VatRates, Categories, Clock, Localizer, Dialogs, Navigation, Toast);

    public VatRateEditViewModel CreateVatRateEditor() =>
        new(VatRates, Tariffs, Clock, Localizer, Dialogs, Navigation, Toast);

    public VatRatesViewModel CreateVatRates(ISectionSelectionMemory? memory = null) =>
        new(VatRates, Clock, Localizer, Navigation, Layout, memory ?? new SectionSelectionMemory(), CreateVatRateEditor);

    public PropertyEditViewModel CreatePropertyEditor() =>
        new(Properties, Contracts, Meters, Suppliers, Categories, Clock, Localizer, Dialogs, Navigation, Toast, CreateContractEditor, CreateMeterEditor);

    public MeterEditViewModel CreateMeterEditor() =>
        new(Meters, Contracts, Clock, Localizer, Dialogs, Navigation, Toast, CreateMeterEditor);

    public ReadingHistoryViewModel CreateHistory() => new(Meters, Attachments, Viewer, Media, Localizer, Dialogs);

    public ReadingsViewModel CreateReadings() =>
        new(Properties, Meters, Contracts, Suppliers, Clock, Localizer, Dialogs, Navigation, Toast, Launcher, Settings, Attachments, Media, Layout, CreateHistory);

    public ChargesViewModel CreateCharges(FakeChargeInputLoader? loader = null) =>
        new(Properties, Contracts, Suppliers, Categories, loader ?? ChargeLoader, Bills, Payments, Clock, Localizer, Navigation, Settings, Layout, ErrorLog);

    public Guid CategoryIdFor(ServiceKind kind) => BuiltInCategories.FindFor(kind, Categories.Stored)!.Value;
}

/// <summary>Assembles the <see cref="ChargeInput"/> of a contract from the fake stores of a <see cref="ContractTestKit"/>.</summary>
internal sealed class FakeChargeInputLoader(ContractTestKit kit) : IChargeInputLoader
{
    /// <summary>Contracts whose loading throws.</summary>
    public Dictionary<Guid, Exception> Failures { get; } = [];

    public Task<ChargeInput?> LoadAsync(Guid contractId, YearMonth period, CancellationToken ct = default)
    {
        if (Failures.TryGetValue(contractId, out var failure))
        {
            throw failure;
        }

        var contract = kit.Contracts.Stored.FirstOrDefault(c => c.Id == contractId);
        var property = contract is null ? null : kit.Properties.Stored.FirstOrDefault(p => p.Id == contract.PropertyId);
        if (contract is null || property is null)
        {
            return Task.FromResult<ChargeInput?>(null);
        }

        return Task.FromResult<ChargeInput?>(new ChargeInput(
            contract,
            property,
            [.. kit.Tariffs.Stored.Where(t => t.ContractId == contractId)],
            [.. kit.Contracts.Links.Where(l => l.ContractId == contractId)],
            [.. kit.Meters.Stored.Where(m => m.PropertyId == contract.PropertyId)],
            kit.VatRates.Stored,
            period));
    }
}

internal sealed class FakeErrorLog : IErrorLog
{
    public string Text { get; set; } = string.Empty;

    public List<(string Source, Exception Exception)> Entries { get; } = [];

    public void Append(string source, Exception exception) => Entries.Add((source, exception));

    public string ReadAll() => Text;

    public void Clear() => Text = string.Empty;
}

internal sealed class FakeAttachmentStore : IAttachmentStore
{
    /// <summary>The attachments added so far with the bytes they were given.</summary>
    public List<(Attachment Meta, byte[] Content)> Added { get; } = [];

    public List<Guid> Deleted { get; } = [];

    /// <summary>When set, <see cref="AddAsync"/> rejects the file with these errors.</summary>
    public ValidationResult? Rejection { get; set; }

    /// <summary>Ids whose file is reported missing.</summary>
    public HashSet<Guid> MissingFiles { get; } = [];

    public async Task<Attachment> AddAsync(Attachment meta, Stream content, CancellationToken ct = default)
    {
        if (Rejection is { } rejection)
        {
            throw new AttachmentRejectedException(rejection);
        }

        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, ct);
        meta.SizeBytes = copy.Length;
        meta.Sha256 = new string('a', 64);
        Added.Add((meta, copy.ToArray()));
        return meta;
    }

    public Task<Attachment?> GetAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Added.Select(a => a.Meta).FirstOrDefault(a => a.Id == id && !Deleted.Contains(id)));

    public Task<Stream?> OpenReadAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<Stream?>(Added.FirstOrDefault(a => a.Meta.Id == id) is { Content: { } bytes } ? new MemoryStream(bytes) : null);

    public Task<string?> GetFilePathAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult<string?>(MissingFiles.Contains(id) || Added.All(a => a.Meta.Id != id) ? null : $"/attachments/{id}.jpg");

    public Task<IReadOnlyList<Attachment>> ListByOwnerAsync(AttachmentOwnerKind kind, Guid ownerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Attachment>>([.. Added.Select(a => a.Meta).Where(a => a.OwnerKind == kind && a.OwnerId == ownerId && !Deleted.Contains(a.Id))]);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        return Task.CompletedTask;
    }
}

internal sealed class FakeMediaService : IMediaService
{
    public bool IsCaptureSupported { get; set; } = true;

    /// <summary>What the next camera / gallery call returns; null = the user cancelled.</summary>
    public PickedFile? Next { get; set; }

    /// <summary>When set, the next call throws it (e.g. <see cref="MediaPermissionDeniedException"/>).</summary>
    public Exception? Failure { get; set; }

    public int CaptureCalls { get; private set; }

    public int PickCalls { get; private set; }

    public static PickedFile Photo(string name = "dial.jpg", string contentType = "image/jpeg", params byte[] bytes) =>
        new(name, contentType, () => Task.FromResult<Stream>(new MemoryStream(bytes.Length == 0 ? [1, 2, 3] : bytes)));

    public Task<PickedFile?> CapturePhotoAsync()
    {
        CaptureCalls++;
        return Result();
    }

    public Task<PickedFile?> PickPhotoAsync()
    {
        PickCalls++;
        return Result();
    }

    public Task<PickedFile?> PickFileAsync(string[] contentTypes) => Result();

    private Task<PickedFile?> Result() => Failure is { } failure ? Task.FromException<PickedFile?>(failure) : Task.FromResult(Next);
}

internal sealed class FakeFileViewer : IFileViewer
{
    public List<(string Path, string ContentType)> Opened { get; } = [];

    public Task OpenAsync(string path, string contentType)
    {
        Opened.Add((path, contentType));
        return Task.CompletedTask;
    }
}

internal sealed class FakeBackupService : IBackupService
{
    public BackupValidation Validation { get; set; } = BackupValidation.Valid(
        new BackupManifest(1, "1.0.0", "20261020125612_InitialCreate", new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero), "Test", 3, 10, 0));

    public int Created { get; private set; }

    public int Staged { get; private set; }

    public int Wipes { get; private set; }

    public string FileName { get; set; } = "backup.zip";

    public string SuggestFileName() => FileName;

    public async Task<BackupManifest> CreateAsync(Stream output, CancellationToken ct = default)
    {
        Created++;
        await output.WriteAsync(new byte[] { 1, 2, 3 }, ct);
        return Validation.Manifest!;
    }

    public Task<BackupValidation> ValidateAsync(Stream backup, CancellationToken ct = default) => Task.FromResult(Validation);

    public Task<BackupValidation> StageRestoreAsync(Stream backup, CancellationToken ct = default)
    {
        if (Validation.IsValid)
        {
            Staged++;
        }

        return Task.FromResult(Validation);
    }

    public void StageWipe() => Wipes++;

    public RestoreOutcome? ApplyPendingRestore() => null;
}

internal sealed class FakeBackupFileService : IBackupFileService
{
    public BackupSaveResult SaveResult { get; set; } = new(true, "C:\\Backups");

    public Stream? Picked { get; set; }

    public List<string> SavedNames { get; } = [];

    public byte[] SavedBytes { get; private set; } = [];

    public async Task<BackupSaveResult> SaveAsync(string fileName, Func<Stream, Task> write)
    {
        SavedNames.Add(fileName);
        var buffer = new MemoryStream();
        await write(buffer);
        SavedBytes = buffer.ToArray();
        return SaveResult;
    }

    public Task<Stream?> PickAsync() => Task.FromResult(Picked);
}

internal sealed class FakeAppLifecycle : IAppLifecycle
{
    public int QuitCalls { get; private set; }

    public void Quit() => QuitCalls++;
}

internal sealed class FakeSampleDataGenerator : ISampleDataGenerator
{
    public bool Result { get; set; } = true;

    public int Calls { get; private set; }

    public Task<bool> FillAsync(CancellationToken ct = default)
    {
        Calls++;
        return Task.FromResult(Result);
    }
}

internal sealed class FixedClock : IClock
{
    public DateOnly Today => new(2026, 10, 24);

    public DateTimeOffset UtcNow => new(2026, 10, 24, 9, 30, 0, TimeSpan.Zero);
}
