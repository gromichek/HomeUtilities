using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

/// <summary>T-027: the import of an invented workbook shaped like the owner's real one (reading dates, replacements, fallback columns).</summary>
public class ImportOwnerWorkbookTests
{
    private readonly ClosedXmlWorkbookReader _reader = new();
    private readonly ImportFixtures _f = new();

    public ImportOwnerWorkbookTests()
    {
        _f.Contract.ReadingDayFrom = 25;
        _f.Contract.ReadingDayTo = 28;
        _f.Cold.InstalledOn = new DateOnly(2004, 1, 1);
        _f.Hot.InstalledOn = new DateOnly(2004, 1, 1);
    }

    private SheetData Read(string sheet)
    {
        using var stream = OwnerWorkbook.Build();
        return _reader.ReadSheet(stream, sheet);
    }

    private ImportProfile WaterProfile() => new()
    {
        SheetName = OwnerWorkbook.Water,
        ContractId = _f.Contract.Id,
        PeriodColumn = 1,
        PeriodRule = ImportPeriodRule.ReadingWindow,
        Registers =
        [
            new RegisterColumnMapping { RegisterId = _f.ColdRegister.Id, ReadingColumn = 2 },
            new RegisterColumnMapping { RegisterId = _f.HotRegister.Id, ReadingColumn = 3 },
        ],
        QuantityColumn = 4,
        TotalColumn = 6,
        TotalFallbackColumn = 5,
        PaidColumn = 7,
        NotesColumns = [8],
        CurrencyRule = ImportCurrencyRule.Eur,
    };

    private ImportPlan PlanWater(ImportProfile? profile = null, IReadOnlyList<ReplacementEdit>? edits = null)
    {
        var context = new ImportContext(_f.Contract, [_f.Cold, _f.Hot], [], null, edits);
        return ImportPlanner.Plan(profile ?? WaterProfile(), Read(OwnerWorkbook.Water), context);
    }

    private static PlannedRow Row(ImportPlan plan, int number) => plan.Rows.Single(r => r.RowNumber == number);

    private PlannedReplacement ReplacementOn(ImportPlan plan, int row, MeterResource resource) =>
        plan.Replacements.Single(r => r.RowNumber == row && r.NewMeter.Resource == resource);

    [Fact]
    public void The_water_sheet_imports_without_errors_and_proposes_five_replacements()
    {
        var plan = PlanWater();

        Assert.Empty(plan.Problems);
        Assert.Equal(0, plan.ErrorRows);
        Assert.Equal(5, plan.Replacements.Count);
        Assert.Equal(5, plan.Counts.Replacements);
        Assert.Equal(3, plan.Replacements.Count(r => r.NewMeter.Resource == MeterResource.ColdWater));
        Assert.Equal(2, plan.Replacements.Count(r => r.NewMeter.Resource == MeterResource.HotWater));
    }

    [Theory]
    [InlineData(2, "2004-02-01", "2004-01")] // the 1st: the reading for the previous month (late reading of the January window)
    [InlineData(5, "2004-05-01", "2004-04")]
    [InlineData(6, "2004-05-07", "2004-04")] // the installation-day row belongs to the same month
    [InlineData(7, "2004-06-01", "2004-05")]
    [InlineData(8, "2004-06-26", "2004-06")] // after the switch to the 26th: inside the window
    [InlineData(16, "2005-02-26", "2005-02")]
    public void The_period_follows_the_reading_window_and_the_reading_keeps_its_date(int row, string date, string period)
    {
        var plan = PlanWater();

        Assert.Equal(YearMonth.Parse(period), Row(plan, row).Period);
        Assert.Equal(DateOnly.Parse(date), Row(plan, row).RowDate);
        Assert.All(Row(plan, row).Readings.Where(r => r.Reading.Kind == ReadingKind.Regular), r => Assert.Equal(DateOnly.Parse(date), r.Reading.Date));
    }

