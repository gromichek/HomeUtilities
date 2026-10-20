using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;

namespace HomeUtilities.Core.Tests.Billing;

public class CustomFieldTests
{
    private static CustomFieldDefinition Def(string name, CustomFieldKind kind = CustomFieldKind.Text,
        CustomFieldTarget target = CustomFieldTarget.Bill, Guid? owner = null) =>
        new() { OwnerId = owner ?? Guid.Empty, Name = name, Kind = kind, Target = target };

    [Fact]
    public void Decimal_round_trip_is_invariant_under_ru_culture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
        try
        {
            var value = CustomFieldValue.From(Def("Losses", CustomFieldKind.Number), 12.5m);
            Assert.Equal("12.5", value.Value);
            Assert.Equal(12.5m, value.AsDecimal());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Date_is_stored_as_iso_and_read_back()
    {
        var value = CustomFieldValue.From(Def("Read on", CustomFieldKind.Date), new DateOnly(2026, 10, 20));
        Assert.Equal("2026-10-20", value.Value);
        Assert.Equal(new DateOnly(2026, 10, 20), value.AsDate());
    }

    [Fact]
    public void From_copies_definition_identity()
    {
        var definition = Def("Reference");
        var value = CustomFieldValue.From(definition, "ABC-1");
        Assert.Equal((definition.Id, "Reference", CustomFieldKind.Text, "ABC-1"), (value.DefinitionId, value.Name, value.Kind, value.Value));
        Assert.Null(value.AsDecimal());
        Assert.Null(value.AsDate());
    }

    [Fact]
    public void Wrong_value_type_throws() =>
        Assert.Throws<ArgumentException>(() => CustomFieldValue.From(Def("N", CustomFieldKind.Number), "text"));

    [Theory]
    [InlineData(CustomFieldKind.Number, "12,5")]
    [InlineData(CustomFieldKind.Money, "abc")]
    [InlineData(CustomFieldKind.Date, "20.10.2026")]
    public void Invalid_value_is_rejected(CustomFieldKind kind, string text)
    {
        var result = CustomFieldValidator.Validate(new CustomFieldValue(null, "F", kind, text));
        Assert.Equal(ErrorCodes.CustomFieldInvalidValue, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Valid_values_pass_and_text_limit_is_500()
    {
        Assert.True(CustomFieldValidator.Validate(new CustomFieldValue(null, "F", CustomFieldKind.Money, "-3.40")).IsValid);
        Assert.True(CustomFieldValidator.Validate(new CustomFieldValue(null, "F", CustomFieldKind.Text, new string('x', 500))).IsValid);
        var result = CustomFieldValidator.Validate(new CustomFieldValue(null, "F", CustomFieldKind.Text, new string('x', 501)));
        Assert.Equal(ErrorCodes.CustomFieldValueTooLong, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public void Definition_name_is_required_and_limited()
    {
        Assert.Equal(ErrorCodes.CustomFieldNameRequired, Assert.Single(CustomFieldValidator.Validate(Def(" ")).Errors).Code);
        Assert.True(CustomFieldValidator.Validate(Def(new string('n', 60))).IsValid);
        Assert.Equal(ErrorCodes.CustomFieldNameTooLong, Assert.Single(CustomFieldValidator.Validate(Def(new string('n', 61))).Errors).Code);
    }

    [Fact]
    public void Duplicate_name_is_rejected_case_insensitively_per_owner_and_target()
    {
        var owner = Guid.CreateVersion7();
        var result = CustomFieldValidator.ValidateAll([Def("Losses", owner: owner), Def("LOSSES", owner: owner)]);
        var error = Assert.Single(result.Errors);
        Assert.Equal((ErrorCodes.CustomFieldNameDuplicate, "CustomFields[1].Name"), (error.Code, error.Field));
    }

    [Fact]
    public void Same_name_on_other_target_owner_or_when_deleted_is_allowed()
    {
        var owner = Guid.CreateVersion7();
        var deleted = Def("Losses", owner: owner);
        deleted.IsDeleted = true;
        var result = CustomFieldValidator.ValidateAll([
            Def("Losses", owner: owner),
            Def("Losses", target: CustomFieldTarget.BillLine, owner: owner),
            Def("Losses", owner: Guid.CreateVersion7()),
            deleted,
        ]);
        Assert.True(result.IsValid);
    }
}
