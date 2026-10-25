namespace HomeUtilities.ViewModels;

/// <summary>The localized labels of the Payments list screen.</summary>
public sealed class PaymentsListTexts
{
    public PaymentsListTexts(ILocalizer localizer)
    {
        PropertyFilterLabel = localizer["PaymentsPropertyFilter"];
        YearLabel = localizer["PaymentsYearLabel"];
        PreviousYearText = localizer["PaymentsPreviousYear"];
        NextYearText = localizer["PaymentsNextYear"];
        AllYearsText = localizer["PaymentsAllYears"];
        ContractFilterLabel = localizer["PaymentsContractFilter"];
    }

    public string PropertyFilterLabel { get; }

    public string YearLabel { get; }

    public string PreviousYearText { get; }

    public string NextYearText { get; }

    public string AllYearsText { get; }

    public string ContractFilterLabel { get; }
}
