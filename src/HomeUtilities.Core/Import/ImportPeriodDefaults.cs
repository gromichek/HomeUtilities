using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Import;

/// <summary>What the wizard suggests for the period of a sheet row, from the kind of service of the contract.</summary>
/// <param name="Rule">Suggested period rule.</param>
/// <param name="DateIsPaymentDate">Suggested value of "the date is the payment date".</param>
public sealed record ImportPeriodSuggestion(ImportPeriodRule Rule, bool DateIsPaymentDate);

/// <summary>
/// Water sheets carry the day the readings were taken (the contract's reading window decides the month); heating and electricity
/// sheets are dated the 1st of the month after the service; the other sheets (fixed amounts) are dated the day the previous month was paid.
/// The owner can change every suggestion.
/// </summary>
public static class ImportPeriodDefaults
{
    /// <summary>Suggests the period rule for a contract.</summary>
    public static ImportPeriodSuggestion For(Contract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var hasWindow = contract.ReadingDayFrom is not null && contract.ReadingDayTo is not null;
        return contract.Service switch
        {
            ServiceKind.ColdWater or ServiceKind.HotWater or ServiceKind.Sewerage =>
                new(hasWindow ? ImportPeriodRule.ReadingWindow : ImportPeriodRule.MonthOfDate, false),
            ServiceKind.Heating or ServiceKind.WaterHeating or ServiceKind.Electricity or ServiceKind.Gas =>
                new(ImportPeriodRule.PreviousMonth, false),
            _ => new(ImportPeriodRule.PreviousMonth, true),
        };
    }
}
