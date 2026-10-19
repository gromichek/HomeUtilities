using System.Globalization;
using System.Resources;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class ResxLocalizer : ILocalizer
{
    private static readonly ResourceManager _resources = new(
        "HomeUtilities.App.Resources.Strings.AppResources", typeof(ResxLocalizer).Assembly);

    public string this[string key] =>
        _resources.GetString(key, CultureInfo.CurrentUICulture) ?? $"!{key}!";

    public string Get(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, this[key], args);
}
