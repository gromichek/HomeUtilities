using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests.Common;

/// <summary>Touches the static <see cref="ViewModelBase.ErrorReporter"/>, so it must not run in parallel with other tests.</summary>
[Collection("StaticErrorReporter")]
public class SafeCommandTests : IDisposable
{
    private readonly FakeErrorLog _log = new();
    private readonly FakeDialogService _dialogs = new();

    public SafeCommandTests()
    {
        ViewModelBase.ErrorReporter = new ErrorReporter(_log, _dialogs, new FakeLocalizer(new FakeLanguageService()));
    }

    public void Dispose() => ViewModelBase.ErrorReporter = null;

    [Fact]
    public async Task ExceptionInACommandHandler_IsLoggedAndShown_AndTheCommandCompletes()
    {
        var kit = new ContractTestKit(new DateOnly(2026, 10, 23));
        var vm = new CategoryEditViewModel(new ThrowingCategoryStore(), kit.Localizer, kit.Dialogs, kit.Navigation, kit.Toast);
        await vm.LoadAsync(null);
        vm.Name = "x";
        // The store throws on save: the failure must be absorbed.
        var reporter = new RecordingReporter();
        ViewModelBase.ErrorReporter = reporter;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Single(reporter.Reported);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task ErrorReporter_WritesTheLogAndShowsTheStandardMessage()
    {
        var reporter = new ErrorReporter(_log, _dialogs, new FakeLocalizer(new FakeLanguageService()));

        await reporter.ReportAsync("Test.Source", new InvalidOperationException("boom"));

        var entry = Assert.Single(_log.Entries);
        Assert.Equal("Test.Source", entry.Source);
        Assert.Equal("boom", entry.Exception.Message);
        Assert.Single(_dialogs.Alerts);
        Assert.Equal("Error_Unexpected_Title [en]", _dialogs.Alerts[0].Title);
        Assert.StartsWith("Error_Unexpected_Message [en]", _dialogs.Alerts[0].Message);
    }

    private sealed class RecordingReporter : IErrorReporter
    {
        public List<Exception> Reported { get; } = [];

        public Task ReportAsync(string source, Exception exception)
        {
            Reported.Add(exception);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCategoryStore : HomeUtilities.Core.Abstractions.ICategoryStore
    {
        public Task<IReadOnlyList<HomeUtilities.Core.Domain.Category>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<HomeUtilities.Core.Domain.Category>>([]);

        public Task<HomeUtilities.Core.Domain.Category?> GetAsync(Guid id, CancellationToken ct = default) =>
            throw new InvalidOperationException("store failed");

        public Task SaveAsync(HomeUtilities.Core.Domain.Category aggregate, CancellationToken ct = default) =>
            throw new InvalidOperationException("store failed");

        public Task DeleteAsync(Guid id, CancellationToken ct = default) =>
            throw new InvalidOperationException("store failed");
    }
}
