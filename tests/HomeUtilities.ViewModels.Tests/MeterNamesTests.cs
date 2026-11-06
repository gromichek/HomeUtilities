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
