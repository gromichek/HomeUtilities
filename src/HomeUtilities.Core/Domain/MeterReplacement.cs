using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Domain;

/// <summary>Replaces a meter by a new one on the same property.</summary>
/// <remarks>
/// Linking the new meter to the contracts of the old one is NOT done here; the caller (service layer) does it.
/// </remarks>
public static class MeterReplacement
{
    /// <summary>Replaces a single-register meter (its <see cref="RegisterKind.Total"/> register).</summary>
    public static MeterReplacementResult Replace(Meter old, DateOnly date, decimal finalValue, decimal initialValue, IClock clock) =>
        Replace(old, date, new Dictionary<RegisterKind, (decimal Final, decimal Initial)>
        {
            [RegisterKind.Total] = (finalValue, initialValue),
        }, clock);

    /// <summary>
    /// Sets <see cref="Meter.RemovedOn"/> of the old meter, adds a Final reading to each of its registers,
    /// creates the new meter (same property, resource, unit and location; same register kinds; no digits
    /// or serial number copied) with an Initial reading per register, and touches everything changed.
    /// </summary>
    /// <param name="old">The meter being replaced.</param>
    /// <param name="date">Replacement date: removal date of the old meter and installation date of the new one.</param>
    /// <param name="values">Final value of the old and initial value of the new register, per register kind.</param>
    /// <param name="clock">Clock for timestamps.</param>
    /// <exception cref="ArgumentException"><paramref name="values"/> does not match the non-deleted registers of the old meter.</exception>
    /// <exception cref="InvalidOperationException">The old meter is already removed.</exception>
    public static MeterReplacementResult Replace(
        Meter old,
        DateOnly date,
        IReadOnlyDictionary<RegisterKind, (decimal Final, decimal Initial)> values,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(clock);

        if (old.RemovedOn is not null)
        {
            throw new InvalidOperationException("The meter is already removed.");
        }

        var oldRegisters = old.Registers.Where(r => !r.IsDeleted).ToList();
        if (oldRegisters.Count != values.Count || oldRegisters.Any(r => !values.ContainsKey(r.Kind)))
        {
            throw new ArgumentException("Values must be given for exactly the registers of the old meter.", nameof(values));
        }

        var newMeter = new Meter
        {
            PropertyId = old.PropertyId,
            Resource = old.Resource,
            Unit = old.Unit,
            Location = old.Location,
            InstalledOn = date,
            ReplacedMeterId = old.Id,
        };

        var finals = new List<MeterReading>();
        var initials = new List<MeterReading>();
        foreach (var oldRegister in oldRegisters)
        {
            var (finalValue, initialValue) = values[oldRegister.Kind];

            var final = new MeterReading
            {
                RegisterId = oldRegister.Id,
                Date = date,
                Value = finalValue,
                Kind = ReadingKind.Final,
            };
            final.Touch(clock);
            oldRegister.Readings.Add(final);
            oldRegister.Touch(clock);
            finals.Add(final);

            var newRegister = new MeterRegister
            {
                MeterId = newMeter.Id,
                Kind = oldRegister.Kind,
                Direction = oldRegister.Direction,
                Name = oldRegister.Name,
            };
            var initial = new MeterReading
            {
                RegisterId = newRegister.Id,
                Date = date,
                Value = initialValue,
                Kind = ReadingKind.Initial,
            };
            initial.Touch(clock);
            newRegister.Readings.Add(initial);
            newRegister.Touch(clock);
            newMeter.Registers.Add(newRegister);
            initials.Add(initial);
        }

        old.RemovedOn = date;
        old.Touch(clock);
        newMeter.Touch(clock);
        return new MeterReplacementResult(newMeter, finals, initials);
    }
}
