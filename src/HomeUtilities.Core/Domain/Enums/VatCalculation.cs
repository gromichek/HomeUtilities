namespace HomeUtilities.Core.Domain;

/// <summary>How a supplier calculates VAT on its bills.</summary>
public enum VatCalculation
{
    /// <summary>VAT is calculated on the total of each VAT percent group (most suppliers).</summary>
    OnTotal = 0,

    /// <summary>VAT is rounded per line and the line VATs are added up.</summary>
    PerLine = 1,
}
