namespace HomeUtilities.Core.Attachments;

/// <summary>The content types attachments may have and their file extensions.</summary>
public static class AttachmentContentTypes
{
    /// <summary>JPEG image.</summary>
    public const string Jpeg = "image/jpeg";

    /// <summary>PNG image.</summary>
    public const string Png = "image/png";

    /// <summary>HEIC image (iPhone / some Android cameras).</summary>
    public const string Heic = "image/heic";

    /// <summary>PDF document.</summary>
    public const string Pdf = "application/pdf";

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = Jpeg,
        [".jpeg"] = Jpeg,
        [".png"] = Png,
        [".heic"] = Heic,
        [".pdf"] = Pdf,
    };

    private static readonly Dictionary<string, string> ExtensionOf = new(StringComparer.OrdinalIgnoreCase)
    {
        [Jpeg] = ".jpg",
        [Png] = ".png",
        [Heic] = ".heic",
        [Pdf] = ".pdf",
    };

    /// <summary>The allowed content types.</summary>
    public static IReadOnlyList<string> Allowed { get; } = [Jpeg, Png, Heic, Pdf];

    /// <summary>True for an allowed content type (case-insensitive).</summary>
    public static bool IsAllowed(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && ExtensionOf.ContainsKey(contentType.Trim());

    /// <summary>True for the image types.</summary>
    public static bool IsImage(string? contentType) =>
        contentType is not null && contentType.Trim().StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    /// <summary>The content type for a file name by its extension, or null when the extension is not an allowed one.</summary>
    public static string? FromFileName(string? fileName) =>
        !string.IsNullOrWhiteSpace(fileName) && ByExtension.TryGetValue(Path.GetExtension(fileName.Trim()), out var type) ? type : null;

    /// <summary>The extension (with the dot) the file of an allowed content type is stored with; null for other types.</summary>
    public static string? ExtensionFor(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && ExtensionOf.TryGetValue(contentType.Trim(), out var ext) ? ext : null;
}
