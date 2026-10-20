using HomeUtilities.Core.Common;

namespace HomeUtilities.ViewModels.Common;

/// <summary>Turns a <see cref="ValidationError"/> into a localized message.</summary>
public static class ValidationText
{
    /// <summary>Resource key of the message shown for codes without a resource.</summary>
    public const string GenericKey = "Error_Generic";

    /// <summary>Resource key of a code: <c>Error_</c> plus the code with dots replaced by underscores.</summary>
    public static string KeyFor(string code) => "Error_" + code.Replace('.', '_');

    public static string For(ILocalizer localizer, ValidationError error)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(error);

        var key = KeyFor(error.Code);
        if (!localizer.TryGet(key, out _))
        {
            var generic = localizer[GenericKey];
#if DEBUG
            generic += $" ({error.Code})";
#endif
            return generic;
        }

        return error.Args is { Length: > 0 }
            ? localizer.Get(key, [.. error.Args.Select(a => a ?? string.Empty)])
            : localizer[key];
    }
}
