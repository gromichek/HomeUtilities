namespace HomeUtilities.Core.Billing;

/// <summary>What a bill is.</summary>
public enum BillKind
{
    /// <summary>An ordinary supplier bill.</summary>
    Regular = 0,

    /// <summary>A monthly bill of an equalized plan.</summary>
    Equalized = 1,

    /// <summary>Annual reconciliation of an equalized plan (positive = surcharge, negative = refund).</summary>
    Settlement = 2,

    /// <summary>A correction of an earlier bill.</summary>
    Correction = 3,

    /// <summary>An installment of a property tax.</summary>
    TaxInstallment = 4,

    /// <summary>An expense without a contract (repair, purchase…).</summary>
    OneOff = 5,
}
