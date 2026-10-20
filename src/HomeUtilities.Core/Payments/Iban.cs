using System.Numerics;
using System.Text;

namespace HomeUtilities.Core.Payments;

/// <summary>IBAN helpers (ISO 13616).</summary>
public static class Iban
{
    /// <summary>Removes spaces and upper-cases the input.</summary>
    public static string Normalize(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return string.Concat(input.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
    }

    /// <summary>True if the normalized input has 15–34 characters, a country prefix and a correct mod-97 check.</summary>
    public static bool IsValid(string input)
    {
        var iban = Normalize(input);
        if (iban.Length is < 15 or > 34
            || !IsAsciiLetter(iban[0]) || !IsAsciiLetter(iban[1])
            || !char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3])
            || !iban.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        var digits = new StringBuilder();
        foreach (var c in iban[4..] + iban[..4])
        {
            digits.Append(char.IsAsciiDigit(c) ? c.ToString() : (c - 'A' + 10).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        return BigInteger.Parse(digits.ToString(), System.Globalization.CultureInfo.InvariantCulture) % 97 == 1;
    }

    /// <summary>Normalizes the input and groups it by four characters separated by spaces.</summary>
    public static string Format(string iban)
    {
        var normalized = Normalize(iban);
        return string.Join(' ', Enumerable.Range(0, (normalized.Length + 3) / 4)
            .Select(i => normalized.Substring(i * 4, Math.Min(4, normalized.Length - i * 4))));
    }

    private static bool IsAsciiLetter(char c) => c is >= 'A' and <= 'Z';
}
