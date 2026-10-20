namespace HomeUtilities.Core.Billing;

/// <summary>Type of a custom field.</summary>
public enum CustomFieldKind
{
    /// <summary>Free text.</summary>
    Text = 0,

    /// <summary>A number (quantity).</summary>
    Number = 1,

    /// <summary>A money amount.</summary>
    Money = 2,

    /// <summary>A date.</summary>
    Date = 3,
}
