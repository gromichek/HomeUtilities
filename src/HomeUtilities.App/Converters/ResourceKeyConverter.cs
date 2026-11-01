using System.Globalization;

namespace HomeUtilities.App.Converters;

/// <summary>
/// Looks up a text resource (an icon glyph defined in <c>Icons.xaml</c>) by its key; an unknown key gives an empty text.
/// Lets a list row choose its icon from data without one trigger per icon.
/// </summary>
public sealed class ResourceKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || Application.Current?.Resources.TryGetValue(key, out var resource) != true)
        {
            return string.Empty;
        }

        return resource switch
        {
            OnPlatform<string> platform => (string)platform,
            string text => text,
            _ => string.Empty,
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
