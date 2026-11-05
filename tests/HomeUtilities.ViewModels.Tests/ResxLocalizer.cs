using System.Globalization;
using System.Xml.Linq;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// The real texts of the app (<c>AppResources.resx</c> / <c>AppResources.ru.resx</c>) as an <see cref="ILocalizer"/>, so a test can
/// check the wording the owner sees without MAUI. A missing key shows as <c>!key!</c> like in the app.
/// </summary>
internal sealed class ResxLocalizer : ILocalizer
{
    private readonly Dictionary<string, string> _texts;
    private readonly CultureInfo _culture;

    private ResxLocalizer(Dictionary<string, string> texts, CultureInfo culture)
    {
        _texts = texts;
        _culture = culture;
    }

    /// <summary>English (the neutral resource file).</summary>
    public static ResxLocalizer English { get; } = Load("AppResources.resx", new CultureInfo("en-US"));

    /// <summary>Russian.</summary>
    public static ResxLocalizer Russian { get; } = Load("AppResources.ru.resx", new CultureInfo("ru-RU"));

    /// <summary>The keys of the resource file.</summary>
    public IReadOnlyCollection<string> Keys => _texts.Keys;

    public string this[string key] => _texts.TryGetValue(key, out var value) ? value : $"!{key}!";

    public bool TryGet(string key, out string value) => _texts.TryGetValue(key, out value!) && value.Length > 0;

    public string Get(string key, params object[] args) => string.Format(_culture, this[key], args);

    private static ResxLocalizer Load(string file, CultureInfo culture)
    {
        var directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "HomeUtilities.sln")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);
        var path = Path.Combine(directory, "src", "HomeUtilities.App", "Resources", "Strings", file);
        var texts = XDocument.Load(path).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);
        return new ResxLocalizer(texts, culture);
    }
}
