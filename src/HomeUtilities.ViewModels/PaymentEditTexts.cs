namespace HomeUtilities.ViewModels;

/// <summary>The localized labels of the payment editor; the editor builds a new instance whenever the language changes.</summary>
public sealed class PaymentEditTexts
{
    public PaymentEditTexts(ILocalizer localizer)
    {
        GeneralHeader = localizer["PaymentGeneral"];
        PropertyLabel = localizer["PaymentPropertyLabel"];
        ContractLabel = localizer["PaymentContractLabel"];
        PaidOnLabel = localizer["PaymentPaidOnLabel"];
        AmountLabel = localizer["PaymentAmountLabel"];
        AmountHint = localizer["PaymentAmountHint"];
        MethodLabel = localizer["PaymentMethodLabel"];
        ReferenceLabel = localizer["PaymentReferenceLabel"];
        FillReferenceText = localizer["PaymentFillReference"];
        NotesLabel = localizer["PaymentNotesLabel"];
        AllocationsHeader = localizer["PaymentAllocations"];
        AddAllocationText = localizer["PaymentAllocationAdd"];
        AutoAllocateText = localizer["PaymentAutoAllocate"];
        AllocationsEmptyText = localizer["PaymentAllocationsEmpty"];
        AllocationsHint = localizer["PaymentAllocationsHint"];
        QuickNote = localizer["PaymentQuickNote"];
        DeletePaymentText = localizer["PaymentDelete"];
    }

    public string GeneralHeader { get; }

    public string PropertyLabel { get; }

    public string ContractLabel { get; }

    public string PaidOnLabel { get; }

    public string AmountLabel { get; }

    public string AmountHint { get; }

    public string MethodLabel { get; }

    public string ReferenceLabel { get; }

    public string FillReferenceText { get; }

    public string NotesLabel { get; }

    public string AllocationsHeader { get; }

    public string AddAllocationText { get; }

    public string AutoAllocateText { get; }

    public string AllocationsEmptyText { get; }

    public string AllocationsHint { get; }

    public string QuickNote { get; }

    public string DeletePaymentText { get; }
}
