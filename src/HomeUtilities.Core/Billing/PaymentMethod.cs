namespace HomeUtilities.Core.Billing;

/// <summary>How a payment was made.</summary>
public enum PaymentMethod
{
    /// <summary>Bank transfer.</summary>
    BankTransfer = 0,

    /// <summary>Card payment.</summary>
    Card = 1,

    /// <summary>Cash.</summary>
    Cash = 2,

    /// <summary>Direct debit.</summary>
    DirectDebit = 3,

    /// <summary>Anything else.</summary>
    Other = 4,
}
