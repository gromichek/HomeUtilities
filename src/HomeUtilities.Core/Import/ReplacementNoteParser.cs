using System.Globalization;
using System.Text.RegularExpressions;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Import;

/// <summary>What a note of the sheet says about one replaced meter.</summary>
/// <param name="Resource">Cold or hot water if the note names it (Auksta/Karsta, холодная/горячая, cold/hot); otherwise null.</param>
/// <param name="SerialNumber">Serial number of the new meter, if the note gives it.</param>
/// <param name="OldFinal">Final reading of the old meter (the first <c>rad:</c> value), if given.</param>
/// <param name="NewInitial">Initial reading of the new meter (the second <c>rad:</c> value), if given.</param>
public sealed record NoteMeterEntry(MeterResource? Resource, string? SerialNumber, decimal? OldFinal, decimal? NewInitial);

/// <summary>The content of a replacement note.</summary>
/// <param name="Date">Replacement date found in the text.</param>
/// <param name="Meters">Meters the note names, in the order of the text.</param>
public sealed record ReplacementNote(DateOnly? Date, IReadOnlyList<NoteMeterEntry> Meters)
{
    /// <summary>A note without any information.</summary>
    public static ReplacementNote None { get; } = new(null, []);

    /// <summary>True when the note has no date and no meter data.</summary>
    public bool IsEmpty => Date is null && Meters.Count == 0;
}

/// <summary>
/// Reads the free text next to a replacement row, e.g. <c>поменяли счетчики 30.04.2015</c> or
/// <c>skaititaja nr.: Auksta - 1210007 rad:398,054 rad:0,072</c> (invented numbers). Never throws.
/// </summary>
public static partial class ReplacementNoteParser
{
    [GeneratedRegex(@"(?<![\d.])(\d{1,2})[./](\d{1,2})[./](\d{4}|\d{2})(?![\d])")]
    private static partial Regex DatePattern();

    [GeneratedRegex(@"(auksta\w*|karsta\w*|холодн\w*|горяч\w*|cold|hot)", RegexOptions.IgnoreCase)]
    private static partial Regex ResourcePattern();

    [GeneratedRegex(@"(?:rad|rād|rādījums|показ\w*|reading)\s*[.:]?\s*:?\s*(-?\d+(?:[.,]\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex ValuePattern();

    [GeneratedRegex(@"(?:nr\.?|№|no[.:]|#)\s*:?\s*([A-Za-z0-9/-]*\d[A-Za-z0-9/-]{2,})", RegexOptions.IgnoreCase)]
    private static partial Regex SerialPattern();

    [GeneratedRegex(@"[-–—:]\s*([A-Za-z0-9][A-Za-z0-9/-]{4,})")]
    private static partial Regex SerialAfterDashPattern();

    /// <summary>Parses a note.</summary>
    public static ReplacementNote Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ReplacementNote.None;
        }

        DateOnly? date = null;
        foreach (Match match in DatePattern().Matches(text))
        {
            if (TryDate(match, out var parsed))
            {
                date = parsed;
                break;
            }
        }

        var meters = new List<NoteMeterEntry>();
        var keywords = ResourcePattern().Matches(text).Cast<Match>().Where(m => ResourceOf(m.Value) is not null).ToList();
        if (keywords.Count == 0)
        {
            var entry = Entry(null, text);
            if (entry is not null)
            {
                meters.Add(entry);
            }
        }
        else
        {
            for (var i = 0; i < keywords.Count; i++)
            {
                var start = keywords[i].Index + keywords[i].Length;
                var end = i + 1 < keywords.Count ? keywords[i + 1].Index : text.Length;
                var entry = Entry(ResourceOf(keywords[i].Value), text[start..end]);
                if (entry is not null)
                {
                    meters.Add(entry);
                }
            }
        }

        return new ReplacementNote(date, meters);
    }

    private static NoteMeterEntry? Entry(MeterResource? resource, string segment)
    {
        var values = new List<decimal>();
        foreach (Match match in ValuePattern().Matches(segment))
        {
            if (ImportCellParser.TryParseDecimalText(match.Groups[1].Value, out var value))
            {
                values.Add(value);
            }
        }

        string? serial = null;
        var serialMatch = resource is null ? SerialPattern().Match(segment) : SerialAfterDashPattern().Match(segment);
        if (serialMatch.Success && !ValuePattern().IsMatch(serialMatch.Value))
        {
            serial = serialMatch.Groups[1].Value;
        }
        else if (resource is not null && SerialPattern().Match(segment) is { Success: true } labelled)
        {
            serial = labelled.Groups[1].Value;
        }

        if (values.Count == 0 && serial is null)
        {
            return resource is null ? null : new NoteMeterEntry(resource, null, null, null);
        }

        return new NoteMeterEntry(
            resource,
            serial,
            values.Count > 0 ? values[0] : null,
            values.Count > 1 ? values[1] : null);
    }

    private static MeterResource? ResourceOf(string word)
    {
        var w = word.ToLowerInvariant();
        if (w.StartsWith("auksta", StringComparison.Ordinal) || w.StartsWith("холодн", StringComparison.Ordinal) || w == "cold")
        {
            return MeterResource.ColdWater;
        }

        if (w.StartsWith("karsta", StringComparison.Ordinal) || w.StartsWith("горяч", StringComparison.Ordinal) || w == "hot")
        {
            return MeterResource.HotWater;
        }

        return null;
    }

    private static bool TryDate(Match match, out DateOnly date)
    {
        date = default;
        var day = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        if (year < 100)
        {
            year += year <= 69 ? 2000 : 1900;
        }

        if (year is < 1900 or > 2200 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
        {
            return false;
        }

        date = new DateOnly(year, month, day);
        return true;
    }
}
