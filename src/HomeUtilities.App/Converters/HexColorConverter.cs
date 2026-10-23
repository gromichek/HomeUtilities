using System.Globalization;

namespace HomeUtilities.App.Converters;

/// <summary>Turns a <c>#RRGGBB</c> text into a <see cref="Color"/>; text that is not a colour becomes transparent.</summary>
public sealed class HexColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text && Color.TryParse(text.Trim(), out var color) ? color : Colors.Transparent;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
