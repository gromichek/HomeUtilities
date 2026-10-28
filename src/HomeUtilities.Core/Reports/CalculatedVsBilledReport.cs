using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Reports;

/// <summary>
/// Per contract and month: what the app calculated (the snapshot stored with the bill, else the calculation as it is now), what
/// the supplier billed, the difference and the difference of quantities (water losses). Months without a usable calculation are marked.
/// </summary>
internal static class CalculatedVsBilledReport
{
    public static ReportModel Create(ReportData data, ReportFilter filter, IReadOnlyList<Property> properties)
    {
        var measure = filter.Measure is ReportMeasure.Difference or ReportMeasure.DifferencePercent or ReportMeasure.Calculated
            or ReportMeasure.Billed or ReportMeasure.QuantityDifference
            ? filter.Measure
            : ReportMeasure.Difference;
        var kind = measure switch
        {
            ReportMeasure.DifferencePercent => ReportValueKind.Percent,
            ReportMeasure.QuantityDifference => ReportValueKind.Quantity,
            _ => ReportValueKind.Money,
        };
        var model = new ReportModel(ReportKind.CalculatedVsBilled, filter)
        {
            HasRowTotals = measure is ReportMeasure.Difference or ReportMeasure.Calculated or ReportMeasure.Billed,
            ToCell = (_, acc) => new ReportCell(Value(measure, acc), acc.Approx || acc.Missing),
        };

        var months = filter.Months();
        var billsByContract = data.Bills
            .Where(b => b.ContractId is not null && b.Kind == BillKind.Regular)
            .ToLookup(b => b.ContractId!.Value);
        var meterUnits = data.Meters.ToDictionary(m => m.Id, m => m.Unit);

        foreach (var property in properties)
        {
            foreach (var contract in data.Contracts
                .Where(c => c.PropertyId == property.Id && c.BillingMode == BillingMode.Actual)
                .OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase))
            {
                var cells = new Dictionary<YearMonth, Acc>();
                foreach (var group in billsByContract[contract.Id].GroupBy(b => b.Period).Where(g => filter.Contains(g.Key)))
                {
                    cells[group.Key] = Month(data, contract, group.Key, [.. group]);
                }

                if (!cells.Values.Any(a => a.Any))
                {
                    continue;
                }

                var unit = data.ContractMeters
                    .Where(cm => cm.ContractId == contract.Id && meterUnits.ContainsKey(cm.MeterId))
                    .OrderBy(cm => cm.ValidFrom)
                    .Select(cm => (MeterUnit?)meterUnits[cm.MeterId])
                    .FirstOrDefault();
                var column = ReportBuilder.Column(
                    $"{property.Id}|{contract.Id}", property, ColumnSubject.Contract, measure, kind, contract.Title, contract.Service, unit: unit);
                model.Add(column, cells, (from, to) => new ReportDrill(DrillTarget.Bills, property.Id, from, to, ContractId: contract.Id));
            }
        }

        return model;
    }

    private static decimal? Value(ReportMeasure measure, Acc acc)
    {
        if (!acc.Any)
        {
            return null;
        }

        return measure switch
        {
            ReportMeasure.Calculated => acc.A,
            ReportMeasure.Billed => acc.B,
            ReportMeasure.QuantityDifference => acc.HasC ? acc.C : null,
            ReportMeasure.DifferencePercent => acc.A == 0 ? null : Math.Round((acc.B - acc.A) / acc.A * 100m, 1, MidpointRounding.AwayFromZero),
            _ => acc.B - acc.A,
        };
    }

    private static Acc Month(ReportData data, Contract contract, YearMonth month, IReadOnlyList<Bill> bills)
    {
        var billed = bills.Sum(b => ReportMoney.Eur(b.Gross, b.Currency));
        var charge = data.Charge(contract, month);

        // One bill with a stored snapshot: that is what the app calculated when the bill was entered.
        var calculated = bills is [{ CalculatedSnapshot: { } snapshot }] && Usable(snapshot) ? snapshot : charge;
        if (calculated is null || !Usable(calculated) || (ReferenceEquals(calculated, charge) && ContractQuantity.HasNoNewReading(calculated)))
        {
            return new Acc { Missing = true };
        }

        var acc = new Acc { A = ReportMoney.Eur(calculated.TotalGross, calculated.Currency), B = billed, Any = true };
        var onBill = bills.Where(b => b.ConsumptionOnBill is not null).Sum(b => b.ConsumptionOnBill!.Value);
        if (bills.Any(b => b.ConsumptionOnBill is not null) && ContractQuantity.Of(charge).Quantity is { } metered)
        {
            acc.C = onBill - metered;
            acc.HasC = true;
        }

        return acc;
    }

    /// <summary>A calculation can be compared when it is complete, has lines and takes no amount from the bill itself.</summary>
    private static bool Usable(CalculatedCharge charge) =>
        charge is { IsComplete: true, Lines.Count: > 0 } && !charge.Lines.Any(l => l.IsFromBill);
}
