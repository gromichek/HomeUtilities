using System.Globalization;

namespace HomeUtilities.App.Converters;

/// <summary>Binds "this is the last field" to the keyboard key of an Entry: <c>Done</c> for the last one, <c>Next</c> otherwise.</summary>
public sealed class BoolToReturnTypeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ReturnType.Done : ReturnType.Next;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