    [Fact]
    public void Pattern_1_the_extra_row_gives_exact_initial_values_and_the_last_reading_as_final()
    {
        var plan = PlanWater();

        var cold = ReplacementOn(plan, 6, MeterResource.ColdWater);
        var hot = ReplacementOn(plan, 6, MeterResource.HotWater);

        Assert.Equal(new DateOnly(2004, 5, 7), cold.Date);
        var coldLine = Assert.Single(cold.Registers);
        Assert.Equal((112m, 3m, false, false, true), (coldLine.FinalReading.Value, coldLine.InitialReading.Value, coldLine.FinalEstimated, coldLine.InitialEstimated, coldLine.InitialIsRowReading));
        // The new hot meter started at 477 (a jump up).
        var hotLine = Assert.Single(hot.Registers);
        Assert.Equal((56m, 477m), (hotLine.FinalReading.Value, hotLine.InitialReading.Value));

        // The sheet row lists the two initial readings of the new meters.
        var readings = Row(plan, 6).Readings;
        Assert.Equal(2, readings.Count);
        Assert.All(readings, r => Assert.Equal(ReadingKind.Initial, r.Reading.Kind));
        Assert.Contains(readings, r => r.RegisterId == coldLine.NewRegisterId && r.Reading.Value == 3m);
    }

    [Fact]
    public void Pattern_2_a_drop_in_a_regular_row_splits_the_consumption_column()
    {
        var plan = PlanWater();

        // Row 10 consumption 12 = hot 3 (489−486) + new cold 6 (6−0) + old cold 3 → final 14+3 = 17, dated the day after the last reading.
        var cold = ReplacementOn(plan, 10, MeterResource.ColdWater);
        var line = Assert.Single(cold.Registers);

        Assert.Equal(new DateOnly(2004, 7, 27), cold.Date);
        Assert.Equal((17m, 0m, true, true, false), (line.FinalReading.Value, line.InitialReading.Value, line.FinalEstimated, line.InitialEstimated, line.InitialIsRowReading));
        Assert.Equal(ReadingKind.Estimated, line.FinalReading.Kind);
        Assert.Equal("estimated during import", line.FinalReading.Note);
        // The reading of the row is a regular reading of the new meter.
        Assert.Contains(Row(plan, 10).Readings, r => r.RegisterId == line.NewRegisterId && r.Reading is { Value: 6m, Kind: ReadingKind.Regular });
    }

    [Fact]
    public void Pattern_3_the_note_gives_the_replacement_date()
    {
        var plan = PlanWater();

        // Row 11 consumption 10 = cold 3 + new hot 3 + old hot 4 → final 489+4.
        var hot = ReplacementOn(plan, 11, MeterResource.HotWater);
        var line = Assert.Single(hot.Registers);

        Assert.Equal(new DateOnly(2004, 8, 30), hot.Date);
        Assert.Equal((493m, 0m, true), (line.FinalReading.Value, line.InitialReading.Value, line.FinalEstimated));
    }

    [Fact]
    public void Pattern_4_the_note_gives_exact_final_initial_and_the_serial_number()
    {
        var plan = PlanWater();

        var cold = ReplacementOn(plan, 13, MeterResource.ColdWater);
        var line = Assert.Single(cold.Registers);

        Assert.Equal((15.3m, 0.072m, false, false), (line.FinalReading.Value, line.InitialReading.Value, line.FinalEstimated, line.InitialEstimated));
        Assert.Equal(ReadingKind.Final, line.FinalReading.Kind);
        Assert.Equal("1210007", cold.NewMeter.SerialNumber);
        Assert.Equal(new DateOnly(2004, 10, 27), cold.Date);
    }

    [Fact]
    public void The_switch_from_the_first_to_the_twenty_sixth_is_not_a_replacement()
    {
        var plan = PlanWater();

        // Rows 7 (01.06) and 8 (26.06) are in the same month without a drop.
        Assert.DoesNotContain(plan.Replacements, r => r.RowNumber is 7 or 8 or 9);
        Assert.DoesNotContain(Row(plan, 8).Issues, i => i.Code == ImportIssueCodes.ReplacementProposed);
    }

