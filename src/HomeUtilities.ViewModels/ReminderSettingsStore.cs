using System.Globalization;
using HomeUtilities.Core.Reminders;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Reads and writes the <see cref="ReminderSettings"/> in the per-device settings store (not in the synced database:
/// every device schedules its own notifications).
/// </summary>
public sealed class ReminderSettingsStore(ISettingsStore store)
{
    /// <summary>Settings key: master switch.</summary>
    public const string EnabledKey = "Reminders.Enabled";

    /// <summary>Settings key: reading window reminders.</summary>
    public const string ReadingsKey = "Reminders.Readings";

    /// <summary>Settings key: bill and tax reminders.</summary>
    public const string BillsKey = "Reminders.Bills";

    /// <summary>Settings key: verification reminders.</summary>
    public const string VerificationKey = "Reminders.Verification";

    /// <summary>Settings key: draft reminders.</summary>
    public const string DraftsKey = "Reminders.Drafts";

    /// <summary>Settings key: time of day (HH:mm).</summary>
    public const string TimeKey = "Reminders.Time";

    /// <summary>Settings key: lead days of bill reminders.</summary>
    public const string LeadDaysKey = "Reminders.BillLeadDays";

    /// <summary>Settings key: the day the start summary was last shown (yyyy-MM-dd); platforms without scheduling show it once a day.</summary>
    public const string LastSummaryKey = "Reminders.LastSummary";

    /// <summary>The stored settings; unset values have their defaults (reminders off, every kind on, 09:00, 2 days).</summary>
    public ReminderSettings Load() => new(
        Enabled: ReadBool(EnabledKey, false),
        Readings: ReadBool(ReadingsKey, true),
        Bills: ReadBool(BillsKey, true),
        Verification: ReadBool(VerificationKey, true),
        Drafts: ReadBool(DraftsKey, true),
        TimeOfDay: TimeOnly.TryParseExact(store.GetString(TimeKey), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : ReminderSettings.DefaultTime,
        BillLeadDays: int.TryParse(store.GetString(LeadDaysKey), NumberStyles.Integer, CultureInfo.InvariantCulture, out var lead)
            ? Math.Clamp(lead, 0, ReminderSettings.MaxBillLeadDays)
            : ReminderSettings.DefaultBillLeadDays);

    /// <summary>Stores all values.</summary>
    public void Save(ReminderSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        store.SetString(EnabledKey, Format(settings.Enabled));
        store.SetString(ReadingsKey, Format(settings.Readings));
        store.SetString(BillsKey, Format(settings.Bills));
        store.SetString(VerificationKey, Format(settings.Verification));
        store.SetString(DraftsKey, Format(settings.Drafts));
        store.SetString(TimeKey, settings.Time.ToString("HH:mm", CultureInfo.InvariantCulture));
        store.SetString(LeadDaysKey, settings.LeadDays.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>The day the start summary was shown last, or null.</summary>
    public DateOnly? LastSummary =>
        DateOnly.TryParseExact(store.GetString(LastSummaryKey), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    /// <summary>Remembers that the start summary was shown on <paramref name="day"/>.</summary>
    public void MarkSummaryShown(DateOnly day) => store.SetString(LastSummaryKey, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private bool ReadBool(string key, bool fallback) => store.GetString(key) switch
    {
        "1" => true,
        "0" => false,
        _ => fallback,
    };

    private static string Format(bool value) => value ? "1" : "0";
}
