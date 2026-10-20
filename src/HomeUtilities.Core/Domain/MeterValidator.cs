using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Validation rules of <see cref="Meter"/>.</summary>
public static class MeterValidator
{
    /// <summary>Validates a meter and its registers (deleted registers are skipped).</summary>
    public static ValidationResult Validate(Meter meter)
    {
        ArgumentNullException.ThrowIfNull(meter);

        var errors = new List<ValidationError>();
        if (meter.PropertyId == Guid.Empty)
        {
            errors.Add(new(ErrorCodes.MeterPropertyRequired, nameof(Meter.PropertyId)));
        }

        if (meter.Digits is < 1 or > 12)
        {
            errors.Add(new(ErrorCodes.MeterDigitsOutOfRange, nameof(Meter.Digits), [meter.Digits]));
        }

        if (meter.RemovedOn is { } removedOn && removedOn < meter.InstalledOn)
        {
            errors.Add(new(ErrorCodes.MeterRemovedBeforeInstalled, nameof(Meter.RemovedOn)));
        }

        if (meter.VerificationIntervalMonths is < 1 or > 600)
        {
            errors.Add(new(ErrorCodes.MeterVerificationIntervalOutOfRange, nameof(Meter.VerificationIntervalMonths), [meter.VerificationIntervalMonths]));
        }

        if (!IsUnitCompatible(meter.Resource, meter.Unit))
        {
            errors.Add(new(ErrorCodes.MeterUnitIncompatible, nameof(Meter.Unit), [meter.Resource, meter.Unit]));
        }

        ValidationHelpers.CheckNotes(errors, meter.Notes, nameof(Meter.Notes));
        ValidateRegisters(meter, errors);
        return ValidationHelpers.ToResult(errors);
    }

    private static void ValidateRegisters(Meter meter, List<ValidationError> errors)
    {
        var seen = new HashSet<(RegisterKind, EnergyDirection)>();
        var any = false;
        for (var i = 0; i < meter.Registers.Count; i++)
        {
            var register = meter.Registers[i];
            if (register.IsDeleted)
            {
                continue;
            }

            any = true;
            var field = $"{nameof(Meter.Registers)}[{i}]";
            if (!seen.Add((register.Kind, register.Direction)))
            {
                errors.Add(new(ErrorCodes.MeterDuplicateRegister, field, [register.Kind, register.Direction]));
            }

            if ((register.Kind == RegisterKind.Export) != (register.Direction == EnergyDirection.Export))
            {
                errors.Add(new(ErrorCodes.MeterExportDirectionMismatch, field, [register.Kind, register.Direction]));
            }
        }

        if (!any)
        {
            errors.Add(new(ErrorCodes.MeterRegisterRequired, nameof(Meter.Registers)));
        }
    }

    private static bool IsUnitCompatible(MeterResource resource, MeterUnit unit) => resource switch
    {
        MeterResource.ColdWater or MeterResource.HotWater or MeterResource.Gas => unit == MeterUnit.M3,
        MeterResource.Electricity => unit == MeterUnit.KWh,
        MeterResource.Heat => unit is MeterUnit.MWh or MeterUnit.Gcal or MeterUnit.KWh,
        _ => true,
    };
}