    [Fact]
    public void Chains_grow_by_the_replacements_and_every_reading_lands_on_the_meter_in_place()
    {
        var plan = PlanWater();

        var cold = plan.Replacements.Where(r => r.NewMeter.Resource == MeterResource.ColdWater).OrderBy(r => r.Date).ToList();
        Assert.Equal([_f.Cold.Id, cold[0].NewMeter.Id, cold[1].NewMeter.Id], [cold[0].OldMeterId, cold[1].OldMeterId, cold[2].OldMeterId]);
        Assert.True(cold[1].OldMeterIsPlanned);
        Assert.Equal([new DateOnly(2004, 5, 7), new DateOnly(2004, 7, 27), new DateOnly(2004, 10, 27)], cold.Select(r => r.NewMeter.InstalledOn));

        // Row 9 (26.07, cold 14) is still the first new meter; row 12 (26.10, cold 12) the second.
        Assert.Equal(cold[0].Registers[0].NewRegisterId, Row(plan, 9).Readings.Single(r => r.Reading.Value == 14m).RegisterId);
        Assert.Equal(cold[1].Registers[0].NewRegisterId, Row(plan, 12).Readings.Single(r => r.Reading.Value == 12m).RegisterId);
        Assert.Equal(cold[2].Registers[0].NewRegisterId, Row(plan, 14).Readings.Single(r => r.Reading.Value == 3.5m).RegisterId);
        // Before the first replacement the readings belong to the meter the owner set up.
        Assert.Equal(_f.ColdRegister.Id, Row(plan, 5).Readings.Single(r => r.Reading.Value == 112m).RegisterId);
    }

    [Fact]
    public void The_consumption_of_a_replacement_month_adds_up_to_the_consumption_column()
    {
        var plan = PlanWater();

        // Row 10: old cold (final − last reading) + new cold (reading − initial) + hot (reading − last reading) = 12.
        var line = ReplacementOn(plan, 10, MeterResource.ColdWater).Registers.Single();
        var oldCold = line.FinalReading.Value - 14m;
        var newCold = 6m - line.InitialReading.Value;
        var hot = Row(plan, 10).Readings.Single(r => r.RegisterId == _f.HotRegister.Id || plan.Replacements.Any(p => p.Registers.Any(x => x.NewRegisterId == r.RegisterId && p.NewMeter.Resource == MeterResource.HotWater))).Reading.Value - 486m;

        Assert.Equal(12m, oldCold + newCold + hot);
    }

    [Fact]
    public void The_new_meter_copies_resource_unit_location_and_digits_and_replaces_the_old_one()
    {
        _f.Cold.Location = "Bathroom";
        _f.Cold.Digits = 5;

        var plan = PlanWater();

        var first = plan.Replacements.Where(r => r.NewMeter.Resource == MeterResource.ColdWater).OrderBy(r => r.Date).First();
        Assert.Equal(("Bathroom", 5, MeterUnit.M3, _f.Cold.PropertyId, _f.Cold.Id), (first.NewMeter.Location, first.NewMeter.Digits, first.NewMeter.Unit, first.NewMeter.PropertyId, first.NewMeter.ReplacedMeterId));
        Assert.Equal(RegisterKind.Total, Assert.Single(first.NewMeter.Registers).Kind);
        // The stored meter is not touched by the dry run.
        Assert.Null(_f.Cold.RemovedOn);
    }

    [Fact]
    public void The_real_total_wins_and_the_calculated_total_is_the_fallback_and_the_preview_says_which_was_used()
    {
        var plan = PlanWater();

        // Row 5 has only the calculated total (E), row 15 has the real one (F) next to it.
        Assert.Equal(10.00m, Row(plan, 5).Bill!.Gross);
        Assert.Equal(["E"], Row(plan, 5).Issues.Single(i => i.Code == ImportIssueCodes.FallbackUsed).Args);
        Assert.Equal(ImportIssueSeverity.Info, Row(plan, 5).Issues.Single(i => i.Code == ImportIssueCodes.FallbackUsed).Severity);
        Assert.Equal(11.20m, Row(plan, 15).Bill!.Gross);
        Assert.DoesNotContain(Row(plan, 15).Issues, i => i.Code == ImportIssueCodes.FallbackUsed);
    }

    [Fact]
    public void Info_issues_are_neither_warnings_nor_errors()
    {
        var plan = PlanWater();

        // Row 14 has a bill by fallback and no other issue.
        Assert.False(Row(plan, 14).HasWarnings);
        Assert.False(Row(plan, 14).HasErrors);
    }

