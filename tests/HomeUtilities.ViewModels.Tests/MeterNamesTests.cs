using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>R-31: two meters in one room can be told apart by their title.</summary>
public sealed class MeterNamesTests
{
    private static Meter Meter(string location, string serial, params RegisterKind[] kinds) => new()
    {
        Location = location,
        SerialNumber = serial,
        Registers = [.. kinds.Select(k => new MeterRegister { Kind = k })],
    };

    [Fact]
    public void Title_LocationAndSerial()
    {
        var title = MeterNames.Title(ResxLocalizer.English, Meter("Bathroom", "CW-1", RegisterKind.Total));

        Assert.Equal("Bathroom · CW-1", title);
    }

    [Fact]
    public void Title_TwoMetersInOneRoom_DiffersBySerial()
    {
        var cold = MeterNames.Title(ResxLocalizer.English, Meter("Bathroom", "CW-1", RegisterKind.Total));
        var hot = MeterNames.Title(ResxLocalizer.English, Meter("Bathroom", "HW-7", RegisterKind.Total));

        Assert.NotEqual(cold, hot);
    }

    [Fact]
    public void Title_NoSerialMultiRegister_LocationAndRegisters()
    {
        var title = MeterNames.Title(ResxLocalizer.English, Meter("Hall", "", RegisterKind.Day, RegisterKind.Night));

        Assert.Equal("Hall · Day / Night", title);
    }

    [Fact]
    public void Title_NoSerialSingleTotalRegister_LocationOnly()
    {
        Assert.Equal("Hall", MeterNames.Title(ResxLocalizer.English, Meter("Hall", " ", RegisterKind.Total)));
    }

    [Fact]
    public void Title_NoLocation_MeterWithSerial_OrUnnamed()
    {
        Assert.Equal("Meter 55", MeterNames.Title(ResxLocalizer.English, Meter("", "55", RegisterKind.Total)));
        Assert.Equal("Meter", MeterNames.Title(ResxLocalizer.English, Meter("", "", RegisterKind.Total)));
    }

    [Fact]
    public void ShortTitle_IsTheLocationOnly()
    {
        Assert.Equal("Bathroom", MeterNames.ShortTitle(ResxLocalizer.English, Meter("Bathroom", "CW-1", RegisterKind.Total)));
    }
}

/// <summary>C-18: a number and its unit never wrap apart.</summary>
public sealed class UnitFormatTests
{
    [Fact]
    public void WithUnit_JoinsNumberAndUnitWithANonBreakingSpace()
    {
        var saved = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            Assert.Equal("1.267\u00A0m³", BillText.WithUnit(1.267m, "N3", "m³"));
            Assert.Equal("54.7\u00A0m²", BillText.WithUnit("54.7", "m²"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = saved;
        }
    }

    [Fact]
    public void WithUnit_WithoutUnit_IsJustTheNumber() => Assert.Equal("5", BillText.WithUnit("5", ""));
}

/// <summary>C-15: the two history lines of a replacement read alike.</summary>
public sealed class MeterHistoryTextTests
{
    [Fact]
    public void ReplacementLines_RemovedAndInstalled_UseTheSameWording()
    {
        var date = new DateOnly(2026, 3, 15);

        var ru = System.Globalization.CultureInfo.GetCultureInfo("ru-RU");
        Assert.Equal(string.Format(ru, "Замена: снят счётчик № CW-1 · {0:d}", date), ResxLocalizer.Russian.Get("MeterHistoryReplaced", "CW-1", date));
        Assert.Equal(
            string.Format(ru, "Замена: установлен счётчик № CW-2 · {0:d}", date),
            ResxLocalizer.Russian.Get("MeterHistoryReplacedBy", "CW-2", date));
        Assert.StartsWith("Replacement: installed meter no. CW-2", ResxLocalizer.English.Get("MeterHistoryReplacedBy", "CW-2", date));
    }
}
