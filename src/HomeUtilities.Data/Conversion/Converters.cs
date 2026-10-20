using HomeUtilities.Core.Common;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HomeUtilities.Data.Conversion;

/// <summary>Stores a <see cref="DateTimeOffset"/> as UTC ticks so that it sorts chronologically.</summary>
internal sealed class DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToTicksConverter()
        : base(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero))
    {
    }
}

/// <summary>Stores a <see cref="YearMonth"/> as <c>Year * 100 + Month</c> (e.g. 202610).</summary>
internal sealed class YearMonthToIntConverter : ValueConverter<YearMonth, int>
{
    public YearMonthToIntConverter()
        : base(v => v.Year * 100 + v.Month, v => new YearMonth(v / 100, v % 100))
    {
    }
}
