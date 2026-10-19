using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class SupplierValidatorTests
{
    [Fact]
    public void Valid_supplier_passes()
    {
        var s = new Supplier { Name = "Test Water Ltd", Links = [new Link { Url = "https://example.com" }] };
        Assert.True(SupplierValidator.Validate(s).IsValid);
    }

    [Fact]
    public void Blank_name_fails()
    {
        var error = Assert.Single(SupplierValidator.Validate(new Supplier { Name = " " }).Errors);
        Assert.Equal(ErrorCodes.SupplierNameRequired, error.Code);
    }

    [Fact]
    public void Name_101_chars_fails()
    {
        var error = Assert.Single(SupplierValidator.Validate(new Supplier { Name = new string('a', 101) }).Errors);
        Assert.Equal(ErrorCodes.SupplierNameTooLong, error.Code);
    }

    [Fact]
    public void Invalid_link_is_reported_with_indexed_field_and_deleted_link_is_skipped()
    {
        var s = new Supplier
        {
            Name = "Test Water Ltd",
            Links =
            [
                new Link { Url = "https://example.com" },
                new Link { Url = "nope" },
                new Link { Url = "bad", IsDeleted = true },
            ],
        };

        var error = Assert.Single(SupplierValidator.Validate(s).Errors);
        Assert.Equal(ErrorCodes.LinkUrlInvalid, error.Code);
        Assert.Equal("Links[1].Url", error.Field);
    }
}
