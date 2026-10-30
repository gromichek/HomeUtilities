using System.Diagnostics;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>
/// Sanity check for long histories (T-028c): 20 years of monthly bills and payments for 45 contracts (3 properties × 15)
/// must not make the list screens slow to build. The limit is generous (CI machines are slow); the point is to catch
/// quadratic behaviour, which is several seconds at this size.
/// </summary>
public class ListPerformanceTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(1);

    private readonly BillTestKit _kit = new(Today);

    private void AddTwentyYears()
    {
        for (var c = 0; c < 45; c++)
        {
            var contract = _kit.AddWaterContract($"Contract {c}", tariff: false);
            for (var i = 0; i < 20 * 12; i++)
            {
                var period = new YearMonth(2007, 1).AddMonths(i);
                var bill = _kit.AddBill(contract, period, 40m + i % 7, snapshot: i % 5 == 0 ? _kit.Calculation(contract, period, 40m + i % 7) : null);
                _kit.AddPayment(40m + i % 7, (bill, 40m + i % 7));
            }
        }
    }

    [Fact]
    public async Task Bills_AllYears_OfTwentyYears_BuildsFast()
    {
        AddTwentyYears();
        var vm = _kit.CreateBills();
        await vm.ReloadAsync();
        vm.Period.Set(PeriodFilter.ForAllYears(2026));

        var watch = Stopwatch.StartNew();
        await vm.ReloadAsync();
        watch.Stop();

        Assert.Equal(ListPaging.PageSize, vm.Items.Count);
        Assert.True(vm.HasMoreItems);
        Assert.True(watch.Elapsed < Limit, $"Bills took {watch.Elapsed.TotalMilliseconds:N0} ms");
    }

    [Fact]
    public async Task Payments_AllYears_OfTwentyYears_BuildsFast()
    {
        AddTwentyYears();
        var vm = _kit.CreatePayments();
        await vm.ReloadAsync();
        vm.Period.Set(PeriodFilter.ForAllYears(2026));

        var watch = Stopwatch.StartNew();
        await vm.ReloadAsync();
        watch.Stop();

        Assert.Equal(ListPaging.PageSize, vm.Items.Count);
        Assert.True(vm.HasMoreItems);
        Assert.True(watch.Elapsed < Limit, $"Payments took {watch.Elapsed.TotalMilliseconds:N0} ms");
    }

    [Fact]
    public async Task Bills_ShowMore_AddsAPageAndAFilterChangeStartsAgain()
    {
        AddTwentyYears();
        var vm = _kit.CreateBills();
        await vm.ReloadAsync();
        vm.Period.Set(PeriodFilter.ForAllYears(2026));
        await vm.ReloadAsync();

        vm.ShowMoreCommand.Execute(null);

        Assert.Equal(2 * ListPaging.PageSize, vm.Items.Count);
        Assert.True(vm.HasMoreItems);

        await vm.ReloadAsync();

        Assert.Equal(2 * ListPaging.PageSize, vm.Items.Count);

        vm.Period.Set(PeriodFilter.ForYear(2025));
        await vm.ReloadAsync();

        Assert.Equal(ListPaging.PageSize, vm.Items.Count);
    }

    [Fact]
    public async Task Bills_ShortList_HasNoShowMore()
    {
        var contract = _kit.AddWaterContract(tariff: false);
        _kit.AddBill(contract, new YearMonth(2026, 9), 40m);
        var vm = _kit.CreateBills();
        await vm.ReloadAsync();

        Assert.False(vm.HasMoreItems);
    }

    [Fact]
    public async Task ReadingHistory_OfTwentyYears_ShowsAPageAtATimeAndBuildsFast()
    {
        var kit = new ContractTestKit(Today);
        var meter = new Meter
        {
            PropertyId = kit.Property.Id,
            Resource = MeterResource.ColdWater,
            Unit = MeterUnit.M3,
            InstalledOn = new DateOnly(2006, 12, 1),
        };
        var register = MeterRegister.CreateDefault(meter.Id);
        meter.Registers.Add(register);
        for (var i = 0; i < 240; i++)
        {
            register.Readings.Add(new MeterReading
            {
                RegisterId = register.Id,
                Date = new DateOnly(2007, 1, 15).AddMonths(i),
                Value = 100m + 7.5m * i,
                Kind = ReadingKind.Regular,
            });
        }

        kit.Meters.Stored.Add(meter);
        var vm = kit.CreateHistory();

        var watch = Stopwatch.StartNew();
        await vm.LoadAsync(meter.Id);
        watch.Stop();

        var group = Assert.Single(vm.Groups);
        Assert.Equal(240, group.Rows.Count);
        Assert.Equal(ReadingHistoryGroupViewModel.PageSize, group.VisibleRows.Count);
        Assert.True(group.HasMore);
        Assert.True(watch.Elapsed < Limit, $"History took {watch.Elapsed.TotalMilliseconds:N0} ms");

        group.ShowMoreCommand.Execute(null);

        Assert.Equal(2 * ReadingHistoryGroupViewModel.PageSize, group.VisibleRows.Count);
        Assert.Same(group.Rows[0], group.VisibleRows[0]);
        Assert.False(vm.IsLoading);
    }
}
