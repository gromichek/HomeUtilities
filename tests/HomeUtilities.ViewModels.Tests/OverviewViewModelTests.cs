using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.ViewModels.Tests;

public class OverviewViewModelTests
{
    [Fact]
    public void Title_ComesFromLocalizer()
    {
        var vm = CreateViewModel(out _, out _, out _);

        Assert.Equal("Home utilities [en]", vm.Title);
        Assert.Equal("Today is 2026-10-19 [en]", vm.TodayText);
    }

    [Fact]
    public void Title_IsRefreshedAfterLanguageChanged()
    {
        var vm = CreateViewModel(out var language, out _, out _);

        language.SetLanguage("ru");

        Assert.Equal("Home utilities [ru]", vm.Title);
        Assert.Equal("Today is 2026-10-19 [ru]", vm.TodayText);
    }

    [Fact]
    public async Task Initialize_Success_ShowsCountsAndRunsOnce()
    {
        var vm = CreateViewModel(out _, out _, out var initializer);

        await vm.InitializeCommand.ExecuteAsync(null);
        await vm.InitializeCommand.ExecuteAsync(null);

        Assert.Equal("DatabaseReady 16|3 [en]", vm.DatabaseStatusText);
        Assert.Equal(1, initializer.Calls);
    }

    [Fact]
    public async Task Initialize_Failure_ShowsErrorMessage()
    {
        var vm = CreateViewModel(out _, out _, out var initializer);
        initializer.Failure = new InvalidOperationException("disk full");

        await vm.InitializeCommand.ExecuteAsync(null);

        Assert.Equal("DatabaseError disk full [en]", vm.DatabaseStatusText);
    }

    [Fact]
    public async Task Initialize_WhileRunning_ShowsStartingText()
    {
        var vm = CreateViewModel(out _, out _, out var initializer);
        var gate = new TaskCompletionSource();
        initializer.Gate = gate.Task;

        var running = vm.InitializeCommand.ExecuteAsync(null);
        Assert.Equal("DatabaseStarting [en]", vm.DatabaseStatusText);

        gate.SetResult();
        await running;
        Assert.Equal("DatabaseReady 16|3 [en]", vm.DatabaseStatusText);
    }

    [Fact]
    public async Task DatabaseStatus_IsRefreshedAfterLanguageChanged()
    {
        var vm = CreateViewModel(out var language, out _, out _);
        await vm.InitializeCommand.ExecuteAsync(null);

        language.SetLanguage("ru");

        Assert.Equal("DatabaseReady 16|3 [ru]", vm.DatabaseStatusText);
    }

    [Fact]
    public void DatabaseStatus_IsEmptyBeforeInitialize()
    {
        var vm = CreateViewModel(out _, out _, out _);

        Assert.Equal(string.Empty, vm.DatabaseStatusText);
    }

    private static OverviewViewModel CreateViewModel(out FakeLanguageService language, out FakeClock clock, out FakeInitializer initializer)
    {
        language = new FakeLanguageService();
        clock = new FakeClock();
        initializer = new FakeInitializer();
        return new OverviewViewModel(
            new FakeLocalizer(language),
            clock,
            initializer,
            new FakeCategoryStore(16),
            new FakeVatRateStore(3));
    }

    private sealed class FakeInitializer : IDatabaseInitializer
    {
        public int Calls { get; private set; }

        public Exception? Failure { get; set; }

        public Task? Gate { get; set; }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            Calls++;
            if (Gate is not null)
            {
                await Gate;
            }

            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }

    private sealed class FakeCategoryStore(int count) : ICategoryStore
    {
        public Task<IReadOnlyList<Category>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Category>>([.. Enumerable.Range(0, count).Select(_ => new Category())]);

        public Task<Category?> GetAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task SaveAsync(Category aggregate, CancellationToken ct = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeVatRateStore(int count) : IVatRateStore
    {
        public Task<IReadOnlyList<VatRate>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<VatRate>>([.. Enumerable.Range(0, count).Select(_ => new VatRate())]);

        public Task<VatRate?> GetAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();

        public Task SaveAsync(VatRate aggregate, CancellationToken ct = default) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeClock : IClock
    {
        public DateOnly Today => new(2026, 10, 19);

        public DateTimeOffset UtcNow => new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeLanguageService : ILanguageService
    {
        public IReadOnlyList<string> SupportedLanguages { get; } = ["en", "ru"];

        public string CurrentLanguage { get; private set; } = "en";

        public void SetLanguage(string code)
        {
            CurrentLanguage = code;
            WeakReferenceMessenger.Default.Send(new LanguageChangedMessage(code));
        }
    }

    private sealed class FakeLocalizer(FakeLanguageService language) : ILocalizer
    {
        public string this[string key] => key switch
        {
            "AppTitle" => $"Home utilities [{language.CurrentLanguage}]",
            _ => $"{key} [{language.CurrentLanguage}]",
        };

        public bool TryGet(string key, out string value)
        {
            value = this[key];
            return true;
        }

        public string Get(string key, params object[] args) => key switch
        {
            "TodayIs" => $"Today is {((DateOnly)args[0]):yyyy-MM-dd} [{language.CurrentLanguage}]",
            "DatabaseReady" => $"{key} {args[0]}|{args[1]} [{language.CurrentLanguage}]",
            "DatabaseError" => $"{key} {args[0]} [{language.CurrentLanguage}]",
            _ => $"{key} [{language.CurrentLanguage}]",
        };
    }
}
