using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.PdfBills;

/// <summary>The text of a PDF, or why it could not be read.</summary>
/// <param name="Text">Text with one line per visual line, pages separated by an empty line; empty on an error.</param>
/// <param name="Error">The error (a <c>pdf.*</c> code of <see cref="ErrorCodes"/>), or null.</param>
public sealed record PdfTextResult(string Text, ValidationError? Error)
{
    /// <summary>True when the text was read.</summary>
    public bool Succeeded => Error is null;

    /// <summary>A failed read.</summary>
    public static PdfTextResult Failed(string code) => new(string.Empty, new ValidationError(code));
}

/// <summary>Extracts the text of a PDF file. Never throws for a broken, encrypted or image-only file.</summary>
public interface IPdfTextReader
{
    /// <summary>Largest PDF that is read (bytes).</summary>
    public const long MaxBytes = 20L * 1024 * 1024;

    /// <summary>Reads the text; the stream is read once, completely. Runs off the calling thread.</summary>
    Task<PdfTextResult> ReadAsync(Stream pdf, CancellationToken ct = default);
}
