namespace HomeUtilities.ViewModels;

/// <summary>The localized labels of the Payments list screen.</summary>
public sealed class PaymentsListTexts
{
    public PaymentsListTexts(ILocalizer localizer)
    {
        PropertyFilterLabel = localizer["PaymentsPropertyFilter"];
        ContractFilterLabel = localizer["PaymentsContractFilter"];
    }

    public string PropertyFilterLabel { get; }

    public string ContractFilterLabel { get; }
}
