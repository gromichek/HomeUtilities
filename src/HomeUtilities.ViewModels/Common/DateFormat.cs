using System.Globalization;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// The one format of a full date everywhere in the app: <c>dd.MM.yyyy</c> ("15.03.2023"), whatever the language (UI_GUIDELINES §9).
/// The system's short date pattern is never used: it shows "3/15/2023" in the English UI and the Windows region format
/// in date pickers (C-10).
/// </summary>
public static class DateFormat
{
    /// <summary>The pattern of a full date.</summary>
    public const string Pattern = "dd.MM.yyyy";

    /// <summary>"15.03.2023".</summary>
    public static string Full(DateOnly date) => date.ToString(Pattern, CultureInfo.InvariantCulture);

    /// <summary>"15.03.2023" (the time of day is ignored).</summary>
    public static string Full(DateTime date) => date.ToString(Pattern, CultureInfo.InvariantCulture);
}
