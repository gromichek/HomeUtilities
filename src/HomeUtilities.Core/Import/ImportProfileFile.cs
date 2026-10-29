using System.Text.Json;
using System.Text.Json.Serialization;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Import;

/// <summary>Reads and writes a profile as a JSON file so that it can be shared; the file holds no amounts, readings or dates.</summary>
public static class ImportProfileFile
{
    /// <summary>Content type of the exported file.</summary>
    public const string ContentType = "application/json";

    /// <summary>Current format version.</summary>
    public const int Version = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Suggested file name for a profile.</summary>
    public static string SuggestFileName(string name)
    {
        // The same characters on every platform, so that a file made on Android opens on Windows.
        const string invalid = "<>:\"/\\|?*";
        var cleaned = new string([.. (string.IsNullOrWhiteSpace(name) ? "import-profile" : name.Trim()).Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c)]);
        return cleaned + ".import-profile.json";
    }

    /// <summary>Serializes a profile.</summary>
    public static string Write(string name, ImportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(new Envelope { Version = Version, Name = name, Profile = profile }, Options);
    }

    /// <summary>Reads a profile file; never throws for a bad file.</summary>
    /// <returns>The name and profile, or an error (<see cref="ErrorCodes.ImportProfileFileInvalid"/>).</returns>
    public static (string Name, ImportProfile Profile, ValidationError? Error) Read(string json)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<Envelope>(json, Options);
            if (envelope is not { Profile: { } profile } || envelope.Version is < 1 or > Version)
            {
                return (string.Empty, new ImportProfile(), new(ErrorCodes.ImportProfileFileInvalid));
            }

            var name = string.IsNullOrWhiteSpace(envelope.Name) ? profile.SheetName : envelope.Name.Trim();
            return (name.Length > InputLimits.Name ? name[..InputLimits.Name] : name, profile, null);
        }
        catch (JsonException)
        {
            return (string.Empty, new ImportProfile(), new(ErrorCodes.ImportProfileFileInvalid));
        }
    }

    private sealed class Envelope
    {
        public int Version { get; set; }

        public string? Name { get; set; }

        public ImportProfile? Profile { get; set; }
    }
}
