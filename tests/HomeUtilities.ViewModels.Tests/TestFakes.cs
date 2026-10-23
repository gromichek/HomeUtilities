using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
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

    public List<(string Title, string Message)> Confirmations { get; } = [];

    /// <summary>The answer to the "Save changes?" dialog.</summary>
    public UnsavedChangesChoice UnsavedChoice { get; set; } = UnsavedChangesChoice.DontSave;

    public int UnsavedAsked { get; private set; }

    /// <summary>The answers to <see cref="ChooseAsync"/>: gets the options, returns the picked one (null = cancelled).</summary>
    public Func<IReadOnlyList<string>, string?>? ChooseScript { get; set; }

    /// <summary>What the "user" does in the next child-item editor dialog; null = just cancel.</summary>
    public Action<EditorDialogViewModel>? EditorScript { get; set; }

    public EditorDialogViewModel? LastEditor { get; private set; }

    public Task AlertAsync(string title, string message) => Task.CompletedTask;

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        Confirmations.Add((title, message));
        return Task.FromResult(ConfirmResult);
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

internal sealed class FakeToastService : IToastService
{
    public List<string> Messages { get; } = [];

    public Task ShowAsync(string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
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

    public Task<IReadOnlyList<ContractMeter>> ListContractMetersAsync(Guid contractId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ContractMeter>>([]);

    public Task SaveContractMeterAsync(ContractMeter contractMeter, CancellationToken ct = default) => Task.CompletedTask;

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

/// <summary>All the fakes a contract editor needs, with the built-in categories and one property in place.</summary>
internal sealed class ContractTestKit
{
    public ContractTestKit(DateOnly today)
    {
        Clock = new TestClock(today);
        Localizer = new FakeLocalizer(Language);
        foreach (var category in BuiltInCategories.Create())
        {
            Categories.Stored.Add(category);
        }

        Properties.Stored.Add(Property);
    }

    public FakeLanguageService Language { get; } = new();

    public FakeLocalizer Localizer { get; }

    public TestClock Clock { get; }

    public FakeContractStore Contracts { get; } = new();

    public FakeSupplierStore Suppliers { get; } = new();

    public FakeCategoryStore Categories { get; } = new();

    public FakePropertyStore Properties { get; } = new();

    public FakeNavigationService Navigation { get; } = new();

    public FakeDialogService Dialogs { get; } = new();

    public FakeLauncherService Launcher { get; } = new();

    public FakeClipboardService Clipboard { get; } = new();

    public FakeToastService Toast { get; } = new();

    public Property Property { get; } = new() { Name = "Sample flat", Kind = PropertyKind.Apartment };

    public SupplierEditViewModel CreateSupplierEditor() =>
        new(Suppliers, Clock, Localizer, Dialogs, Navigation, Launcher, Clipboard, Toast);

    public ContractEditViewModel CreateContractEditor() =>
        new(Contracts, Suppliers, Categories, Properties, Clock, Localizer, Dialogs, Navigation, Launcher, Clipboard, Toast, CreateSupplierEditor);

    public PropertyEditViewModel CreatePropertyEditor() =>
        new(Properties, Contracts, Suppliers, Categories, Clock, Localizer, Dialogs, Navigation, Toast, CreateContractEditor);

    public Guid CategoryIdFor(ServiceKind kind) => BuiltInCategories.FindFor(kind, Categories.Stored)!.Value;
}

internal sealed class FakeErrorLog : IErrorLog
{
    public string Text { get; set; } = string.Empty;

    public List<(string Source, Exception Exception)> Entries { get; } = [];

    public void Append(string source, Exception exception) => Entries.Add((source, exception));

    public string ReadAll() => Text;

    public void Clear() => Text = string.Empty;
}
