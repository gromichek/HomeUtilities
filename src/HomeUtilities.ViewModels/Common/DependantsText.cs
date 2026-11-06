using System.Globalization;
using HomeUtilities.Core.Abstractions;

namespace HomeUtilities.ViewModels.Common;

/// <summary>
/// The list of dependent records in a delete confirmation (UI_GUIDELINES §5.1): "12 bills, 3 payments, 1 tariff". Resource keys
/// <c>Dependant_&lt;Kind&gt;_One / _Few / _Many</c>; a language without a "few" form (English) leaves <c>_Few</c> out.
/// </summary>
public static class DependantsText
{
    /// <summary>"12 счетов, 3 платежа, 1 тариф"; empty when there are no dependants.</summary>
    public static string List(ILocalizer localizer, IEnumerable<Dependant> dependants)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(dependants);
        return string.Join(", ", dependants.Where(d => d.Count > 0).Select(d => Phrase(localizer, d)));
    }

    /// <summary>"3 платежа": the count with the noun in the right plural form for the UI language.</summary>
    public static string Phrase(ILocalizer localizer, Dependant dependant)
    {
        var form = Form(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, dependant.Count);
        var key = $"Dependant_{dependant.Kind}_{form}";
        if (form == "Few" && !localizer.TryGet(key, out _))
        {
            key = $"Dependant_{dependant.Kind}_Many";
        }

        return localizer.Get(key, dependant.Count.ToString(CultureInfo.CurrentCulture));
    }

    /// <summary>The plural form name: One, Few or Many (Russian and Ukrainian rules; others: 1 is One).</summary>
    public static string Form(string language, int count)
    {
        var n = Math.Abs(count);
        if (language is "ru" or "uk")
        {
            if (n % 10 == 1 && n % 100 != 11)
            {
                return "One";
            }

            return n % 10 is >= 2 and <= 4 && n % 100 is < 12 or > 14 ? "Few" : "Many";
        }

        return n == 1 ? "One" : "Many";
    }
}
