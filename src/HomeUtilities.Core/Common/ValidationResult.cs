namespace HomeUtilities.Core.Common;

/// <summary>Outcome of a domain validation: a list of errors, empty when valid.</summary>
public sealed class ValidationResult
{
    private ValidationResult(IReadOnlyList<ValidationError> errors)
    {
        Errors = errors;
    }

    /// <summary>A valid result without errors.</summary>
    public static ValidationResult Ok { get; } = new([]);

    /// <summary>The validation errors; empty when valid.</summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>True when there are no errors.</summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>Creates a result with the given errors.</summary>
    public static ValidationResult Fail(params ValidationError[] errors) => new(errors.ToArray());

    /// <summary>Returns a result containing the errors of both results.</summary>
    public ValidationResult Merge(ValidationResult other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new([.. Errors, .. other.Errors]);
    }
}
