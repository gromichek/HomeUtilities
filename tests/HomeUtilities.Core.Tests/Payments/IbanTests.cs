using HomeUtilities.Core.Payments;

namespace HomeUtilities.Core.Tests.Payments;

public class IbanTests
{
    [Theory]
    [InlineData("LV80BANK0000435195001")]
    [InlineData("GB82 WEST 1234 5698 7654 32")]
    [InlineData("gb82west12345698765432")]
    public void Valid_ibans_pass(string iban) => Assert.True(Iban.IsValid(iban));

    [Fact]
    public void Normalize_removes_spaces_and_upper_cases()
    {
        Assert.Equal("GB82WEST12345698765432", Iban.Normalize("gb82 west 1234 5698 7654 32"));
    }

    [Theory]
    [InlineData("LV81BANK0000435195001")] // changed check digits
    [InlineData("LV80BANK0000435195002")] // changed account digit
    [InlineData("LV80BANK00004")] // too short
    [InlineData("1280BANK0000435195001")] // no country letters
    [InlineData("LV80BANK0000435195001!")]
    [InlineData("")]
    public void Invalid_ibans_fail(string iban) => Assert.False(Iban.IsValid(iban));

    [Fact]
    public void Too_long_iban_fails()
    {
        Assert.False(Iban.IsValid("GB82" + new string('0', 31)));
    }

    [Theory]
    [InlineData("LV80BANK0000435195001", "LV80 BANK 0000 4351 9500 1")]
    [InlineData("gb82 west 1234 5698 7654 32", "GB82 WEST 1234 5698 7654 32")]
    public void Format_groups_by_four(string input, string expected) => Assert.Equal(expected, Iban.Format(input));
}
