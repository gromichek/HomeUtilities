namespace HomeUtilities.ViewModels;

/// <summary>Per-device sync preferences (T-032): the messages switch and the download rule for attachment files.</summary>
public sealed class SyncSettings(ISettingsStore store)
{
    private const string MessagesKey = "Sync.ShowMessages";
    private const string MobileDataKey = "Sync.DownloadOnMobileData";

    /// <summary>Short messages about the sync stages (C13); on by default.</summary>
    public bool ShowMessages
    {
        get => store.GetString(MessagesKey) != "0";
        set => store.SetString(MessagesKey, value ? "1" : "0");
    }

    /// <summary>Download photos and documents on mobile data without being asked (Part B); off by default.</summary>
    public bool DownloadOnMobileData
    {
        get => store.GetString(MobileDataKey) == "1";
        set => store.SetString(MobileDataKey, value ? "1" : "0");
    }
}
