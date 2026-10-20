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

    /// <summary>What the "user" does in the next child-item editor dialog; null = just cancel.</summary>
    public Action<EditorDialogViewModel>? EditorScript { get; set; }

    public EditorDialogViewModel? LastEditor { get; private set; }

    public Task AlertAsync(string title, string message) => Task.CompletedTask;

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        Confirmations.Add((title, message));
        return Task.FromResult(ConfirmResult);
    }

    public Task<string?> ChooseAsync(string title, IReadOnlyList<string> options) => Task.FromResult<string?>(null);

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
