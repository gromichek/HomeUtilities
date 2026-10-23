namespace HomeUtilities.Core.Common;

/// <summary>Maximum lengths of the text fields; the UI uses them as <c>MaxLength</c> so that nobody can type beyond them.</summary>
public static class InputLimits
{
    /// <summary>Names and titles of properties, suppliers, contracts, categories, links, parameters.</summary>
    public const int Name = 100;

    /// <summary>Free-text comments.</summary>
    public const int Notes = 2000;

    /// <summary>Other single-line text (address, phone, e-mail).</summary>
    public const int Text = 200;

    /// <summary>Identifiers: customer / contract / registration numbers, logins, IBAN, BIC.</summary>
    public const int Code = 50;

    /// <summary>Web address.</summary>
    public const int Url = 2000;

    /// <summary>Any payment detail (the bank purpose limit); equals <see cref="Payments.PaymentDetailsValidator.MaxFieldLength"/>.</summary>
    public const int PaymentField = 140;

    /// <summary>Name of a custom field definition.</summary>
    public const int CustomFieldName = 60;

    /// <summary>A number typed as text (area, value).</summary>
    public const int Number = 30;

    /// <summary>A small whole number typed as text (dial digits, months).</summary>
    public const int Count = 4;

    /// <summary>A day of month (1–31).</summary>
    public const int Day = 2;

    /// <summary>A colour as <c>#RRGGBB</c> with some slack for pasted spaces.</summary>
    public const int Color = 10;
}
