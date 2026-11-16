using System.Globalization;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// Header summaries of collapsible cards (T-057): "3 profiles", "12 imports · last 10.11.2026". Counts use the plural forms of the
/// UI language through the keys <c>&lt;prefix&gt;_One / _Few / _Many</c> (a language without a "few" form leaves <c>_Few</c> out).
/// </summary>
public static class CardSummaryText
{
    /// <summary>"3 profiles": the count with the noun in the right plural form; "none" for 0.</summary>
    public static string Count(ILocalizer localizer, string keyPrefix, int count)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        if (count <= 0)
        {
            return localizer["CardSummaryNone"];
        }

        var form = DependantsText.Form(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, count);
        var key = $"{keyPrefix}_{form}";
        if (form == "Few" && !localizer.TryGet(key, out _))
        {
            key = $"{keyPrefix}_Many";
        }

        return localizer.Get(key, count.ToString(CultureInfo.CurrentCulture));
    }

    /// <summary>"12 imports · last 10.11.2026" (date only); "none" for 0.</summary>
    public static string CountAndLastDate(ILocalizer localizer, string keyPrefix, int count, DateTimeOffset? last) =>
        count <= 0 || last is null
            ? Count(localizer, keyPrefix, count)
            : localizer.Get("CardSummaryWithLastDate", Count(localizer, keyPrefix, count), last.Value.ToLocalTime());

    /// <summary>"8 copies · last 16.11.2026 03:00" (date and time); "none" for 0.</summary>
    public static string CountAndLastTime(ILocalizer localizer, string keyPrefix, int count, DateTimeOffset? last) =>
        count <= 0 || last is null
            ? Count(localizer, keyPrefix, count)
            : localizer.Get("CardSummaryWithLastTime", Count(localizer, keyPrefix, count), last.Value.ToLocalTime());
}
