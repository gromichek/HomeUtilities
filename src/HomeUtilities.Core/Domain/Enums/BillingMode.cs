namespace HomeUtilities.Core.Domain;

/// <summary>How a contract is billed.</summary>
public enum BillingMode
{
    /// <summary>By consumption.</summary>
    Actual = 0,

    /// <summary>Fixed monthly amount plus an annual settlement.</summary>
    Equalized = 1,

    /// <summary>Fixed subscription fee.</summary>
    Fixed = 2,

    /// <summary>Taxes: annual amount paid in installments.</summary>
    Assessed = 3,
}
