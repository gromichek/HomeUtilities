namespace HomeUtilities.ViewModels.Tests;

/// <summary>The "Loading…" hint of <see cref="ViewModelBase.RunLoadAsync"/> (R-52, T-036a check C-03).</summary>
public class LoadingHintTests
{
    [Fact]
    public async Task FailedFastLoad_DoesNotLeaveTheHintOn()
    {
        // The load fails after 10 ms, well before the 300 ms hint delay; 450 ms later the hint must still be off.
        var vm = new HintViewModel();

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadAsync(async ct =>
        {
            await Task.Delay(10, ct);
            throw new InvalidOperationException("load failed");
        }));
        await Task.Delay(ViewModelBase.LoadingHintDelay + TimeSpan.FromMilliseconds(150));

        Assert.False(vm.ShowLoading);
    }

    [Fact]
    public async Task SlowLoad_ShowsTheHintWhileLoading_AndHidesItAfter()
    {
        // The load takes 600 ms: the hint appears after 300 ms and is off when the load is over.
        var vm = new HintViewModel();
        var seenDuringLoad = false;

        await vm.LoadAsync(async ct =>
        {
            await Task.Delay(ViewModelBase.LoadingHintDelay + TimeSpan.FromMilliseconds(300), ct);
            seenDuringLoad = vm.ShowLoading;
        });

        Assert.True(seenDuringLoad);
        Assert.False(vm.ShowLoading);
    }

    private sealed class HintViewModel : ViewModelBase
    {
        protected override bool LoadingHintApplies => true;

        public Task LoadAsync(Func<CancellationToken, Task> load) => RunLoadAsync(load);
    }
}
