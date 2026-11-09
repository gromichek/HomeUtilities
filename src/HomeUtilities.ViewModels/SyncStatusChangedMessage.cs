namespace HomeUtilities.ViewModels;

/// <summary>
/// Sent through the weak messenger by <see cref="SyncNotifier"/> whenever the sync engine's status changes, so screens need no
/// subscription to the singleton engine (and cannot be kept alive by one). May arrive on the sync thread.
/// </summary>
public sealed record SyncStatusChangedMessage;
