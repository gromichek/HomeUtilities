using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Tests.Tariffs;

public class VatRateValidatorTests
{
    private static VatRate Valid() => new()
    {
        Code = "LV-STANDARD",
        Name = "Standard rate",
        Versions = [new() { ValidFrom = new(2012, 7, 1), Percent = 21m }],
    };

    private static string[] Codes(VatRate rate) => [.. VatRateValidator.Validate(rate).Errors.Select(e => e.Code)];

    [Fact]
    public void Valid_rate_passes() => Assert.True(VatRateValidator.Validate(Valid()).IsValid);

    [Fact]
    public void Defaults_are_valid() =>
        Assert.All(LatvianVatDefaults.Create(), r => Assert.True(VatRateValidator.Validate(r).IsValid));

    [Theory]
    [InlineData("lv standard")]
    [InlineData("lv-standard")]
    [InlineData("")]
    [InlineData("TOO-LONG-0123456789-0123456789-0123")]
    public void Bad_code_fails(string code)
    {
        var rate = Valid();
        rate.Code = code;
        Assert.Equal([ErrorCodes.VatRateCodeInvalid], Codes(rate));
    }

    [Fact]
    public void Blank_name_fails()
    {
        var rate = Valid();
        rate.Name = " ";
        Assert.Equal([ErrorCodes.VatRateNameRequired], Codes(rate));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(-1)]
    public void Percent_out_of_range_fails(int percent)
    {
        var rate = Valid();
        rate.Versions[0].Percent = percent;
        Assert.Equal([ErrorCodes.VatPercentOutOfRange], Codes(rate));
    }

    [Fact]
    public void Percent_bounds_pass()
    {
        var rate = Valid();
        rate.Versions.Add(new() { ValidFrom = new(2020, 1, 1), Percent = 0m });
        rate.Versions.Add(new() { ValidFrom = new(2021, 1, 1), Percent = 100m });
        Assert.True(VatRateValidator.Validate(rate).IsValid);
    }

    [Fact]
    public void Duplicate_valid_from_fails()
    {
        var rate = Valid();
        rate.Versions.Add(new() { ValidFrom = new(2012, 7, 1), Percent = 22m });
        Assert.Equal([ErrorCodes.DuplicateValidFrom], Codes(rate));
    }
}
