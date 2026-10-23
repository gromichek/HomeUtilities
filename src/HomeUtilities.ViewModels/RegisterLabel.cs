using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Display label of a register in the readings screens.</summary>
public static class RegisterLabel
{
    /// <summary>The localized register kind (Day, Night, Export...); empty for the only, Total register of a meter.</summary>
    public static string For(ILocalizer localizer, Meter meter, MeterRegister register)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(meter);
        ArgumentNullException.ThrowIfNull(register);

        var registers = meter.Registers.Count(r => !r.IsDeleted);
        return registers == 1 && register.Kind == RegisterKind.Total ? string.Empty : EnumText.Get(localizer, register.Kind);
    }
}
