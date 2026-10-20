namespace HomeUtilities.ViewModels.Common;

/// <summary>Localized names of enum members; resource key <c>Enum_&lt;Type&gt;_&lt;Member&gt;</c>.</summary>
public static class EnumText
{
    public static string KeyFor<TEnum>(TEnum value)
        where TEnum : struct, Enum => $"Enum_{typeof(TEnum).Name}_{value}";

    /// <summary>The localized name, or the member name if the resource is missing.</summary>
    public static string Get<TEnum>(ILocalizer localizer, TEnum value)
        where TEnum : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return localizer.TryGet(KeyFor(value), out var text) ? text : value.ToString();
    }

    /// <summary>All members in declaration order with their localized names.</summary>
    public static IReadOnlyList<EnumOption> Options<TEnum>(ILocalizer localizer)
        where TEnum : struct, Enum =>
        [.. Enum.GetValues<TEnum>().Select(v => new EnumOption(v, Get(localizer, v)))];
}
