namespace HomeUtilities.ViewModels;

/// <summary>The localized labels of the bill line dialog.</summary>
public sealed class BillLineEditorTexts
{
    public BillLineEditorTexts(ILocalizer localizer)
    {
        DescriptionLabel = localizer["BillLineDescriptionLabel"];
        QuantityLabel = localizer["BillLineQuantityLabel"];
        UnitLabel = localizer["BillLineUnitLabel"];
        UnitPriceLabel = localizer["BillLineUnitPriceLabel"];
        CategoryLabel = localizer["BillLineCategoryLabel"];
        NoteLabel = localizer["BillLineNoteLabel"];
    }

    public string DescriptionLabel { get; }

    public string QuantityLabel { get; }

    public string UnitLabel { get; }

    public string UnitPriceLabel { get; }

    public string CategoryLabel { get; }

    public string NoteLabel { get; }
}
