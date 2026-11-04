using HomeUtilities.Core.Sync;

namespace HomeUtilities.App.Services;

/// <summary>MAUI <see cref="Connectivity"/> behind <see cref="ISyncConnectivity"/> (C10, attachment download rule).</summary>
public sealed class MauiSyncConnectivity : ISyncConnectivity, IDisposable
{
    public MauiSyncConnectivity() => Connectivity.Current.ConnectivityChanged += OnChanged;

    public event EventHandler? Changed;

    public bool IsConnected => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    /// <summary>True on Wi-Fi or Ethernet; false on mobile data (and when no profile is known).</summary>
    public bool IsUnmetered => Connectivity.Current.ConnectionProfiles.Any(p => p is ConnectionProfile.WiFi or ConnectionProfile.Ethernet);

    public void Dispose() => Connectivity.Current.ConnectivityChanged -= OnChanged;

    private void OnChanged(object? sender, ConnectivityChangedEventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
