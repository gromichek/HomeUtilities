using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Validation rules of <see cref="MeterReading"/> in the context of its register and meter.</summary>
public static class MeterReadingValidator
{
    /// <summary>
    /// Validates a reading against the other non-deleted readings of the register
    /// (the reading itself is excluded by id, so it may or may not be in the list yet).
    /// </summary>
    public static ValidationResult Validate(MeterReading reading, MeterRegister register, Meter meter)
    {
        ArgumentNullException.ThrowIfNull(reading);
        ArgumentNullException.ThrowIfNull(register);
        ArgumentNullException.ThrowIfNull(meter);

        var errors = new List<ValidationError>();
        var others = register.Readings.Where(r => !r.IsDeleted && r.Id != reading.Id).ToList();

        if (reading.Value < 0)
        {
            errors.Add(new(ErrorCodes.MeterReadingValueNegative, nameof(MeterReading.Value)));
        }

        if (reading.Date < meter.InstalledOn || (meter.RemovedOn is { } removedOn && reading.Date > removedOn))
        {
            errors.Add(new(ErrorCodes.MeterReadingDateOutsideInstallation, nameof(MeterReading.Date)));
        }

        CheckInitial(reading, others, errors);
        CheckFinal(reading, others, meter, errors);

        if (others.Any(r => r.Date == reading.Date && r.Kind == reading.Kind))
        {
            errors.Add(new(ErrorCodes.MeterReadingDuplicateOnDate, nameof(MeterReading.Date)));
        }

        ValidationHelpers.CheckNotes(errors, reading.Note, nameof(MeterReading.Note));
        return ValidationHelpers.ToResult(errors);
    }

    private static void CheckInitial(MeterReading reading, List<MeterReading> others, List<ValidationError> errors)
    {
        if (reading.Kind == ReadingKind.Initial)
        {
            if (others.Any(r => r.Kind == ReadingKind.Initial))
            {
                errors.Add(new(ErrorCodes.MeterReadingDuplicateInitial, nameof(MeterReading.Kind)));
            }

            if (others.Any(r => r.Date < reading.Date))
            {
                errors.Add(new(ErrorCodes.MeterReadingInitialNotEarliest, nameof(MeterReading.Date)));
            }
        }
        else if (others.Any(r => r.Kind == ReadingKind.Initial && r.Date > reading.Date))
        {
            errors.Add(new(ErrorCodes.MeterReadingInitialNotEarliest, nameof(MeterReading.Date)));
        }
    }

    private static void CheckFinal(MeterReading reading, List<MeterReading> others, Meter meter, List<ValidationError> errors)
    {
        if (reading.Kind == ReadingKind.Final)
        {
            if (others.Any(r => r.Kind == ReadingKind.Final))
            {
                errors.Add(new(ErrorCodes.MeterReadingDuplicateFinal, nameof(MeterReading.Kind)));
            }

            if (others.Any(r => r.Date > reading.Date))
            {
                errors.Add(new(ErrorCodes.MeterReadingFinalNotLatest, nameof(MeterReading.Date)));
            }

            if (meter.RemovedOn is { } removedOn && reading.Date != removedOn)
            {
                errors.Add(new(ErrorCodes.MeterReadingFinalDateMismatch, nameof(MeterReading.Date)));
            }
        }
        else if (others.Any(r => r.Kind == ReadingKind.Final && r.Date < reading.Date))
        {
            errors.Add(new(ErrorCodes.MeterReadingFinalNotLatest, nameof(MeterReading.Date)));
        }
    }
}
