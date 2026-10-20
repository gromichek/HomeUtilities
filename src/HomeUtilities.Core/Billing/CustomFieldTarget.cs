namespace HomeUtilities.Core.Billing;

/// <summary>Where a custom field is entered.</summary>
public enum CustomFieldTarget
{
    /// <summary>Once per bill.</summary>
    Bill = 0,

    /// <summary>On each bill line.</summary>
    BillLine = 1,
}
