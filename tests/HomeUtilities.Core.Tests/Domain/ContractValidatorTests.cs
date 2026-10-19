using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class ContractValidatorTests
{
    private static Contract Valid() => new()
    {
        PropertyId = Guid.CreateVersion7(),
        SupplierId = Guid.CreateVersion7(),
        Title = "Water",
        ValidFrom = new DateOnly(2024, 1, 1),
    };

    private static ValidationError Single(Contract c) => Assert.Single(ContractValidator.Validate(c).Errors);

    [Fact]
    public void Valid_contract_passes()
    {
        var c = Valid();
        c.ValidTo = new DateOnly(2024, 1, 1);
        c.ReadingDayFrom = 26;
        c.ReadingDayTo = 3;
        c.BillDay = 31;
        c.PaymentDueDay = 1;
        c.Links = [new Link { Url = "https://example.com" }];
        Assert.True(ContractValidator.Validate(c).IsValid);
    }

    [Fact]
    public void Empty_property_fails()
    {
        var c = Valid();
        c.PropertyId = Guid.Empty;
        Assert.Equal(ErrorCodes.ContractPropertyRequired, Single(c).Code);
    }

    [Fact]
    public void Empty_supplier_fails()
    {
        var c = Valid();
        c.SupplierId = Guid.Empty;
        Assert.Equal(ErrorCodes.ContractSupplierRequired, Single(c).Code);
    }

    [Fact]
    public void Blank_title_fails()
    {
        var c = Valid();
        c.Title = " ";
        Assert.Equal(ErrorCodes.ContractTitleRequired, Single(c).Code);
    }

    [Fact]
    public void Title_101_chars_fails_and_100_passes()
    {
        var c = Valid();
        c.Title = new string('a', 100);
        Assert.True(ContractValidator.Validate(c).IsValid);

        c.Title = new string('a', 101);
        Assert.Equal(ErrorCodes.ContractTitleTooLong, Single(c).Code);
    }

    [Fact]
    public void ValidTo_before_valid_from_fails()
    {
        var c = Valid();
        c.ValidTo = new DateOnly(2023, 12, 31);
        var error = Single(c);
        Assert.Equal(ErrorCodes.ContractValidToBeforeValidFrom, error.Code);
        Assert.Equal("ValidTo", error.Field);
    }

    [Theory]
    [InlineData("ReadingDayFrom", 0)]
    [InlineData("ReadingDayTo", 32)]
    [InlineData("BillDay", -1)]
    [InlineData("PaymentDueDay", 32)]
    public void Day_out_of_range_fails(string field, int day)
    {
        var c = Valid();
        c.ReadingDayFrom = 5;
        c.ReadingDayTo = 10;
        typeof(Contract).GetProperty(field)!.SetValue(c, day);

        var error = Single(c);
        Assert.Equal(ErrorCodes.ContractDayOutOfRange, error.Code);
        Assert.Equal(field, error.Field);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void Day_boundaries_pass(int day)
    {
        var c = Valid();
        c.BillDay = day;
        c.PaymentDueDay = day;
        Assert.True(ContractValidator.Validate(c).IsValid);
    }

    [Fact]
    public void Reading_window_with_only_from_fails()
    {
        var c = Valid();
        c.ReadingDayFrom = 20;
        var error = Single(c);
        Assert.Equal(ErrorCodes.ContractReadingWindowIncomplete, error.Code);
        Assert.Equal("ReadingDayTo", error.Field);
    }

    [Fact]
    public void Reading_window_with_only_to_fails()
    {
        var c = Valid();
        c.ReadingDayTo = 26;
        Assert.Equal("ReadingDayFrom", Single(c).Field);
    }

    [Fact]
    public void Invalid_link_is_reported_with_indexed_field()
    {
        var c = Valid();
        c.Links = [new Link { Url = "https://example.com" }, new Link { Url = "ftp://x" }];
        var error = Single(c);
        Assert.Equal(ErrorCodes.LinkUrlInvalid, error.Code);
        Assert.Equal("Links[1].Url", error.Field);
    }
}
