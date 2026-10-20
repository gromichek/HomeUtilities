using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class CategoryValidatorTests
{
    private static ValidationError Single(Category c) => Assert.Single(CategoryValidator.Validate(c).Errors);

    [Fact]
    public void Valid_user_category_passes()
    {
        Assert.True(CategoryValidator.Validate(new Category { Name = "Kindergarten", Color = "#a1B2c3" }).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void User_category_without_name_fails(string? name)
    {
        var error = Single(new Category { Name = name });
        Assert.Equal((ErrorCodes.CategoryNameRequired, "Name"), (error.Code, error.Field));
    }

    [Fact]
    public void Built_in_category_without_name_passes()
    {
        Assert.True(CategoryValidator.Validate(new Category { SystemKey = ServiceKind.Gas }).IsValid);
    }

    [Fact]
    public void Built_in_category_with_blank_name_fails()
    {
        Assert.Equal(ErrorCodes.CategoryNameRequired, Single(new Category { SystemKey = ServiceKind.Gas, Name = " " }).Code);
    }

    [Fact]
    public void Name_101_chars_fails_and_100_passes()
    {
        Assert.True(CategoryValidator.Validate(new Category { Name = new string('a', 100) }).IsValid);
        Assert.Equal(ErrorCodes.CategoryNameTooLong, Single(new Category { Name = new string('a', 101) }).Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("#12345")]
    [InlineData("#1234567")]
    [InlineData("#GGGGGG")]
    public void Bad_colour_fails(string color)
    {
        var error = Single(new Category { Name = "X", Color = color });
        Assert.Equal((ErrorCodes.CategoryColorInvalid, "Color"), (error.Code, error.Field));
    }

    [Fact]
    public void Notes_limit_is_2000()
    {
        Assert.True(CategoryValidator.Validate(new Category { Name = "X", Notes = new string('n', 2000) }).IsValid);
        Assert.Equal(ErrorCodes.NotesTooLong, Single(new Category { Name = "X", Notes = new string('n', 2001) }).Code);
    }
}
