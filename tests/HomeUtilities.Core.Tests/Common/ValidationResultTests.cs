using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Common;

public class ValidationResultTests
{
    [Fact]
    public void Ok_IsValid()
    {
        Assert.True(ValidationResult.Ok.IsValid);
        Assert.Empty(ValidationResult.Ok.Errors);
    }

    [Fact]
    public void Fail_IsInvalid()
    {
        var result = ValidationResult.Fail(new ValidationError("a.b", "Field"));

        Assert.False(result.IsValid);
        Assert.Equal("a.b", Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Merge_ConcatenatesErrors()
    {
        var first = ValidationResult.Fail(new ValidationError("one"));
        var second = ValidationResult.Fail(new ValidationError("two"), new ValidationError("three"));

        var merged = first.Merge(second);

        Assert.Equal(["one", "two", "three"], merged.Errors.Select(e => e.Code));
        Assert.True(ValidationResult.Ok.Merge(ValidationResult.Ok).IsValid);
        Assert.Single(first.Errors);
    }
}
