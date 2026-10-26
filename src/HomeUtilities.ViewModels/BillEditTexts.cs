namespace HomeUtilities.ViewModels;

/// <summary>The localized labels of the bill editor; the editor builds a new instance whenever the language changes.</summary>
public sealed class BillEditTexts
{
    public BillEditTexts(ILocalizer localizer)
    {
        GeneralHeader = localizer["BillGeneral"];
        TypeLabel = localizer["BillTypeLabel"];
        PropertyLabel = localizer["BillPropertyLabel"];
        ContractLabel = localizer["BillContractLabel"];
        PeriodLabel = localizer["BillPeriodLabel"];
        PeriodHint = localizer["BillPeriodHint"];
        PreviousPeriodText = localizer["BillPreviousPeriod"];
        NextPeriodText = localizer["BillNextPeriod"];
        KindLabel = localizer["BillKindLabel"];
        TitleLabel = localizer["BillTitleLabel"];
        NumberLabel = localizer["BillNumberLabel"];
        HasIssuedOnLabel = localizer["BillHasIssuedOnLabel"];
        IssuedOnLabel = localizer["BillIssuedOnLabel"];
        HasDueOnLabel = localizer["BillHasDueOnLabel"];
        DueOnLabel = localizer["BillDueOnLabel"];
        HasServicePeriodLabel = localizer["BillHasServicePeriodLabel"];
        ServiceFromLabel = localizer["BillServiceFromLabel"];
        ServiceToLabel = localizer["BillServiceToLabel"];
        CategoryLabel = localizer["BillCategoryLabel"];
        SupplierLabel = localizer["BillSupplierLabel"];
        AmountsHeader = localizer["BillAmounts"];
        AmountsHint = localizer["BillAmountsHint"];
        NetLabel = localizer["BillNetLabel"];
        VatPercentLabel = localizer["BillVatPercentLabel"];
        VatLabel = localizer["BillVatLabel"];
        GrossLabel = localizer["BillGrossLabel"];
        QuantitiesHeader = localizer["BillQuantities"];
        QuantitiesHint = localizer["BillQuantitiesHint"];
        ComparisonHeader = localizer["BillComparison"];
        CalculatedLabel = localizer["BillCalculatedLabel"];
        PlanLabel = localizer["BillPlanLabel"];
        DifferenceLabel = localizer["BillDifferenceLabel"];
        ConsumptionLabel = localizer["BillConsumptionLabel"];
        MeteredLabel = localizer["BillMeteredLabel"];
        LossesLabel = localizer["BillLossesLabel"];
        ShowChargesText = localizer["BillShowCharges"];
        UpdateSnapshotText = localizer["BillUpdateSnapshot"];
        NoCalculationText = localizer["BillNoCalculation"];
        LinesHeader = localizer["BillLines"];
        AddLineText = localizer["BillLineAdd"];
        LinesEmptyText = localizer["BillLinesEmpty"];
        ShowLinesText = localizer["BillLinesShow"];
        HideLinesText = localizer["BillLinesHide"];
        DeleteLineText = localizer["BillLineDelete"];
        FieldsHeader = localizer["BillFields"];
        FieldsEmptyText = localizer["BillFieldsEmpty"];
        FieldDateSetText = localizer["BillFieldDateSet"];
        AttachmentsHeader = localizer["BillAttachments"];
        AddAttachmentText = localizer["BillAttachmentAdd"];
        AttachmentsEmptyText = localizer["BillAttachmentsEmpty"];
        OpenAttachmentText = localizer["BillAttachmentOpen"];
        RemoveAttachmentText = localizer["BillAttachmentRemove"];
        PaymentHeader = localizer["BillPayment"];
        PaidAmountLabel = localizer["BillPaidAmountLabel"];
        OpenAmountLabel = localizer["BillOpenAmountLabel"];
        PaidText = localizer["BillPaid"];
        PaymentsEmptyText = localizer["BillPaymentsEmpty"];
        RemovePaymentText = localizer["BillRemovePayment"];
        PaymentsHint = localizer["BillPaymentsHint"];
        AddPaymentText = localizer["BillAddPayment"];
        NotesLabel = localizer["BillNotesLabel"];
        DeleteBillText = localizer["BillDelete"];
    }

    public string GeneralHeader { get; }

    public string TypeLabel { get; }

    public string PropertyLabel { get; }

    public string ContractLabel { get; }

    public string PeriodLabel { get; }

    public string PeriodHint { get; }

    public string PreviousPeriodText { get; }

    public string NextPeriodText { get; }

    public string KindLabel { get; }

    public string TitleLabel { get; }

    public string NumberLabel { get; }

    public string HasIssuedOnLabel { get; }

    public string IssuedOnLabel { get; }

    public string HasDueOnLabel { get; }

    public string DueOnLabel { get; }

    public string HasServicePeriodLabel { get; }

    public string ServiceFromLabel { get; }

    public string ServiceToLabel { get; }

    public string CategoryLabel { get; }

    public string SupplierLabel { get; }

    public string AmountsHeader { get; }

    public string AmountsHint { get; }

    public string NetLabel { get; }

    public string VatPercentLabel { get; }

    public string VatLabel { get; }

    public string GrossLabel { get; }

    public string QuantitiesHeader { get; }

    public string QuantitiesHint { get; }

    public string ComparisonHeader { get; }

    public string CalculatedLabel { get; }

    public string PlanLabel { get; }

    public string DifferenceLabel { get; }

    public string ConsumptionLabel { get; }

    public string MeteredLabel { get; }

    public string LossesLabel { get; }

    public string ShowChargesText { get; }

    public string UpdateSnapshotText { get; }

    public string NoCalculationText { get; }

    public string LinesHeader { get; }

    public string AddLineText { get; }

    public string LinesEmptyText { get; }

    public string ShowLinesText { get; }

    public string HideLinesText { get; }

    public string DeleteLineText { get; }

    public string FieldsHeader { get; }

    public string FieldsEmptyText { get; }

    public string FieldDateSetText { get; }

    public string AttachmentsHeader { get; }

    public string AddAttachmentText { get; }

    public string AttachmentsEmptyText { get; }

    public string OpenAttachmentText { get; }

    public string RemoveAttachmentText { get; }

    public string PaymentHeader { get; }

    public string PaidAmountLabel { get; }

    public string OpenAmountLabel { get; }

    public string PaidText { get; }

    public string PaymentsEmptyText { get; }

    public string RemovePaymentText { get; }

    public string PaymentsHint { get; }

    public string AddPaymentText { get; }

    public string NotesLabel { get; }

    public string DeleteBillText { get; }
}
