using System.Globalization;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.ViewModels.Tests;

public class BillLineEditorViewModelTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
    private readonly ContractTestKit _kit = new(new DateOnly(2026, 10, 27));

    public BillLineEditorViewModelTests() => CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

    public void Dispose() => CultureInfo.CurrentCulture = _culture;

    private BillLineEditorViewModel Create(BillLine? existing = null, params CustomFieldDefinition[] definitions) =>
        new(_kit.Localizer, existing, _kit.Categories.Stored, definitions, new DateOnly(2026, 10, 27));

    [Fact]
    public void ValidLine_IsReturnedWithItsAmounts()
    {
        var dialog = Create();
        dialog.Description = "Water 2 m3";
        dialog.QuantityText = "2";
        dialog.Unit = "m3";
        dialog.UnitPriceText = "1,41";
        dialog.Amounts.VatPercentText = "21";
        dialog.Amounts.NetText = "2,82";

        dialog.SaveCommand.Execute(null);

        // 2 m3 x 1.41 = 2.82; VAT 21 % = 0.59; total 3.41
        Assert.True(dialog.Saved);
        var line = dialog.Result!;
        Assert.Equal(("Water 2 m3", 2m, "m3", 1.41m), (line.Description, line.Quantity, line.Unit, line.UnitPrice));
        Assert.Equal((2.82m, 21m, 0.59m, 3.41m), (line.Net, line.VatPercent, line.Vat, line.Gross));
        Assert.Null(line.CategoryId);
    }

    [Fact]
    public void Description_IsRequired()
    {
        var dialog = Create();
        dialog.Amounts.GrossText = "5";

        dialog.SaveCommand.Execute(null);

        Assert.False(dialog.Saved);
        Assert.Equal("Error_bill_line_description_required [en]", dialog.DescriptionError);
    }

    [Fact]
    public void Total_IsRequired_AndNonsenseIsReported()
    {
        var dialog = Create();
        dialog.Description = "x";
        dialog.QuantityText = "many";

        dialog.SaveCommand.Execute(null);

        Assert.False(dialog.Saved);
        Assert.Equal("Error_common_number_invalid [en]", dialog.QuantityError);
        Assert.Equal("Error_amount_gross_required [en]", dialog.GrossError);
    }

    [Fact]
    public void OwnCategory_IsKept_AndTheDefaultMeansTheBillsCategory()
    {
        var category = _kit.Categories.Stored[3];
        var dialog = Create();
        dialog.Description = "x";
        dialog.Amounts.GrossText = "5";
        Assert.Equal(Guid.Empty, dialog.SelectedCategory!.Id);

        dialog.SelectedCategoryIndex = dialog.CategoryChoices.ToList().FindIndex(c => c.Id == category.Id);
        dialog.SaveCommand.Execute(null);

        Assert.Equal(category.Id, dialog.Result!.CategoryId);
    }

    [Fact]
    public void Editing_KeepsTheIdAndShowsTheValues()
    {
        var existing = new BillLine { Description = "Old", Quantity = 3m, Gross = 12.10m, Net = 10m, VatPercent = 21m, Vat = 2.10m, SortOrder = 4 };
        var dialog = Create(existing);

        Assert.Equal("Old", dialog.Description);
        Assert.Equal("12.10", dialog.Amounts.GrossText);

        dialog.Description = "New";
        dialog.SaveCommand.Execute(null);

        Assert.Equal((existing.Id, "New", 4), (dialog.Result!.Id, dialog.Result.Description, dialog.Result.SortOrder));
    }

    [Fact]
    public void CustomFieldsOfTheLine_AreSaved()
    {
        var definition = new CustomFieldDefinition { Name = "Meter reading", Kind = CustomFieldKind.Number, Target = CustomFieldTarget.BillLine };
        var dialog = Create(null, definition);
        dialog.Description = "x";
        dialog.Amounts.GrossText = "5";
        dialog.FieldRows[0].Text = "1234,5";

        dialog.SaveCommand.Execute(null);

        var value = Assert.Single(dialog.Result!.CustomFields);
        Assert.Equal(("Meter reading", "1234.5", definition.Id), (value.Name, value.Value, value.DefinitionId));
    }
}