    [Fact]
    public void The_extra_row_has_no_bill_and_its_period_keeps_the_bill_of_the_row_before()
    {
        var plan = PlanWater();

        Assert.NotNull(Row(plan, 5).Bill);
        Assert.Null(Row(plan, 6).Bill);
        Assert.False(Row(plan, 6).IsSkipped);
    }

    [Fact]
    public void Turning_the_detection_off_makes_a_drop_an_error_again()
    {
        var profile = WaterProfile();
        profile.DetectReplacements = false;

        var plan = PlanWater(profile);

        Assert.Empty(plan.Replacements);
        Assert.True(Row(plan, 6).HasErrors);
    }

    [Fact]
    public void The_owner_can_correct_a_proposed_replacement()
    {
        var edits = new[] { new ReplacementEdit(10, _f.ColdRegister.Id, new DateOnly(2004, 8, 10), 16.5m, 0.2m, "AB-123") };

        var plan = PlanWater(edits: edits);

        var cold = ReplacementOn(plan, 10, MeterResource.ColdWater);
        var line = cold.Registers.Single();
        Assert.Equal(new DateOnly(2004, 8, 10), cold.Date);
        Assert.Equal((16.5m, 0.2m, false), (line.FinalReading.Value, line.InitialReading.Value, line.FinalEstimated));
        Assert.Equal(ReadingKind.Final, line.FinalReading.Kind);
        Assert.Equal("AB-123", cold.NewMeter.SerialNumber);
        Assert.Equal(new DateOnly(2004, 8, 10), line.FinalReading.Date);
    }

    [Fact]
    public void A_link_is_closed_and_a_new_one_starts_for_every_replacement()
    {
        var link = new ContractMeter { ContractId = _f.Contract.Id, MeterId = _f.Cold.Id, ValidFrom = new DateOnly(2004, 1, 1) };
        var context = new ImportContext(_f.Contract, [_f.Cold, _f.Hot], [], [link]);

        var plan = ImportPlanner.Plan(WaterProfile(), Read(OwnerWorkbook.Water), context);

        var first = plan.Replacements.Where(r => r.NewMeter.Resource == MeterResource.ColdWater).OrderBy(r => r.Date).ToList();
        var closed = Assert.Single(first[0].LinksToClose);
        Assert.Equal((link.Id, new DateOnly(2004, 5, 6)), (closed.Id, closed.ValidTo!.Value));
        Assert.Equal((_f.Contract.Id, first[0].NewMeter.Id, new DateOnly(2004, 5, 7)), (first[0].LinksToCreate[0].ContractId, first[0].LinksToCreate[0].MeterId, first[0].LinksToCreate[0].ValidFrom));
        // The second replacement ends the link of the first new meter the day before.
        Assert.Equal(new DateOnly(2004, 7, 26), first[0].LinksToCreate[0].ValidTo);
        Assert.Equal(first[1].NewMeter.Id, first[1].LinksToCreate[0].MeterId);
        // The stored link is not touched by the dry run.
        Assert.Null(link.ValidTo);
    }

    [Fact]
    public void Replanning_gives_the_same_result_and_does_not_change_the_stored_data()
    {
        var first = PlanWater();
        var second = PlanWater();

        Assert.Equal(first.Counts, second.Counts);
        Assert.Equal(first.Replacements.Select(r => (r.Date, r.RowNumber)), second.Replacements.Select(r => (r.Date, r.RowNumber)));
        Assert.Empty(_f.ColdRegister.Readings);
    }

    [Fact]
    public void A_chain_the_owner_already_set_up_is_followed_and_not_replaced_twice()
    {
        // The owner set up the meter chain for the first replacement (07.05.2004) already; the readings land on the new meter.
        var second = new Meter
        {
            PropertyId = _f.Cold.PropertyId,
            Resource = MeterResource.ColdWater,
            Unit = MeterUnit.M3,
            InstalledOn = new DateOnly(2004, 5, 7),
            ReplacedMeterId = _f.Cold.Id,
        };
        second.Registers.Add(MeterRegister.CreateDefault(second.Id));
        _f.Cold.RemovedOn = new DateOnly(2004, 5, 7);

        var context = new ImportContext(_f.Contract, [_f.Cold, second, _f.Hot], []);
        var plan = ImportPlanner.Plan(WaterProfile(), Read(OwnerWorkbook.Water), context);

        Assert.Equal(2, plan.Replacements.Count(r => r.NewMeter.Resource == MeterResource.ColdWater));
        Assert.DoesNotContain(plan.Replacements, r => r.RowNumber == 6 && r.NewMeter.Resource == MeterResource.ColdWater);
        Assert.Equal(second.Registers[0].Id, Row(plan, 9).Readings.Single(r => r.Reading.Value == 14m).RegisterId);
        Assert.Equal(_f.ColdRegister.Id, Row(plan, 5).Readings.Single(r => r.Reading.Value == 112m).RegisterId);
    }

