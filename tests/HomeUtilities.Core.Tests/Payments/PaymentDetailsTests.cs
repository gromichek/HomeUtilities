using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;

namespace HomeUtilities.Core.Tests.Payments;

public class PaymentDetailsTests
{
    [Fact]
    public void Contract_iban_overrides_and_supplier_recipient_is_kept()
    {
        var supplier = new Supplier { PaymentDetails = new("Water Co", "40000000000", "LV80BANK0000435195001", "BANKLV22", "Bank A", "Nr. {CustomerNumber}") };
        var contract = new Contract { PaymentDetails = PaymentDetails.Empty with { Iban = "GB82WEST12345698765432" } };

        var effective = PaymentDetails.Effective(contract, supplier);

        Assert.Equal(new PaymentDetails("Water Co", "40000000000", "GB82WEST12345698765432", "BANKLV22", "Bank A", "Nr. {CustomerNumber}"), effective);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Blank_contract_value_does_not_override(string? blank)
    {
        var supplier = new Supplier { PaymentDetails = PaymentDetails.Empty with { Iban = "LV80BANK0000435195001" } };
        var contract = new Contract { PaymentDetails = PaymentDetails.Empty with { Iban = blank } };

        Assert.Equal("LV80BANK0000435195001", PaymentDetails.Effective(contract, supplier).Iban);
    }

    [Fact]
    public void Missing_details_on_either_side_are_empty()
    {
        Assert.Equal(PaymentDetails.Empty, PaymentDetails.Effective(new Contract(), new Supplier()));
        var supplier = new Supplier { PaymentDetails = PaymentDetails.Empty with { BankName = "Bank A" } };
        Assert.Equal("Bank A", PaymentDetails.Effective(new Contract(), supplier).BankName);
        var contract = new Contract { PaymentDetails = PaymentDetails.Empty with { BankName = "Bank B" } };
        Assert.Equal("Bank B", PaymentDetails.Effective(contract, new Supplier()).BankName);
    }
}
