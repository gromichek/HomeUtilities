using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HomeUtilities.Data.Conversion;

/// <summary>Value converter and comparer pair that stores a value as a JSON text column.</summary>
internal static class JsonColumn
{
    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { IgnoreChargeLineConsumption } },
        Converters = { new YearMonthJsonConverter() },
    };

    /// <summary>Converter that serializes <typeparamref name="T"/> to JSON text.</summary>
    public static ValueConverter<T, string> Converter<T>()
        where T : class? =>
        new(v => Serialize(v), v => Deserialize<T>(v));

    /// <summary>Comparer that detects in-place changes by comparing the serialized form.</summary>
    public static ValueComparer<T> Comparer<T>()
        where T : class? =>
        new(
            (a, b) => Serialize(a) == Serialize(b),
            v => Serialize(v).GetHashCode(),
            v => Deserialize<T>(Serialize(v)));

    private static string Serialize<T>(T? value) => JsonSerializer.Serialize(value, Options);

    private static T Deserialize<T>(string json)
        where T : class?
    {
        var value = JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new InvalidOperationException("A JSON column contains 'null'.");

        // The consumption details are recomputable and not persisted: restore the non-null empty list.
        return value is CalculatedCharge charge
            ? (T)(object)(charge with { Lines = [.. charge.Lines.Select(l => l with { Consumption = [] })] })
            : value;
    }

    private static void IgnoreChargeLineConsumption(JsonTypeInfo info)
    {
        if (info.Type != typeof(ChargeLine))
        {
            return;
        }

        foreach (var property in info.Properties)
        {
            if (property.Name == nameof(ChargeLine.Consumption))
            {
                property.ShouldSerialize = static (_, _) => false;
            }
        }
    }
}

/// <summary>Writes a <see cref="YearMonth"/> as <c>yyyy-MM</c>; the default struct handling would lose its value.</summary>
internal sealed class YearMonthJsonConverter : JsonConverter<YearMonth>
{
    public override YearMonth Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        YearMonth.Parse(reader.GetString() ?? string.Empty);

    public override void Write(Utf8JsonWriter writer, YearMonth value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
