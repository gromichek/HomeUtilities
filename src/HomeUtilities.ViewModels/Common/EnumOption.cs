namespace HomeUtilities.ViewModels.Common;

/// <summary>An enum member with its localized name, for pickers.</summary>
public sealed record EnumOption(object Value, string DisplayName)
{
    public TEnum As<TEnum>()
        where TEnum : struct, Enum => (TEnum)Value;
}
