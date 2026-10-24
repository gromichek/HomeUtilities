using System.Globalization;

namespace HomeUtilities.App.Converters;

/// <summary>Turns "a way to open the picture" (a <c>Func&lt;Task&lt;Stream&gt;&gt;</c>) into an image source for a thumbnail.</summary>
public sealed class StreamFactoryImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Func<Task<Stream>> open ? ImageSource.FromStream(_ => open()) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
