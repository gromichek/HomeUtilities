using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.PdfBills;

/// <summary>The outcome of reading a layouts file.</summary>
/// <param name="Layouts">The layouts of the file; empty when there are errors.</param>
/// <param name="Errors">What is wrong with the file; empty when it is fine.</param>
public sealed record PdfBillLayoutFileResult(IReadOnlyList<PdfBillLayout> Layouts, IReadOnlyList<ValidationError> Errors)
{
    /// <summary>True when the file can be imported.</summary>
    public bool Succeeded => Errors.Count == 0;
}

/// <summary>Reads, writes and validates the JSON file that carries PDF bill layouts (several layouts per file).</summary>
public static class PdfBillLayoutFile
{
    /// <summary>Content type of the file.</summary>
    public const string ContentType = "application/json";

    /// <summary>Current format version.</summary>
    public const int Version = 1;

    /// <summary>Suggested file name of an export.</summary>
    public const string SuggestedFileName = "pdf-bill-layouts.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serializes layouts (only their content: no ids, no timestamps).</summary>
    public static string Write(IEnumerable<PdfBillLayout> layouts)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        var envelope = new Envelope
        {
            Version = Version,
            Layouts =
            [
                .. layouts.Select(l => new LayoutDto
                {
                    Name = l.Name,
                    Markers = [.. l.Markers],
                    Fields = [.. l.Fields.Select(ToDto)],
                    Notes = l.Notes,
                }),
            ],
        };
        return JsonSerializer.Serialize(envelope, Options);
    }

    /// <summary>Reads a layouts file and validates it; never throws for a bad file.</summary>
    public static PdfBillLayoutFileResult Read(string json)
    {
        Envelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<Envelope>(json, Options);
        }
        catch (JsonException)
        {
            return Invalid();
        }

        if (envelope is not { Layouts: { } dtos } || envelope.Version is < 1 or > Version)
        {
            return Invalid();
        }

        var layouts = new List<PdfBillLayout>();
        foreach (var dto in dtos)
        {
            if (dto is null)
            {
                return Invalid();
            }

            layouts.Add(new PdfBillLayout
            {
                Name = Clip(dto.Name?.Trim() ?? string.Empty, InputLimits.Name),
                Markers = [.. (dto.Markers ?? []).Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim())],
                Fields = [.. (dto.Fields ?? []).Where(f => f is not null).Select(FromDto)],
                Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : Clip(dto.Notes.Trim(), InputLimits.Notes),
            });
        }

        var errors = layouts.SelectMany((layout, index) => Validate(layout, index)).ToList();
        return errors.Count > 0 ? new PdfBillLayoutFileResult([], errors) : new PdfBillLayoutFileResult(layouts, []);
    }

    /// <summary>Checks one layout: a name, at least one marker, every pattern a valid regular expression.</summary>
    public static IReadOnlyList<ValidationError> Validate(PdfBillLayout layout, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(layout);

        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(layout.Name))
        {
            errors.Add(new(ErrorCodes.PdfLayoutNameRequired, (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (!layout.Markers.Any(m => !string.IsNullOrWhiteSpace(m)))
        {
            errors.Add(new(ErrorCodes.PdfLayoutMarkersRequired, null, [layout.Name]));
        }

        foreach (var field in layout.Fields)
        {
            Regex? regex = null;
            try
            {
                regex = new Regex(field.Pattern, PdfBillReader.PatternOptions, PdfBillReader.MatchTimeout);
            }
            catch (ArgumentException ex)
            {
                errors.Add(new(ErrorCodes.PdfLayoutPatternInvalid, field.Field.ToString(), [layout.Name, field.Field.ToString(), ex.Message]));
            }

            ValidateOptions(layout, field, regex, errors);
        }

        return errors;
    }

    // component / name / combine / format of one entry.
    private static void ValidateOptions(PdfBillLayout layout, PdfBillFieldPattern entry, Regex? regex, List<ValidationError> errors)
    {
        var field = entry.Field;
        var fieldName = field.ToString();
        if (field is PdfBillField.Quantity or PdfBillField.ComponentAmount && string.IsNullOrWhiteSpace(entry.Component))
        {
            errors.Add(new(ErrorCodes.PdfLayoutComponentRequired, fieldName, [layout.Name, fieldName]));
        }

        if (field == PdfBillField.CustomField && string.IsNullOrWhiteSpace(entry.Name))
        {
            errors.Add(new(ErrorCodes.PdfLayoutFieldNameRequired, fieldName, [layout.Name, fieldName]));
        }

        var combine = entry.Combine;
        var allowed = combine switch
        {
            PdfCombine.First => true,
            PdfCombine.Sum => field is PdfBillField.Gross or PdfBillField.VatPercent or PdfBillField.Vat or PdfBillField.Net
                or PdfBillField.Consumption or PdfBillField.Quantity or PdfBillField.ComponentAmount or PdfBillField.CustomField,
            PdfCombine.All => field is PdfBillField.Notes or PdfBillField.Lines,
            _ => false,
        };
        if (!allowed)
        {
            errors.Add(new(ErrorCodes.PdfLayoutCombineInvalid, fieldName, [layout.Name, fieldName, combine.ToString().ToLowerInvariant()]));
        }

        if (!string.IsNullOrEmpty(entry.Format) && field != PdfBillField.Notes)
        {
            errors.Add(new(ErrorCodes.PdfLayoutFormatInvalid, fieldName, [layout.Name, fieldName]));
        }

        if (field == PdfBillField.Lines && regex is not null && !regex.GetGroupNames().Contains("description", StringComparer.Ordinal))
        {
            errors.Add(new(ErrorCodes.PdfLayoutLinesDescriptionRequired, fieldName, [layout.Name]));
        }
    }

    private static FieldDto ToDto(PdfBillFieldPattern f) => new()
    {
        Field = f.Field,
        Pattern = f.Pattern,
        Component = string.IsNullOrWhiteSpace(f.Component) ? null : f.Component,
        Name = string.IsNullOrWhiteSpace(f.Name) ? null : f.Name,
        Combine = f.Combine == PdfCombine.First ? null : f.Combine.ToString().ToLowerInvariant(),
        Format = string.IsNullOrEmpty(f.Format) ? null : f.Format,
    };

    // An unknown combine word is kept as an invalid value (-1) so that validation reports it.
    private static PdfBillFieldPattern FromDto(FieldDto f) => new()
    {
        Field = f.Field,
        Pattern = f.Pattern ?? string.Empty,
        Component = string.IsNullOrWhiteSpace(f.Component) ? null : f.Component.Trim(),
        Name = string.IsNullOrWhiteSpace(f.Name) ? null : f.Name.Trim(),
        Combine = string.IsNullOrWhiteSpace(f.Combine) ? PdfCombine.First
            : Enum.TryParse<PdfCombine>(f.Combine.Trim(), true, out var combine) && Enum.IsDefined(combine) ? combine : (PdfCombine)(-1),
        Format = string.IsNullOrEmpty(f.Format) ? null : f.Format,
    };

    /// <summary>
    /// Plans an import: a layout with the same name (ignoring case) as a stored one replaces it in place, the others are added.
    /// </summary>
    /// <returns>The stored layouts to update (already changed) and the new layouts to add.</returns>
    public static (IReadOnlyList<PdfBillLayout> Updated, IReadOnlyList<PdfBillLayout> Added) Merge(
        IEnumerable<PdfBillLayout> stored,
        IEnumerable<PdfBillLayout> incoming)
    {
        var existing = stored.Where(l => !l.IsDeleted).ToList();
        var updated = new List<PdfBillLayout>();
        var added = new List<PdfBillLayout>();
        foreach (var layout in incoming)
        {
            var match = existing.FirstOrDefault(l => string.Equals(l.Name.Trim(), layout.Name.Trim(), StringComparison.CurrentCultureIgnoreCase));
            if (match is null)
            {
                existing.Add(layout);
                added.Add(layout);
                continue;
            }

            match.Name = layout.Name;
            match.Markers = [.. layout.Markers];
            match.Fields = [.. layout.Fields];
            match.Notes = layout.Notes;
            if (!updated.Contains(match) && !added.Contains(match))
            {
                updated.Add(match);
            }
        }

        return (updated, added);
    }

    private static PdfBillLayoutFileResult Invalid() => new([], [new ValidationError(ErrorCodes.PdfLayoutFileInvalid)]);

    private static string Clip(string text, int max) => text.Length > max ? text[..max] : text;

    private sealed class Envelope
    {
        public int Version { get; set; }

        public List<LayoutDto?>? Layouts { get; set; }
    }

    private sealed class LayoutDto
    {
        public string? Name { get; set; }

        public List<string>? Markers { get; set; }

        public List<FieldDto>? Fields { get; set; }

        public string? Notes { get; set; }
    }

    private sealed class FieldDto
    {
        public PdfBillField Field { get; set; }

        public string? Pattern { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Component { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Combine { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Format { get; set; }
    }
}