    // ---- the heating sheet: dates on the 1st of the next month, no readings ----

    private ImportProfile HeatProfile() => new()
    {
        SheetName = OwnerWorkbook.Heat,
        ContractId = _f.Contract.Id,
        PeriodColumn = 1,
        PeriodRule = ImportPeriodRule.PreviousMonth,
        QuantityColumn = 2,
        TotalColumn = 3,
        PaidColumn = 4,
        CurrencyRule = ImportCurrencyRule.Eur,
    };

    [Fact]
    public void The_heating_sheet_imports_bills_for_the_previous_month_without_any_readings()
    {
        var plan = ImportPlanner.Plan(HeatProfile(), Read(OwnerWorkbook.Heat), new ImportContext(_f.Contract, [_f.Cold, _f.Hot], []));

        Assert.Empty(plan.Problems);
        Assert.Equal(new ImportCounts(Readings: 0, Bills: 3, Payments: 2, SkippedRows: 0), plan.Counts);
        Assert.Equal([new YearMonth(2004, 5), new YearMonth(2004, 6), new YearMonth(2004, 7)], plan.Rows.Select(r => r.Period!.Value));
        Assert.Equal([3m, 2m, 4m], plan.Rows.Select(r => r.Bill!.ConsumptionOnBill!.Value));
        Assert.Empty(plan.Rows.SelectMany(r => r.Readings));
    }

    // ---- the fixed-amount sheet: the 2nd is the payment date for the previous month ----

    private ImportProfile InternetProfile() => new()
    {
        SheetName = OwnerWorkbook.Internet,
        ContractId = _f.Contract.Id,
        PeriodColumn = 1,
        PeriodRule = ImportPeriodRule.PreviousMonth,
        DateIsPaymentDate = true,
        TotalColumn = 2,
        PaidColumn = 3,
        CurrencyRule = ImportCurrencyRule.Eur,
    };

    [Fact]
    public void A_fixed_amount_row_is_the_previous_month_paid_on_the_date_of_the_row()
    {
        var plan = ImportPlanner.Plan(InternetProfile(), Read(OwnerWorkbook.Internet), new ImportContext(_f.Contract, [], []));

        var july = Row(plan, 2);
        Assert.Equal(new YearMonth(2022, 11), july.Period);
        Assert.Equal(new DateOnly(2022, 12, 2), july.Payment!.PaidOn);
        Assert.Equal(15.00m, july.Payment.Amount);
    }

    [Fact]
    public void Without_the_option_the_payment_is_dated_the_first_of_the_period_month()
    {
        var profile = InternetProfile();
        profile.DateIsPaymentDate = false;

        var plan = ImportPlanner.Plan(profile, Read(OwnerWorkbook.Internet), new ImportContext(_f.Contract, [], []));

        Assert.Equal(new DateOnly(2022, 11, 1), Row(plan, 2).Payment!.PaidOn);
    }

    [Fact]
    public void A_rule_change_by_date_moves_prepaid_rows_to_the_next_month()
    {
        var profile = InternetProfile();
        profile.PeriodRuleChanges = [new PeriodRuleChange { FromDate = new DateOnly(2023, 2, 1), Rule = ImportPeriodRule.NextMonth }];

        var plan = ImportPlanner.Plan(profile, Read(OwnerWorkbook.Internet), new ImportContext(_f.Contract, [], []));

        Assert.Equal(
            [new YearMonth(2022, 11), new YearMonth(2022, 12), new YearMonth(2023, 3), new YearMonth(2023, 5)],
            plan.Rows.Select(r => r.Period!.Value));
    }

