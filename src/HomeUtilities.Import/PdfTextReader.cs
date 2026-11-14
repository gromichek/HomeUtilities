using HomeUtilities.Core.Common;
using HomeUtilities.Core.PdfBills;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Exceptions;

namespace HomeUtilities.Import;

/// <summary>Reads the text of a PDF with PdfPig (Apache-2.0, pure managed) and lays it out with <see cref="PdfLineBuilder"/>.</summary>
public sealed class PdfTextReader : IPdfTextReader
{
    /// <inheritdoc />
    public Task<PdfTextResult> ReadAsync(Stream pdf, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        return Task.Run(() => Read(pdf, ct), ct);
    }

    private static PdfTextResult Read(Stream pdf, CancellationToken ct)
    {
        try
        {
            var bytes = ReadAll(pdf, ct);
            if (bytes is null)
            {
                return PdfTextResult.Failed(ErrorCodes.PdfTooLarge);
            }

            using var document = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = true, SkipMissingFonts = true });
            var pages = new List<IReadOnlyList<PdfWord>>();
            foreach (var page in document.GetPages())
            {
                ct.ThrowIfCancellationRequested();
                var words = new List<PdfWord>();
                foreach (var word in page.GetWords())
                {
                    var baseline = word.Letters.Count > 0 ? word.Letters[0].StartBaseLine.Y : word.BoundingBox.Bottom;
                    words.Add(new PdfWord(word.Text, word.BoundingBox.Left, baseline, word.BoundingBox.Width));
                }

                pages.Add(words);
            }

            var text = PdfLineBuilder.Build(pages);
            return string.IsNullOrWhiteSpace(text) ? PdfTextResult.Failed(ErrorCodes.PdfNoText) : new PdfTextResult(text, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PdfDocumentEncryptedException)
        {
            return PdfTextResult.Failed(ErrorCodes.PdfEncrypted);
        }
        catch (Exception)
        {
            // A broken file is an ordinary outcome here, reported to the user as "cannot be read".
            return PdfTextResult.Failed(ErrorCodes.PdfUnreadable);
        }
    }

    // The whole file is read once; null when it is larger than the limit.
    private static byte[]? ReadAll(Stream stream, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            memory.Write(buffer, 0, read);
            if (memory.Length > IPdfTextReader.MaxBytes)
            {
                return null;
            }
        }

        return memory.ToArray();
    }
}
