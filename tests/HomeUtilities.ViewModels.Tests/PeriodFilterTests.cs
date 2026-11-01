using System.Globalization;
using HomeUtilities.Core.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The shared period filter (all years / year / month) of the Bills and Payments screens.</summary>
public class PeriodFilterTests
{
    private static readonly DateOnly Today = new(2026, 10, 27);

    [Fact]
    public void Default_IsTheCurrentYear()
    {
        var filter = PeriodFilter.Default(Today);

        Assert.Equal(PeriodScope.Year, filter.Scope);
        Assert.Equal(2026, filter.Year);
    }

    [Fact]
    public void YearMode_StepsByYear_AndClamps()
    {
        Assert.Equal(PeriodFilter.ForYear(2025), PeriodFilter.ForYear(2026).Step(-1));
        Assert.Equal(PeriodFilter.ForYear(2027), PeriodFilter.ForYear(2026).Step(1));
        Assert.Equal(PeriodFilter.ForYear(1900), PeriodFilter.ForYear(1900).Step(-1));
        Assert.Equal(PeriodFilter.ForYear(2200), PeriodFilter.ForYear(2200).Step(1));
    }

    [Fact]
    public void MonthMode_StepsByMonth_AcrossTheYearBoundary()
    {
        Assert.Equal(PeriodFilter.ForMonth(new YearMonth(2026, 12)), PeriodFilter.ForMonth(new YearMonth(2027, 1)).Step(-1));
        Assert.Equal(PeriodFilter.ForMonth(new YearMonth(2027, 1)), PeriodFilter.ForMonth(new YearMonth(2026, 12)).Step(1));
        Assert.Equal(PeriodFilter.ForMonth(new YearMonth(2026, 9)), PeriodFilter.ForMonth(new YearMonth(2026, 10)).Step(-1));
    }

    [Fact]
    public void MonthMode_StopsAtTheLimits()
    {
        var first = PeriodFilter.ForMonth(new YearMonth(1900, 1));
        var last = PeriodFilter.ForMonth(new YearMonth(2200, 12));

        Assert.Equal(first, first.Step(-1));
        Assert.Equal(last, last.Step(1));
    }

    [Fact]
    public void AllYearsMode_DoesNotStep()
    {
        var all = PeriodFilter.ForAllYears(2026);

        Assert.Equal(all, all.Step(-1));
        Assert.Equal(all, all.Step(1));
    }

    [Fact]
    public void YearToMonth_UsesTheCurrentMonthWhenItIsInTheSelectedYear()
    {
        var month = PeriodFilter.ForYear(2026).WithScope(PeriodScope.Month, Today);

        Assert.Equal(PeriodFilter.ForMonth(new YearMonth(2026, 10)), month);
    }

    [Theory]
    [InlineData(2025)]
    [InlineData(2027)]
    public void YearToMonth_UsesJanuaryWhenTheCurrentMonthIsNotInTheSelectedYear(int year)
    {
        var month = PeriodFilter.ForYear(year).WithScope(PeriodScope.Month, Today);

        Assert.Equal(PeriodFilter.ForMonth(new YearMonth(year, 1)), month);
    }

    [Fact]
    public void MonthToYear_KeepsTheYear()
    {
        var year = PeriodFilter.ForMonth(new YearMonth(2025, 3)).WithScope(PeriodScope.Year, Today);

        Assert.Equal(PeriodFilter.ForYear(2025), year);
    }

    [Fact]
    public void MonthToMonth_ChangesNothing()
    {
        var month = PeriodFilter.ForMonth(new YearMonth(2025, 3));

        Assert.Equal(month, month.WithScope(PeriodScope.Month, Today));
    }

    [Fact]
    public void AllYears_RemembersTheYearForTheWayBack()
    {
        var all = PeriodFilter.ForYear(2024).WithScope(PeriodScope.AllYears, Today);

        Assert.Equal(PeriodScope.AllYears, all.Scope);
        Assert.Equal(PeriodFilter.ForYear(2024), all.WithScope(PeriodScope.Year, Today));
    }

    [Fact]
    public void Contains_Period_FollowsTheScope()
    {
        Assert.True(PeriodFilter.ForAllYears(2026).Contains(new YearMonth(2001, 5)));
        Assert.True(PeriodFilter.ForYear(2026).Contains(new YearMonth(2026, 12)));
        Assert.False(PeriodFilter.ForYear(2026).Contains(new YearMonth(2025, 12)));
        Assert.True(PeriodFilter.ForMonth(new YearMonth(2026, 10)).Contains(new YearMonth(2026, 10)));
        Assert.False(PeriodFilter.ForMonth(new YearMonth(2026, 10)).Contains(new YearMonth(2026, 11)));
        Assert.False(PeriodFilter.ForMonth(new YearMonth(2026, 10)).Contains(new YearMonth(2025, 10)));
    }

