using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Attachments;

/// <summary>The file cannot be stored as an attachment (too large, empty, type not allowed...).</summary>
public sealed class AttachmentRejectedException(ValidationResult result)
    : Exception("The attachment is not valid: " + string.Join(", ", result.Errors.Select(e => e.Code)))
{
    /// <summary>What is wrong with the file.</summary>
    public ValidationResult Result { get; } = result;
}
