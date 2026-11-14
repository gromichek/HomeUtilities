namespace HomeUtilities.Core.PdfBills;

/// <summary>A header field of a bill that a <see cref="PdfBillLayout"/> can read from the supplier's PDF.</summary>
public enum PdfBillField
{
    /// <summary>The bill number.</summary>
    Number = 0,

    /// <summary>The date the bill was issued.</summary>
    IssuedOn = 1,

    /// <summary>The service period (a month).</summary>
    Period = 2,

    /// <summary>The due date.</summary>
    DueOn = 3,

    /// <summary>The total to pay for the period (including VAT).</summary>
    Gross = 4,

    /// <summary>The VAT rate in percent.</summary>
    VatPercent = 5,

    /// <summary>The VAT amount.</summary>
    Vat = 6,
}