    [Fact]
    public void Contains_Date_FollowsTheScope()
    {
        var october = PeriodFilter.ForMonth(new YearMonth(2026, 10));

        Assert.True(october.Contains(new DateOnly(2026, 10, 1)));
        Assert.True(october.Contains(new DateOnly(2026, 10, 31)));
        Assert.False(october.Contains(new DateOnly(2026, 11, 1)));
        Assert.False(october.Contains(new DateOnly(2026, 9, 30)));
        Assert.True(PeriodFilter.ForYear(2026).Contains(new DateOnly(2026, 1, 1)));
        Assert.False(PeriodFilter.ForYear(2026).Contains(new DateOnly(2025, 12, 31)));
    }

    // ---- The control ----------------------------------------------------------------------------------------------

    private sealed class Harness
    {
        private readonly FakeLanguageService _language = new();

        public Harness()
        {
            Control = new PeriodFilterViewModel(
                () => Stored,
                value => Stored = value,
                new TestClock(Today),
                new FakeLocalizer(_language),
                Dialogs,
                () => Changes++,
                CultureInfo.InvariantCulture);
        }

        public FakeDialogService Dialogs { get; } = new();

        public PeriodFilter? Stored { get; private set; }

        public int Changes { get; private set; }

        public PeriodFilterViewModel Control { get; }
    }

    [Fact]
    public void Control_StartsInYearModeOfTheCurrentYear()
    {
        var h = new Harness();

        Assert.Equal(PeriodScope.Year, h.Control.Current.Scope);
        Assert.Equal("2026", h.Control.Label);
        Assert.True(h.Control.IsYearScope);
        Assert.True(h.Control.ShowStepper);
        Assert.Null(h.Stored);
    }

    [Fact]
    public void Control_LabelsAndStepper_FollowTheScope()
    {
        var h = new Harness();

        h.Control.ShowMonthCommand.Execute(null);
        Assert.Equal("October 2026", h.Control.Label);
        Assert.Equal("PeriodPreviousMonth [en]", h.Control.PreviousText);
        Assert.Equal("PeriodNextMonth [en]", h.Control.NextText);

        h.Control.ShowAllYearsCommand.Execute(null);
        Assert.Equal("PeriodAllYears [en]", h.Control.Label);
        Assert.False(h.Control.ShowStepper);

        h.Control.ShowYearCommand.Execute(null);
        Assert.Equal("2026", h.Control.Label);
        Assert.Equal("PeriodPreviousYear [en]", h.Control.PreviousText);
    }

    [Fact]
    public void Control_StepsAcrossTheYearBoundary_AndRemembersTheFilter()
    {
        var h = new Harness();
        h.Control.ShowYearCommand.Execute(null);
        h.Control.NextCommand.Execute(null); // 2027
        h.Control.ShowMonthCommand.Execute(null); // 2027 does not contain today's month -> January

        Assert.Equal("January 2027", h.Control.Label);

        h.Control.PreviousCommand.Execute(null);

        Assert.Equal("December 2026", h.Control.Label);
        Assert.Equal(PeriodFilter.ForMonth(new YearMonth(2026, 12)), h.Stored);
    }

    [Fact]
    public void Control_RaisesChangedForUserActions_ButNotForSet()
    {
        var h = new Harness();

        h.Control.NextCommand.Execute(null);
        h.Control.ShowMonthCommand.Execute(null);
        Assert.Equal(2, h.Changes);

        h.Control.Set(PeriodFilter.ForAllYears(2026));
        Assert.Equal(2, h.Changes);
        Assert.True(h.Control.IsAllYearsScope);
    }

    [Fact]
    public async Task ScopeDropDown_ShowsTheCurrentScopeAndOffersAllThree()
    {
        var h = new Harness();
        IReadOnlyList<string>? offered = null;
        h.Dialogs.ChooseScript = options =>
        {
            offered = options;
            return null;
        };

        Assert.Equal("PeriodYear [en]", h.Control.ScopeText);
        await h.Control.ChooseScopeCommand.ExecuteAsync(null);

        Assert.Equal(["PeriodAllYears [en]", "PeriodYear [en]", "PeriodMonth [en]"], offered);
        Assert.Equal(0, h.Changes);
        Assert.Null(h.Stored);
    }

    [Fact]
    public async Task ScopeDropDown_PickingAScope_ChangesTheFilterAndRebuildsTheList()
    {
        var h = new Harness();
        h.Dialogs.ChooseScript = options => options[2]; // Month

        await h.Control.ChooseScopeCommand.ExecuteAsync(null);

        Assert.Equal(PeriodScope.Month, h.Control.Current.Scope);
        Assert.Equal("October 2026", h.Control.Label);
        Assert.Equal("PeriodMonth [en]", h.Control.ScopeText);
        Assert.Equal(1, h.Changes);

        h.Dialogs.ChooseScript = options => options[0]; // All years
        await h.Control.ChooseScopeCommand.ExecuteAsync(null);

        Assert.True(h.Control.IsAllYearsScope);
        Assert.False(h.Control.ShowStepper);
        Assert.Equal(2, h.Changes);
    }

    [Fact]
    public async Task ScopeDropDown_PickingTheCurrentScope_ChangesNothing()
    {
        var h = new Harness();
        h.Dialogs.ChooseScript = options => options[1]; // Year, already active

        await h.Control.ChooseScopeCommand.ExecuteAsync(null);

        Assert.Equal(0, h.Changes);
    }
}