    [Fact]
    public void A_row_override_changes_the_period_spreads_the_bill_over_months_and_adds_the_remark()
    {
        var profile = InternetProfile();
        profile.PeriodRuleChanges = [new PeriodRuleChange { FromDate = new DateOnly(2023, 2, 1), Rule = ImportPeriodRule.NextMonth }];
        profile.RowOverrides =
        [
            new ImportRowOverride
            {
                RowDate = new DateOnly(2023, 2, 2),
                Period = new DateOnly(2023, 2, 1),
                CoversUntil = new DateOnly(2023, 3, 1),
                Remark = "incl. connection fee",
            },
        ];

        var plan = ImportPlanner.Plan(profile, Read(OwnerWorkbook.Internet), new ImportContext(_f.Contract, [], []));

        var row = Row(plan, 4);
        Assert.Equal(new YearMonth(2023, 2), row.Period);
        Assert.Equal(new YearMonth(2023, 3), row.RuleBasedPeriod);
        Assert.Equal((new DateOnly(2023, 2, 1), new DateOnly(2023, 3, 31)), (row.Bill!.ServiceFrom!.Value, row.Bill.ServiceTo!.Value));
        Assert.Equal("incl. connection fee", row.Bill.Notes);
        Assert.Equal(new DateOnly(2023, 2, 2), row.Payment!.PaidOn);
        Assert.Contains(row.Issues, i => i.Code == ImportIssueCodes.RowOverridden);
    }

    [Fact]
    public void Two_rows_in_one_period_import_both_readings_and_one_bill()
    {
        // Rows 5 and 6 are both April; the bill comes from row 5 (row 6 has no amount), readings from both rows.
        var plan = PlanWater();

        Assert.Equal(new YearMonth(2004, 4), Row(plan, 5).Period);
        Assert.Equal(new YearMonth(2004, 4), Row(plan, 6).Period);
        Assert.Single(plan.Rows, r => r.Period == new YearMonth(2004, 4) && r.Bill is not null);
        Assert.NotEmpty(Row(plan, 5).Readings);
        Assert.NotEmpty(Row(plan, 6).Readings);
    }

    [Fact]
    public void The_profile_survives_a_json_round_trip_with_the_new_options()
    {
        var profile = WaterProfile();
        profile.PeriodRuleChanges = [new PeriodRuleChange { FromDate = new DateOnly(2023, 2, 1), Rule = ImportPeriodRule.NextMonth }];
        profile.RowOverrides = [new ImportRowOverride { RowDate = new DateOnly(2023, 2, 2), Period = new DateOnly(2023, 2, 1), Remark = "x" }];
        profile.DateIsPaymentDate = true;
        profile.DetectReplacements = false;
        profile.ColumnHeaders = [new ImportColumnHeader { Field = "Total", Column = "F", Header = "Real total" }];

        var (_, read, error) = ImportProfileFile.Read(ImportProfileFile.Write("Water", profile));

        Assert.Null(error);
        Assert.Equal(ImportPeriodRule.ReadingWindow, read.PeriodRule);
        Assert.Equal(5, read.TotalFallbackColumn);
        Assert.Equal(ImportPeriodRule.NextMonth, Assert.Single(read.PeriodRuleChanges).Rule);
        Assert.Equal("x", Assert.Single(read.RowOverrides).Remark);
        Assert.True(read.DateIsPaymentDate);
        Assert.False(read.DetectReplacements);
        Assert.Equal("Real total", Assert.Single(read.ColumnHeaders).Header);
    }

    [Fact]
    public void An_older_profile_without_the_new_fields_still_reads_with_detection_on()
    {
        const string json = """{"version":1,"name":"Old","profile":{"sheetName":"Water","contractId":"00000000-0000-0000-0000-000000000001","periodColumn":1,"totalColumn":2}}""";

        var (_, profile, error) = ImportProfileFile.Read(json);

        Assert.Null(error);
        Assert.Equal(ImportPeriodRule.MonthOfDate, profile.PeriodRule);
        Assert.True(profile.DetectReplacements);
        Assert.Empty(profile.RowOverrides);
    }
}
