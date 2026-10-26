using System.Globalization;

namespace HomeUtilities.App.Converters;

/// <summary>
/// Binds a <see cref="DateOnly"/> view-model property to the date of a DatePicker (<c>DateTime</c> in older MAUI versions,
/// <c>DateTime?</c> since .NET MAUI 10).
/// </summary>
public sealed class DateOnlyConverter : IValueConverter
{
    /// <summary>The earliest date a DatePicker can show; anything before it is not a date the user could have picked.</summary>
    private static readonly DateTime Earliest = new(1900, 1, 1);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var date = value is DateOnly dateOnly ? dateOnly.ToDateTime(TimeOnly.MinValue) : DateTime.Today;
        return date < Earliest ? DateTime.Today : date;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime dateTime && dateTime >= Earliest
            ? DateOnly.FromDateTime(dateTime)

            // The picker reported "no date" (null) or a value it cannot hold: keep what the view model has instead of
            // overwriting it with today's date.
            : Binding.DoNothing;
}
