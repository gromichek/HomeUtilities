namespace HomeUtilities.ViewModels;

/// <summary>The localized labels of the Bills list screen.</summary>
public sealed class BillsListTexts
{
    public BillsListTexts(ILocalizer localizer)
    {
        PropertyFilterLabel = localizer["BillsPropertyFilter"];
        YearLabel = localizer["BillsYearLabel"];
        PreviousYearText = localizer["BillsPreviousYear"];
        NextYearText = localizer["BillsNextYear"];
        AllYearsText = localizer["BillsAllYears"];
        StatusFilterLabel = localizer["BillsStatusFilter"];
        BilledLabel = localizer["BillsBilled"];
        PaidLabel = localizer["BillsPaid"];
        OpenLabel = localizer["BillsOpen"];
    }

    public string PropertyFilterLabel { get; }

    public string YearLabel { get; }

    public string PreviousYearText { get; }

    public string NextYearText { get; }

    public string AllYearsText { get; }

    public string StatusFilterLabel { get; }

    public string BilledLabel { get; }

    public string PaidLabel { get; }

    public string OpenLabel { get; }
}
