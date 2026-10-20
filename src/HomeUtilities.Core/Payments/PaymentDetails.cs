using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Payments;

/// <summary>Bank details for paying a supplier.</summary>
/// <param name="RecipientName">Name of the payee.</param>
/// <param name="RecipientRegistrationNumber">Registration number of the payee.</param>
/// <param name="Iban">IBAN, stored without spaces.</param>
/// <param name="Bic">BIC / SWIFT code.</param>
/// <param name="BankName">Name of the bank.</param>
/// <param name="PurposeTemplate">Payment purpose with placeholders, see <see cref="PaymentPurpose"/>.</param>
public sealed record PaymentDetails(
    string? RecipientName,
    string? RecipientRegistrationNumber,
    string? Iban,
    string? Bic,
    string? BankName,
    string? PurposeTemplate)
{
    /// <summary>Details with every field null.</summary>
    public static PaymentDetails Empty { get; } = new(null, null, null, null, null, null);

    /// <summary>Per field: the contract's value if it is not null or blank, else the supplier's.</summary>
    public static PaymentDetails Effective(Contract contract, Supplier supplier)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(supplier);

        var c = contract.PaymentDetails ?? Empty;
        var s = supplier.PaymentDetails ?? Empty;
        return new PaymentDetails(
            Pick(c.RecipientName, s.RecipientName),
            Pick(c.RecipientRegistrationNumber, s.RecipientRegistrationNumber),
            Pick(c.Iban, s.Iban),
            Pick(c.Bic, s.Bic),
            Pick(c.BankName, s.BankName),
            Pick(c.PurposeTemplate, s.PurposeTemplate));
    }

    private static string? Pick(string? preferred, string? fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
