using System.Diagnostics;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;
using static HomeUtilities.Core.Tests.Reports.ReportScenario;

namespace HomeUtilities.Core.Tests.Reports;

public class LargeDataTests
{
    /// <summary>
    /// 20 years x 3 properties x 15 contracts (5 of them metered, 3 with a tariff and 2 without), a bill per contract and month,
    /// a reading per meter and month. A sanity check against loops over all bills or readings for every cell, not a benchmark.
    /// </summary>
    [Fact]
    public void Twenty_years_of_three_properties_and_fifteen_contracts_each_are_reported_in_two_seconds()
    {
        var s = new ReportScenario();
        var properties = new List<Property> { s.Flat, s.AddProperty("Cottage"), s.AddProperty("Garage", 3) };
        var first = new YearMonth(2007, 1);
        var last = new YearMonth(2026, 12);
        var months = first.MonthsUntil(last) + 1;
        var expectedBills = 0;
        foreach (var property in properties)
        {
            for (var c = 0; c < 15; c++)
            {
                var contract = s.AddContract($"Contract {c}", c % 2 == 0 ? s.Water : s.Maintenance, property);
                contract.ValidFrom = new DateOnly(2006, 12, 1);
                var metered = c < 5;
                if (metered)
                {
                    if (c < 3)
                    {
                        s.AddPerUnit(contract, "Quantity", 1.50m);
                    }

                    var meter = Meter(new DateOnly(2006, 12, 1), 8);
                    meter.PropertyId = property.Id;
                    for (var m = -1; m < months; m++)
                    {
                        var month = first.AddMonths(m);
                        meter.Add(month.LastDay, 100m + (m + 1) * 3m);
                    }

                    s.Meters.Add(meter);
                    s.Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2006, 12, 1) });
                }

                for (var m = 0; m < months; m++)
                {
                    s.AddBill(contract, 10m + c, first.AddMonths(m), property: property);
                    expectedBills++;
                }
            }
        }

        var data = s.Data;
        var filter = Filter(first, last);
        var watch = Stopwatch.StartNew();
        var costs = ReportBuilder.Costs(data, filter);
        var costsTime = watch.Elapsed;
        var consumption = ReportBuilder.Consumption(data, filter);
        var consumptionTime = watch.Elapsed - costsTime;
        var unitCost = ReportBuilder.UnitCost(data, filter);
        var unitCostTime = watch.Elapsed - costsTime - consumptionTime;
        var compared = ReportBuilder.CalculatedVsBilled(data, filter);
        watch.Stop();
        var breakdown = $"costs {costsTime.TotalMilliseconds:0} ms, consumption {consumptionTime.TotalMilliseconds:0} ms, unit cost {unitCostTime.TotalMilliseconds:0} ms, total {watch.Elapsed.TotalMilliseconds:0} ms";

        Assert.Equal(240, costs.Rows.Count);
        Assert.Equal(
            properties.Sum(_ => Enumerable.Range(0, 15).Sum(c => (10m + c) * months)),
            costs.Totals!.Total!.Value);
        Assert.Equal(3, consumption.Columns.Count);
        Assert.Equal(15, unitCost.Columns.Count);
        Assert.Equal(15, compared.Columns.Count);
        Assert.True(expectedBills > 0);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"The reports took too long: {breakdown}.");
    }
}
