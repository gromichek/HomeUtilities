using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Tests.Domain;

public class PropertyValidatorTests
{
    private static PropertyParameter Param(PropertyParameterKind kind, string from, decimal value, string? name = null) =>
        new() { Kind = kind, ValidFrom = DateOnly.Parse(from), Value = value, Name = name };

    private static Property Valid() => new() { Name = "Test apartment" };

    [Fact]
    public void Valid_property_passes()
    {
        var p = Valid();
        p.Parameters.Add(Param(PropertyParameterKind.TotalArea, "2020-01-01", 50));
        Assert.True(PropertyValidator.Validate(p).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_fails(string name)
    {
        var p = Valid();
        p.Name = name;
        var error = Assert.Single(PropertyValidator.Validate(p).Errors);
        Assert.Equal(ErrorCodes.PropertyNameRequired, error.Code);
        Assert.Equal("Name", error.Field);
    }

    [Fact]
    public void Name_of_100_chars_passes_and_101_fails()
    {
        var p = Valid();
        p.Name = new string('a', 100);
        Assert.True(PropertyValidator.Validate(p).IsValid);

        p.Name = new string('a', 101);
        Assert.Equal(ErrorCodes.PropertyNameTooLong, Assert.Single(PropertyValidator.Validate(p).Errors).Code);
    }

    [Fact]
    public void Negative_area_fails_with_indexed_field()
    {
        var p = Valid();
        p.Parameters.Add(Param(PropertyParameterKind.TotalArea, "2020-01-01", 50));
        p.Parameters.Add(Param(PropertyParameterKind.HeatedArea, "2020-01-01", 40));
        p.Parameters.Add(Param(PropertyParameterKind.LandArea, "2020-01-01", -1));

        var error = Assert.Single(PropertyValidator.Validate(p).Errors);
        Assert.Equal(ErrorCodes.PropertyParameterValueNegative, error.Code);
        Assert.Equal("Parameters[2].Value", error.Field);
    }

    [Fact]
    public void Fractional_residents_fail_but_whole_pass()
    {
        var p = Valid();
        p.Parameters.Add(Param(PropertyParameterKind.DeclaredResidents, "2020-01-01", 2.5m));
        var error = Assert.Single(PropertyValidator.Validate(p).Errors);
        Assert.Equal(ErrorCodes.PropertyParameterResidentsNotWhole, error.Code);
        Assert.Equal("Parameters[0].Value", error.Field);

        p.Parameters[0].Value = 2;
        Assert.True(PropertyValidator.Validate(p).IsValid);
    }

    [Fact]
    public void Fractional_area_is_allowed()
    {
        var p = Valid();
        p.Parameters.Add(Param(PropertyParameterKind.TotalArea, "2020-01-01", 54.5m));
        Assert.True(PropertyValidator.Validate(p).IsValid);
    }

    [Fact]
    public void Custom_without_name_fails_and_with_name_passes()
    {
        var p = Valid();
        p.Parameters.Add(Param(PropertyParameterKind.Custom, "2020-01-01", 1));
        var error = Assert.Single(PropertyValidator.Validate(p).Errors);
        Assert.Equal(ErrorCodes.PropertyParameterNameRequired, error.Code);
        Assert.Equal("Parameters[0].Name", error.Field);

        p.Parameters[0].Name = "Balcony";
        Assert.True(PropertyValidator.Validate(p).IsValid);
    }

    [Fact]
    public void Duplicate_valid_from_for_same_kind_fails()
    {
        var p = Valid();
        p.Parameters.Add(Param(PropertyParameterKind.TotalArea, "2020-01-01", 50));
        p.Parameters.Add(Param(PropertyParameterKind.TotalArea, "2020-01-01", 51));

        var error = Assert.Single(PropertyValidator.Validate(p).Errors);
        Assert.Equal(ErrorCodes.DuplicateValidFrom, error.Code);
        Assert.Equal("Parameters.ValidFrom", error.Field);
    }

    [Fact]
    public void Same_valid_from_for_different_kinds_passes()
    {
        var p = Valid();
        p.Parameters.Add(Param(PropertyParameterKind.TotalArea, "2020-01-01", 50));
        p.Parameters.Add(Param(PropertyParameterKind.HeatedArea, "2020-01-01", 45));
        Assert.True(PropertyValidator.Validate(p).IsValid);
    }

    [Fact]
    public void Same_valid_from_for_different_custom_names_passes_but_same_name_fails()
    {
        var p = Valid();
        p.Parameters.Add(Param(PropertyParameterKind.Custom, "2020-01-01", 1, "Balcony"));
        p.Parameters.Add(Param(PropertyParameterKind.Custom, "2020-01-01", 2, "Cellar"));
        Assert.True(PropertyValidator.Validate(p).IsValid);

        p.Parameters[1].Name = "Balcony";
        Assert.Equal(ErrorCodes.DuplicateValidFrom, Assert.Single(PropertyValidator.Validate(p).Errors).Code);
    }

    [Fact]
    public void Deleted_parameters_are_not_validated()
    {
        var p = Valid();
        p.Parameters.Add(Param(PropertyParameterKind.TotalArea, "2020-01-01", -5));
        p.Parameters[0].IsDeleted = true;
        Assert.True(PropertyValidator.Validate(p).IsValid);
    }
}
