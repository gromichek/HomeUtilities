using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;

namespace HomeUtilities.Core.Tests.Payments;

public class PaymentDetailsValidatorTests
{
    private static ValidationError Single(PaymentDetails d) => Assert.Single(PaymentDetailsValidator.Validate(d).Errors);

    [Fact]
    public void Empty_and_complete_details_pass()
    {
        Assert.True(PaymentDetailsValidator.Validate(PaymentDetails.Empty).IsValid);
        Assert.True(PaymentDetailsValidator.Validate(
            new("Water Co", "40000000000", "LV80 BANK 0000 4351 9500 1", "BANKLV22XXX", "Bank A", "Nr. {CustomerNumber}")).IsValid);
    }

    [Fact]
    public void Invalid_iban_fails()
    {
        var error = Single(PaymentDetails.Empty with { Iban = "LV81BANK0000435195001" });
        Assert.Equal((ErrorCodes.PaymentIbanInvalid, "Iban"), (error.Code, error.Field));
    }

    [Theory]
    [InlineData("BANKLV2")]
    [InlineData("bankLV22")]
    [InlineData("BANKLV22XX")]
    [InlineData("BANK1V22")]
    [InlineData("")]
    public void Bad_bic_fails(string bic)
    {
        var error = Single(PaymentDetails.Empty with { Bic = bic });
        Assert.Equal((ErrorCodes.PaymentBicInvalid, "Bic"), (error.Code, error.Field));
    }

    [Theory]
    [InlineData("BANKLV22")]
    [InlineData("BANKLV22XXX")]
    public void Good_bic_passes(string bic) => Assert.True(PaymentDetailsValidator.Validate(PaymentDetails.Empty with { Bic = bic }).IsValid);

    [Fact]
    public void Purpose_141_chars_fails_and_140_passes()
    {
        Assert.True(PaymentDetailsValidator.Validate(PaymentDetails.Empty with { PurposeTemplate = new string('p', 140) }).IsValid);
        var error = Single(PaymentDetails.Empty with { PurposeTemplate = new string('p', 141) });
        Assert.Equal((ErrorCodes.PaymentFieldTooLong, "PurposeTemplate"), (error.Code, error.Field));
    }

    [Fact]
    public void Supplier_and_contract_validators_prefix_the_field()
    {
        var bad = PaymentDetails.Empty with { Iban = "XX00" };

        var supplier = Assert.Single(SupplierValidator.Validate(new Supplier { Name = "S", PaymentDetails = bad }).Errors);
        Assert.Equal((ErrorCodes.PaymentIbanInvalid, "PaymentDetails.Iban"), (supplier.Code, supplier.Field));

        var contract = new Contract
        {
            PropertyId = Guid.CreateVersion7(),
            SupplierId = Guid.CreateVersion7(),
            CategoryId = Guid.CreateVersion7(),
            Title = "C",
            PaymentDetails = bad,
        };
        var error = Assert.Single(ContractValidator.Validate(contract).Errors);
        Assert.Equal((ErrorCodes.PaymentIbanInvalid, "PaymentDetails.Iban"), (error.Code, error.Field));
    }
}
