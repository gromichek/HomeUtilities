namespace HomeUtilities.ViewModels;

/// <summary>The localized labels of the Bills list screen.</summary>
public sealed class BillsListTexts
{
    public BillsListTexts(ILocalizer localizer)
    {
        PropertyFilterLabel = localizer["BillsPropertyFilter"];
        StatusFilterLabel = localizer["BillsStatusFilter"];
        CategoryFilterLabel = localizer["BillsCategoryFilter"];
        BilledLabel = localizer["BillsBilled"];
        PaidLabel = localizer["BillsPaid"];
        OpenLabel = localizer["BillsOpen"];
    }

    public string PropertyFilterLabel { get; }

    public string StatusFilterLabel { get; }

    public string CategoryFilterLabel { get; }

    public string BilledLabel { get; }

    public string PaidLabel { get; }

    public string OpenLabel { get; }
}
