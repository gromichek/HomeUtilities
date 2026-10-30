using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Reports;

/// <summary>
/// Per contract and month: what the app calculated (the snapshot stored with the bill, else the calculation as it is now), what
/// the supplier billed, the difference and the difference of quantities (water losses). Months without a usable calculation are marked.
/// A month an equalized plan covers is compared too: billed is the plan (the advance), calculated is what the readings cost by the tariff,
/// so the difference shows which way the settlement will go. A settlement is shown as billed in its own month.
/// A contract with meters but no tariff cannot be calculated: its months show what was billed and the quantities, the calculated amount
/// and the difference stay empty ("no tariff", not zero).
/// </summary>
internal static class CalculatedVsBilledReport
{
    public static ReportModel Create(ReportData data, ReportFilter filter, IReadOnlyList<Property> properties, CancellationToken cancellation = default)
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
            ToCell = (_, acc) => new ReportCell(Value(measure, acc), acc.Approx || acc.Missing || IsPartial(measure, acc)),
        };

        var months = filter.Months();
        var billsByContract = data.Bills
            .Where(b => !b.IsDraft && b.ContractId is not null && b.Kind is BillKind.Regular or BillKind.Equalized or BillKind.Settlement)
            .ToLookup(b => b.ContractId!.Value);
        var meterUnits = data.Meters.ToDictionary(m => m.Id, m => m.Unit);
        var metered = new MeteredQuantities(data, filter, cancellation);

        foreach (var property in properties)
        {
            foreach (var contract in data.Contracts
                .Where(c => c.PropertyId == property.Id && ReportData.CanBeBilledByConsumption(c))
                .OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase))
            {
                cancellation.ThrowIfCancellationRequested();
                var unit = ContractQuantity.FirstUnit(data, contract, meterUnits);
                var cells = new Dictionary<YearMonth, Acc>();
                foreach (var group in billsByContract[contract.Id].GroupBy(b => b.Period).Where(g => filter.Contains(g.Key)))
                {
                    // A settlement is billed in its own month with nothing to compare it with; the other bills of the month are compared.
                    var settlements = group.Where(b => b.Kind == BillKind.Settlement).ToList();
                    var others = group.Where(b => b.Kind != BillKind.Settlement).ToList();
                    var acc = others.Count > 0 ? Month(data, metered, contract, unit, group.Key, others) : default;
                    if (settlements.Count > 0)
                    {
                        acc += new Acc { B = settlements.Sum(b => ReportMoney.Eur(b.Gross, b.Currency)), Any = true };
                    }

                    // A bill outside the contract's dates (history older than its "valid from") is shown, but marked.
                    if (!UnitCostReport.Overlaps(contract, group.Key))
                    {
                        acc.Approx = true;
                    }

                    cells[group.Key] = acc;
                }

                if (!cells.Values.Any(a => a.Any))
                {
                    continue;
                }

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
            ReportMeasure.Calculated => acc.HasCalc ? acc.A : null,
            ReportMeasure.Billed => acc.B,
            ReportMeasure.QuantityDifference => acc.HasC ? acc.C : null,
            ReportMeasure.DifferencePercent => acc.HasCalc && acc.A != 0 ? Math.Round((acc.D - acc.A) / acc.A * 100m, 1, MidpointRounding.AwayFromZero) : null,
            _ => acc.HasCalc ? acc.D - acc.A : null,
        };
    }

    /// <summary>
    /// True if a figure compares only some of the billed months with a calculation (the others have no tariff): such a number is
    /// not the whole truth and is marked. The billed amount itself always covers all months.
    /// </summary>
    private static bool IsPartial(ReportMeasure measure, Acc acc) =>
        acc.HasCalc && acc.B != acc.D && measure is ReportMeasure.Difference or ReportMeasure.DifferencePercent or ReportMeasure.Calculated;

    private static Acc Month(ReportData data, MeteredQuantities metered, Contract contract, MeterUnit? unit, YearMonth month, IReadOnlyList<Bill> bills)
    {
        var billed = bills.Sum(b => ReportMoney.Eur(b.Gross, b.Currency));
        var charge = data.ActualCharge(contract, month);

        // The quantity the meters measured: from the per-unit lines of the calculation, else straight from the readings.
        var (fromCharge, hasPerUnit) = ContractQuantity.Of(charge);
        decimal? meteredQuantity = fromCharge;
        var readingsKnown = false;
        if ((!hasPerUnit || fromCharge is null)
            && unit is { } meterUnit
            && metered.Of(contract, month).TryGetValue(meterUnit, out var fromReadings)
            && fromReadings is { Any: true, Missing: false })
        {
            meteredQuantity = fromReadings.A;
            readingsKnown = true;
        }

        // One bill with a stored snapshot: that is what the app calculated when the bill was entered.
        var calculated = bills is [{ CalculatedSnapshot: { } snapshot }] && Usable(snapshot) ? snapshot : charge;
        if (calculated is null || !Usable(calculated) || (ReferenceEquals(calculated, charge) && ContractQuantity.HasNoNewReading(calculated)))
        {
            // No tariff to calculate with, but the readings are there: the bill and the quantities can still be shown.
            if (readingsKnown)
            {
                var noTariff = new Acc { B = billed, Any = true };
                AddQuantityDifference(ref noTariff, bills, meteredQuantity);
                return noTariff;
            }

            return new Acc { Missing = true };
        }

        var withCalc = new Acc { A = ReportMoney.Eur(calculated.TotalGross, calculated.Currency), B = billed, D = billed, Any = true, HasCalc = true };
        AddQuantityDifference(ref withCalc, bills, meteredQuantity);
        return withCalc;
    }

    private static void AddQuantityDifference(ref Acc acc, IReadOnlyList<Bill> bills, decimal? metered)
    {
        if (bills.Any(b => b.ConsumptionOnBill is not null) && metered is { } quantity)
        {
            acc.C = bills.Where(b => b.ConsumptionOnBill is not null).Sum(b => b.ConsumptionOnBill!.Value) - quantity;
            acc.HasC = true;
        }
    }

    /// <summary>A calculation can be compared when it is complete, has lines and takes no amount from the bill itself.</summary>
    private static bool Usable(CalculatedCharge charge) =>
        charge is { IsComplete: true, Lines.Count: > 0 } && !charge.Lines.Any(l => l.IsFromBill);
}
