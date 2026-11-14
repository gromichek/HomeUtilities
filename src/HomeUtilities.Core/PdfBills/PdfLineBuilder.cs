using System.Text;

namespace HomeUtilities.Core.PdfBills;

/// <summary>One word of a PDF page: its text and where it sits (PDF units, origin at the bottom left).</summary>
/// <param name="Text">The word.</param>
/// <param name="X">Left edge.</param>
/// <param name="Y">Baseline; larger means higher on the page.</param>
/// <param name="Width">Width of the word.</param>
public readonly record struct PdfWord(string Text, double X, double Y, double Width);

/// <summary>
/// Turns the words of PDF pages into text with one line per visual line, like <c>pdftotext -layout</c>: words are grouped
/// by their baseline, ordered left to right, joined by one space — or by at least two spaces across a wide gap (a
/// column) — and lines run top to bottom. Pages are separated by an empty line.
/// </summary>
public static class PdfLineBuilder
{
    /// <summary>Baselines closer than this (in PDF units) belong to the same line.</summary>
    public const double BaselineTolerance = 2.0;

    /// <summary>A gap wider than this many average character widths is a column gap.</summary>
    public const double ColumnGapFactor = 1.5;

    /// <summary>Builds the text of all pages.</summary>
    public static string Build(IEnumerable<IReadOnlyList<PdfWord>> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var builder = new StringBuilder();
        var first = true;
        foreach (var page in pages)
        {
            var text = BuildPage(page);
            if (!first)
            {
                builder.Append('\n');
            }

            first = false;
            builder.Append(text);
        }

        return builder.ToString();
    }

    /// <summary>Builds the lines of one page (each line ends with a line feed).</summary>
    public static string BuildPage(IReadOnlyList<PdfWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);

        var builder = new StringBuilder();
        var remaining = words.Where(w => !string.IsNullOrWhiteSpace(w.Text)).OrderByDescending(w => w.Y).ThenBy(w => w.X).ToList();
        var index = 0;
        while (index < remaining.Count)
        {
            var baseline = remaining[index].Y;
            var line = new List<PdfWord>();
            while (index < remaining.Count && baseline - remaining[index].Y <= BaselineTolerance)
            {
                line.Add(remaining[index]);
                index++;
            }

            AppendLine(builder, line);
        }

        return builder.ToString();
    }

    private static void AppendLine(StringBuilder builder, List<PdfWord> line)
    {
        line.Sort((a, b) => a.X.CompareTo(b.X));
        var previous = line[0];
        builder.Append(previous.Text);
        for (var i = 1; i < line.Count; i++)
        {
            var word = line[i];
            var gap = word.X - (previous.X + previous.Width);
            var charWidth = Math.Max(CharWidth(previous), CharWidth(word));
            if (charWidth > 0 && gap > charWidth * ColumnGapFactor)
            {
                builder.Append(' ', Math.Max(2, (int)Math.Round(gap / charWidth)));
            }
            else
            {
                builder.Append(' ');
            }

            builder.Append(word.Text);
            previous = word;
        }

        builder.Append('\n');
    }

    private static double CharWidth(PdfWord word) => word.Text.Length == 0 ? 0 : word.Width / word.Text.Length;
}
