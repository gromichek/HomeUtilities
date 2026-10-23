// View models talk through the static WeakReferenceMessenger.Default; tests running in parallel saw each
// other's messages (flaky SettingsViewModelTests on CI, PR #32 / #41). Run this assembly's tests sequentially.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
