using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Import;

/// <summary>Checks that a profile can be planned: the period column and something to import are mapped.</summary>
public static class ImportProfileValidator
{
    /// <summary>The highest column Excel has (XFD).</summary>
    public const int MaxColumn = 16384;

    /// <summary>Validates the profile on its own (without looking at the contract's meters).</summary>
    public static ValidationResult Validate(ImportProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var errors = new List<ValidationError>();
        if (profile.ContractId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.ImportContractRequired, nameof(ImportProfile.ContractId)));
        }

        if (profile.PeriodColumn is < 1 or > MaxColumn)
        {
            errors.Add(new(ErrorCodes.ImportPeriodColumnRequired, nameof(ImportProfile.PeriodColumn)));
        }

        if (profile.HeaderRow < 0
            || profile.FirstDataRow < 1
            || profile.FirstDataRow <= profile.HeaderRow
            || (profile.LastDataRow is { } last && last < profile.FirstDataRow))
        {
            errors.Add(new(ErrorCodes.ImportRowsInvalid, nameof(ImportProfile.FirstDataRow)));
        }

        var anything = profile.TotalColumn > 0
            || profile.NetColumn > 0
            || profile.VatColumn > 0
            || profile.Registers.Any(r => r.ReadingColumn > 0);
        if (!anything)
        {
            errors.Add(new(ErrorCodes.ImportNothingMapped));
        }

        if (profile.PaidColumn > 0 && string.IsNullOrWhiteSpace(profile.PaidMarker))
        {
            errors.Add(new(ErrorCodes.ImportPaidMarkerRequired, nameof(ImportProfile.PaidMarker)));
        }

        return errors.Count == 0 ? ValidationResult.Ok : ValidationResult.Fail([.. errors]);
    }
}
