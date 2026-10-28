namespace HomeUtilities.ViewModels;

/// <summary>The localized labels of the bill template editor; the editor builds a new instance whenever the language changes.</summary>
public sealed class BillTemplateEditTexts
{
    public BillTemplateEditTexts(ILocalizer localizer)
    {
        GeneralHeader = localizer["BillTemplateGeneral"];
        NameLabel = localizer["BillTemplateNameLabel"];
        PropertyLabel = localizer["BillPropertyLabel"];
        CategoryLabel = localizer["BillCategoryLabel"];
        ContractLabel = localizer["BillContractLabel"];
        SupplierLabel = localizer["BillSupplierLabel"];
        TitleLabel = localizer["BillTitleLabel"];
        IsActiveLabel = localizer["BillTemplateIsActive"];
        AmountsHeader = localizer["BillTemplateAmounts"];
        AmountsHint = localizer["BillTemplateAmountsHint"];
        NetLabel = localizer["BillNetLabel"];
        VatPercentLabel = localizer["BillVatPercentLabel"];
        VatLabel = localizer["BillVatLabel"];
        GrossLabel = localizer["BillGrossLabel"];
        LinesHeader = localizer["BillLines"];
        AddLineText = localizer["BillLineAdd"];
        LinesEmptyText = localizer["BillTemplateLinesEmpty"];
        DeleteLineText = localizer["BillLineDelete"];
        FieldsHeader = localizer["CustomFieldsHeader"];
        FieldsHint = localizer["BillTemplateFieldsHint"];
        FieldsEmptyText = localizer["CustomFieldsEmpty"];
        AddFieldText = localizer["CustomFieldAdd"];
        ScheduleHeader = localizer["BillTemplateSchedule"];
        KindLabel = localizer["BillTemplateKindLabel"];
        EveryMonthsLabel = localizer["BillTemplateEveryMonthsLabel"];
        StartLabel = localizer["BillTemplateStartLabel"];
        HasEndLabel = localizer["BillTemplateHasEndLabel"];
        EndLabel = localizer["BillTemplateEndLabel"];
        DayLabel = localizer["BillTemplateDayLabel"];
        DayHint = localizer["BillTemplateDayHint"];
        TimingLabel = localizer["ContractBillingTimingLabel"];
        TimingHint = localizer["BillTemplateTimingHint"];
        DueOffsetLabel = localizer["BillTemplateDueOffsetLabel"];
        RemindLabel = localizer["BillTemplateRemindLabel"];
        PreviewHeader = localizer["BillTemplatePreviewHeader"];
        NotesLabel = localizer["BillNotesLabel"];
    }

    public string GeneralHeader { get; }

    public string NameLabel { get; }

    public string PropertyLabel { get; }

    public string CategoryLabel { get; }

    public string ContractLabel { get; }

    public string SupplierLabel { get; }

    public string TitleLabel { get; }

    public string IsActiveLabel { get; }

    public string AmountsHeader { get; }

    public string AmountsHint { get; }

    public string NetLabel { get; }

    public string VatPercentLabel { get; }

    public string VatLabel { get; }

    public string GrossLabel { get; }

    public string LinesHeader { get; }

    public string AddLineText { get; }

    public string LinesEmptyText { get; }

    public string DeleteLineText { get; }

    public string FieldsHeader { get; }

    public string FieldsHint { get; }

    public string FieldsEmptyText { get; }

    public string AddFieldText { get; }

    public string ScheduleHeader { get; }

    public string KindLabel { get; }

    public string EveryMonthsLabel { get; }

    public string StartLabel { get; }

    public string HasEndLabel { get; }

    public string EndLabel { get; }

    public string DayLabel { get; }

    public string DayHint { get; }

    public string TimingLabel { get; }

    public string TimingHint { get; }

    public string DueOffsetLabel { get; }

    public string RemindLabel { get; }

    public string PreviewHeader { get; }

    public string NotesLabel { get; }
}
