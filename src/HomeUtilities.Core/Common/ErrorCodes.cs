namespace HomeUtilities.Core.Common;

/// <summary>Validation error codes. The UI maps them to localized messages.</summary>
public static class ErrorCodes
{
    /// <summary>Two versioned items share the same ValidFrom date.</summary>
    public const string DuplicateValidFrom = "versioned.duplicate_valid_from";
}
