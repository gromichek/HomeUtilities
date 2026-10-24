using System.Text.RegularExpressions;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Attachments;

/// <summary>Validation rules of <see cref="Attachment"/>.</summary>
public static partial class AttachmentValidator
{
    /// <summary>Largest file that is stored: 20 MB.</summary>
    public const long MaxSizeBytes = 20L * 1024 * 1024;

    /// <summary>Longest original file name.</summary>
    public const int MaxFileNameLength = InputLimits.Text;

    /// <summary>Checks the metadata including the size and the hash computed by the store.</summary>
    public static ValidationResult Validate(Attachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(attachment.FileName))
        {
            errors.Add(new(ErrorCodes.AttachmentFileNameRequired, nameof(Attachment.FileName)));
        }
        else if (attachment.FileName.Length > MaxFileNameLength)
        {
            errors.Add(new(ErrorCodes.AttachmentFileNameTooLong, nameof(Attachment.FileName), [MaxFileNameLength]));
        }

        if (string.IsNullOrWhiteSpace(attachment.ContentType))
        {
            errors.Add(new(ErrorCodes.AttachmentContentTypeRequired, nameof(Attachment.ContentType)));
        }
        else if (!AttachmentContentTypes.IsAllowed(attachment.ContentType))
        {
            errors.Add(new(ErrorCodes.AttachmentContentTypeNotAllowed, nameof(Attachment.ContentType)));
        }

        if (attachment.SizeBytes <= 0)
        {
            errors.Add(new(ErrorCodes.AttachmentEmpty, nameof(Attachment.SizeBytes)));
        }
        else if (attachment.SizeBytes > MaxSizeBytes)
        {
            errors.Add(new(ErrorCodes.AttachmentTooLarge, nameof(Attachment.SizeBytes), [MaxSizeBytes / (1024 * 1024)]));
        }

        if (!HexSha256().IsMatch(attachment.Sha256 ?? string.Empty))
        {
            errors.Add(new(ErrorCodes.AttachmentHashInvalid, nameof(Attachment.Sha256)));
        }

        if (attachment.Notes is { Length: > InputLimits.Notes })
        {
            errors.Add(new(ErrorCodes.NotesTooLong, nameof(Attachment.Notes), [InputLimits.Notes]));
        }

        return errors.Count == 0 ? ValidationResult.Ok : ValidationResult.Fail([.. errors]);
    }

    [GeneratedRegex("^[0-9a-fA-F]{64}$")]
    private static partial Regex HexSha256();
}
