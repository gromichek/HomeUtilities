using HomeUtilities.Core.Sync;

namespace HomeUtilities.ViewModels;

/// <summary>The short localized lines of the sync Details list: "14:52 Received changes (records: 2)" (R-43).</summary>
public static class SyncDetailText
{
    /// <summary>One line: the local time and the localized text of the kind. Never a file name or an id.</summary>
    public static string Line(ILocalizer localizer, SyncDetail detail)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(detail);
        return $"{detail.At.ToLocalTime():HH:mm} {localizer.Get("SyncDetail_" + detail.Kind, detail.Count)}";
    }

    /// <summary>The lines, newest first as the engine keeps them, one per line.</summary>
    public static string Lines(ILocalizer localizer, IEnumerable<SyncDetail> details) =>
        string.Join(Environment.NewLine, details.Select(d => Line(localizer, d)));
}
