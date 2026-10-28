using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Reports;

/// <summary>
/// What one unit of a contract's metered quantity costs: the contract's bills of the month (gross, or net) divided by the metered
/// quantity. A month without a bill uses the calculation and is marked. Only contracts billed by consumption
/// (<see cref="BillingMode.Actual"/> with per-unit tariff lines) appear. A longer row (a year) divides the sums, not the average of months.
/// </summary>
internal static class UnitCostReport
{
    public static ReportModel Create(ReportData data, ReportFilter filter, IReadOnlyList<Property> properties)
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

        foreach (var property in properties)
        {
            foreach (var contract in data.Contracts
                .Where(c => c.PropertyId == property.Id && c.BillingMode == BillingMode.Actual)
                .OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase))
            {
                var shares = billsByContract[contract.Id]
                    .SelectMany(ReportMoney.Shares)
                    .Where(s => filter.Contains(s.Month))
                    .ToLookup(s => s.Month);
                var cells = new Dictionary<YearMonth, Acc>();
                foreach (var month in months.Where(m => Overlaps(contract, m)))
                {
                    var charge = data.Charge(contract, month);
                    var (quantity, hasPerUnit) = ContractQuantity.Of(charge);
                    if (!hasPerUnit)
                    {
                        continue;
                    }

                    var billed = shares[month].ToList();
                    decimal? money = null;
                    var approx = false;
                    if (billed.Count > 0)
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

                    if (quantity is not { } metered || money is not { } amount)
                    {
                        cells[month] = new Acc { Missing = true };
                    }
                    else
                    {
                        cells[month] = new Acc { A = amount, B = metered, Any = true, Approx = approx };
                    }
                }

                if (!cells.Values.Any(a => a.Any && a.B > 0))
                {
                    continue;
                }

                var unit = data.ContractMeters
                    .Where(cm => cm.ContractId == contract.Id && meterUnits.ContainsKey(cm.MeterId))
                    .OrderBy(cm => cm.ValidFrom)
                    .Select(cm => (MeterUnit?)meterUnits[cm.MeterId])
                    .FirstOrDefault();
                var column = ReportBuilder.Column(
                    $"{property.Id}|{contract.Id}", property, ColumnSubject.Contract, measure, ReportValueKind.UnitPrice,
                    contract.Title, contract.Service, unit: unit);
                model.Add(column, cells, (from, to) => new ReportDrill(DrillTarget.Bills, property.Id, from, to, ContractId: contract.Id));
            }
        }

        return model;
    }

    /// <summary>True if the contract applies on at least one day of the month.</summary>
    internal static bool Overlaps(Contract contract, YearMonth month) =>
        contract.ValidFrom <= month.LastDay && (contract.ValidTo is null || contract.ValidTo >= month.FirstDay);
}
