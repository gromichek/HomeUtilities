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

    /// <summary>The amount without VAT.</summary>
    Net = 7,

    /// <summary>The start of the service period (set together with <see cref="ServiceTo"/>).</summary>
    ServiceFrom = 8,

    /// <summary>The end of the service period.</summary>
    ServiceTo = 9,

    /// <summary>The bill's title.</summary>
    Title = 10,

    /// <summary>The quantity on the bill («Количество в счёте»).</summary>
    Consumption = 11,

    /// <summary>The quantity of one tariff line taken from the bill; needs <c>component</c>.</summary>
    Quantity = 12,

    /// <summary>The amount of one tariff line taken from the bill; needs <c>component</c>.</summary>
    ComponentAmount = 13,

    /// <summary>The bill's custom field with the name given in <c>name</c>.</summary>
    CustomField = 14,

    /// <summary>The comment.</summary>
    Notes = 15,

    /// <summary>The bill lines; one line per match.</summary>
    Lines = 16,
}
