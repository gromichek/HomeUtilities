using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class LinkValidatorTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://example.com/portal?x=1")]
    public void Http_and_https_urls_are_valid(string url)
    {
        var link = new Link { Url = url };
        Assert.True(LinkValidator.Validate(link).IsValid);
        Assert.NotNull(link.TryGetUri());
    }

    [Theory]
    [InlineData("ftp://x")]
    [InlineData("example.com")]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    public void Other_urls_are_invalid(string url)
    {
        var link = new Link { Url = url };
        var error = Assert.Single(LinkValidator.Validate(link).Errors);
        Assert.Equal(ErrorCodes.LinkUrlInvalid, error.Code);
        Assert.Equal("Url", error.Field);
        Assert.Null(link.TryGetUri());
    }

    [Fact]
    public void Title_longer_than_100_fails()
    {
        var link = new Link { Url = "https://example.com", Title = new string('t', 101) };
        Assert.Equal(ErrorCodes.LinkTitleTooLong, Assert.Single(LinkValidator.Validate(link).Errors).Code);

        link.Title = new string('t', 100);
        Assert.True(LinkValidator.Validate(link).IsValid);
    }
}
