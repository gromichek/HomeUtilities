using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Reports;

/// <summary>
/// What one unit of a contract's metered quantity costs: the contract's bills of the month (gross, or net) divided by the metered
/// quantity. A month without a bill uses the calculation and is marked. Only contracts billed by consumption
/// (<see cref="BillingMode.Actual"/> or <see cref="BillingMode.Equalized"/>) appear. A month an equalized plan covers is not left out: the
/// plan is only an advance, so its money is what the readings cost by the tariff (marked approximate), or the plan amount where there
/// is no tariff. The quantity comes from the per-unit
/// lines of the calculated charge; where the contract has none (the owner entered no tariff, only the bills of the past) it comes
/// from the readings of the meters linked to the contract. A contract metered in several units gets one column per unit.
/// A longer row (a year) divides the sums, not the average of months.
/// </summary>
internal static class UnitCostReport
{
    /// <summary>The cells of one column of a contract: the unit they are per, and the figures per month.</summary>
    private sealed class Series(MeterUnit? unit)
    {
        public MeterUnit? Unit { get; } = unit;

        public Dictionary<YearMonth, Acc> Cells { get; } = [];
    }

    public static ReportModel Create(ReportData data, ReportFilter filter, IReadOnlyList<Property> properties, CancellationToken cancellation = default)
    {
        var net = filter.Measure == ReportMeasure.Net;
        var measure = net ? ReportMeasure.Net : ReportMeasure.Gross;
        var model = new ReportModel(ReportKind.UnitCost, filter)
        {
            ToCell = (_, acc) => new ReportCell(
                acc.Any && acc.B > 0 ? ReportFormat.RoundUnitPrice(acc.A / acc.B) : null,
                acc.Approx || acc.Missing),
        };

        var months = filter.Months();
        var billsByContract = data.Bills
            .Where(b => !b.IsDraft && b.ContractId is not null && b.Kind is BillKind.Regular or BillKind.Correction)
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
                if (!data.CanHaveQuantity(contract))
                {
                    continue;
                }

                var shares = billsByContract[contract.Id]
                    .SelectMany(ReportMoney.Shares)
                    .Where(s => filter.Contains(s.Month))
                    .ToLookup(s => s.Month);
                var tariffUnit = ContractQuantity.FirstUnit(data, contract, meterUnits);
                var series = new List<Series>();
                Series SeriesOf(MeterUnit? unit)
                {
                    var found = series.Find(s => s.Unit == unit);
                    if (found is null)
                    {
                        found = new Series(unit);
                        series.Add(found);
                    }

                    return found;
                }

                foreach (var month in months.Where(m => Overlaps(contract, m) || shares[m].Any()))
                {
                    var equalized = data.IsEqualized(contract, month);
                    var charge = data.ActualCharge(contract, month);
                    var (quantity, hasPerUnit) = ContractQuantity.Of(charge);

                    var billed = shares[month].ToList();
                    decimal? money = null;
                    var approx = false;
                    if (equalized)
                    {
                        // By tariff, not by the advance; the plan amount only where there is no tariff to calculate with.
                        approx = true;
                        if (charge is { IsComplete: true, Lines.Count: > 0 })
                        {
                            money = ReportMoney.Eur(net ? charge.TotalNet : charge.TotalGross, charge.Currency);
                        }
                        else
                        {
                            money = data.PlanAmount(contract, month);
                        }
                    }
                    else if (billed.Count > 0)
                    {
                        money = billed.Sum(s => s.Of(measure));
                        approx = billed.Any(s => s.IsSpread);
                    }
                    else if (charge is { IsComplete: true, Lines.Count: > 0 })
                    {
                        money = net ? charge.TotalNet : charge.TotalGross;
                        money = ReportMoney.Eur(money.Value, charge.Currency);
                        approx = true;
                    }

                    // The calculated charge knows the quantity: the tariff is entered and the readings are there.
                    if (hasPerUnit && quantity is { } calculated)
                    {
                        SeriesOf(tariffUnit).Cells[month] = money is { } amount
                            ? new Acc { A = amount, B = calculated, Any = true, Approx = approx }
                            : new Acc { Missing = true };
                        continue;
                    }

                    // No tariff (or a tariff that cannot be applied to this month): the readings of the linked meters.
                    var fromMeters = metered.Of(contract, month);
                    if (fromMeters.Count == 0)
                    {
                        if (hasPerUnit)
                        {
                            SeriesOf(tariffUnit).Cells[month] = new Acc { Missing = true };
                        }

                        continue;
                    }

                    foreach (var (unit, meters) in fromMeters)
                    {
                        if (money is not { } total)
                        {
                            // Nothing was billed and nothing can be calculated: with a tariff that is a gap worth marking, without one it is just a quiet month.
                            if (hasPerUnit)
                            {
                                SeriesOf(unit).Cells[month] = new Acc { Missing = true };
                            }
                        }
                        else if (meters.Missing || !meters.Any)
                        {
                            SeriesOf(unit).Cells[month] = new Acc { Missing = true };
                        }
                        else
                        {
                            SeriesOf(unit).Cells[month] = new Acc { A = total, B = meters.A, Any = true, Approx = approx || meters.Approx };
                        }
                    }
                }

                var shown = series.Where(s => s.Cells.Values.Any(a => a.Any && a.B > 0)).OrderBy(s => s.Unit is null).ThenBy(s => s.Unit).ToList();
                foreach (var one in shown)
                {
                    var key = shown.Count == 1 ? $"{property.Id}|{contract.Id}" : $"{property.Id}|{contract.Id}|{one.Unit}";
                    var column = ReportBuilder.Column(
                        key, property, ColumnSubject.Contract, measure, ReportValueKind.UnitPrice,
                        contract.Title, contract.Service, unit: one.Unit);
                    model.Add(column, one.Cells, (from, to) => new ReportDrill(DrillTarget.Bills, property.Id, from, to, ContractId: contract.Id));
                }
            }
        }

        return model;
    }

    /// <summary>True if the contract applies on at least one day of the month.</summary>
    internal static bool Overlaps(Contract contract, YearMonth month) =>
        contract.ValidFrom <= month.LastDay && (contract.ValidTo is null || contract.ValidTo >= month.FirstDay);
}
