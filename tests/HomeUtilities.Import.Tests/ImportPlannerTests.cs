using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

public class ImportPlannerTests
{
    private readonly ClosedXmlWorkbookReader _reader = new();
    private readonly ImportFixtures _f = new();

    private SheetData Read(string sheet)
    {
        using var stream = SyntheticWorkbook.Build();
        return _reader.ReadSheet(stream, sheet);
    }

    private static EqualizedPlan Plan(Contract contract, DateOnly from, decimal amount) =>
        new() { ContractId = contract.Id, ValidFrom = from, MonthlyAmount = amount };

    private static PlannedRow Row(ImportPlan plan, int number) => plan.Rows.Single(r => r.RowNumber == number);

    private static bool Has(PlannedRow row, string code, ImportIssueSeverity severity) =>
        row.Issues.Any(i => i.Code == code && i.Severity == severity);

    [Fact]
    public void Water_sheet_gives_the_expected_counts()
    {
        // Rows 2-5 import (readings of 2 registers each = 8), 3 bills (row 5 has no total), 2 payments (rows 2 and 3),
        // row 6 is skipped (cold dial lower than before); the row after the empty row 7 is not read.
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.Empty(plan.Problems);
        Assert.Equal(new ImportCounts(Readings: 8, Bills: 3, Payments: 2, SkippedRows: 1), plan.Counts);
        Assert.Equal([2, 3, 4, 5, 6], plan.Rows.Select(r => r.RowNumber));
        Assert.Equal(1, plan.ErrorRows);
        Assert.Equal(1, plan.WarningRows);
    }

    [Fact]
    public void Rows_before_the_contract_start_are_imported_with_a_warning_and_a_suggested_start()
    {
        // Periods are 2013-11, 2013-12, 2014-01, 2014-02, 2014-03; the contract starts on 2014-02-10: the first three are before it.
        _f.Contract.ValidFrom = new DateOnly(2014, 2, 10);

        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        foreach (var number in new[] { 2, 3, 4 })
        {
            var issue = Row(plan, number).Issues.Single(i => i.Code == ImportIssueCodes.PeriodBeforeContractStart);
            Assert.Equal(ImportIssueSeverity.Warning, issue.Severity);
            Assert.Equal(["10.02.2014"], issue.Args);
            Assert.NotNull(Row(plan, number).Bill);
        }

        Assert.DoesNotContain(Row(plan, 5).Issues, i => i.Code == ImportIssueCodes.PeriodBeforeContractStart);
        Assert.Equal(3, plan.RowsBeforeContractStart);
        Assert.Equal(new DateOnly(2013, 11, 1), plan.SuggestedContractStart);
        Assert.Equal(new DateOnly(2014, 2, 10), plan.ContractValidFrom);
    }

    [Fact]
    public void A_contract_that_covers_the_sheet_gives_no_validity_warning()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.Equal(0, plan.RowsBeforeContractStart);
        Assert.Null(plan.SuggestedContractStart);
        Assert.DoesNotContain(plan.Rows.SelectMany(r => r.Issues), i => i.Code is ImportIssueCodes.PeriodBeforeContractStart or ImportIssueCodes.PeriodAfterContractEnd);
    }

    [Fact]
    public void Rows_after_the_contract_end_get_a_warning()
    {
        _f.Contract.ValidTo = new DateOnly(2013, 12, 31);

        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.True(Has(Row(plan, 4), ImportIssueCodes.PeriodAfterContractEnd, ImportIssueSeverity.Warning));
        Assert.False(Has(Row(plan, 3), ImportIssueCodes.PeriodAfterContractEnd, ImportIssueSeverity.Warning));
    }

    [Fact]
    public void Each_period_format_of_the_water_sheet_is_understood()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.Equal(
            [new YearMonth(2013, 11), new YearMonth(2013, 12), new YearMonth(2014, 1), new YearMonth(2014, 2), new YearMonth(2014, 3)],
            plan.Rows.Select(r => r.Period!.Value));
    }

    [Fact]
    public void Lvl_before_2014_and_eur_after_follows_the_currency_rule()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.Equal((12.34m, Currency.LVL), (Row(plan, 2).Bill!.Gross, Row(plan, 2).Bill!.Currency));
        Assert.Equal((13.00m, Currency.LVL), (Row(plan, 3).Bill!.Gross, Row(plan, 3).Bill!.Currency));
        Assert.Equal((11.50m, Currency.EUR), (Row(plan, 4).Bill!.Gross, Row(plan, 4).Bill!.Currency));
    }

    [Theory]
    [InlineData(ImportCurrencyRule.Eur, 2013, 12, Currency.EUR)]
    [InlineData(ImportCurrencyRule.Lvl, 2020, 1, Currency.LVL)]
    [InlineData(ImportCurrencyRule.LvlBefore2014, 2013, 12, Currency.LVL)]
    [InlineData(ImportCurrencyRule.LvlBefore2014, 2014, 1, Currency.EUR)]
    public void Currency_rule_decides_per_period(ImportCurrencyRule rule, int year, int month, Currency expected)
    {
        var profile = new ImportProfile { CurrencyRule = rule };

        Assert.Equal(expected, profile.CurrencyFor(new YearMonth(year, month)));
    }

    [Fact]
    public void A_paid_marker_creates_a_payment_equal_to_the_bill_total_dated_the_first_of_the_month()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        var row = Row(plan, 2);
        var payment = row.Payment!;
        Assert.Equal((12.34m, Currency.LVL, new DateOnly(2013, 11, 1)), (payment.Amount, payment.Currency, payment.PaidOn));
        Assert.Equal("imported", payment.Reference);
        Assert.Equal(_f.Contract.Id, payment.ContractId);
        var allocation = Assert.Single(payment.Allocations);
        Assert.Equal((row.Bill!.Id, 12.34m, payment.Id), (allocation.BillId, allocation.Amount, allocation.PaymentId));
        Assert.Null(Row(plan, 4).Payment);
    }

    [Fact]
    public void The_paid_date_column_dates_the_payment()
    {
        var plan = ImportPlanner.Plan(_f.WasteProfile(), Read(SyntheticWorkbook.Waste), _f.Context());

        Assert.Equal(new DateOnly(2014, 2, 10), Row(plan, 2).Payment!.PaidOn);
        Assert.Equal(new DateOnly(2014, 2, 1), Row(plan, 3).Payment!.PaidOn);
    }

    [Fact]
    public void The_paid_marker_text_is_configurable_and_ignores_case_and_spaces()
    {
        var profile = _f.WaterProfile();
        profile.PaidMarker = " paid ";
        var sheet = new SheetData("S", [new SheetRow(1, []), Cells(2, SheetCell.Date(new DateOnly(2014, 1, 1)), SheetCell.String("PAID"), SheetCell.Number(10m))], 3);
        profile.Registers = [];
        profile.PaidColumn = 2;
        profile.TotalColumn = 3;
        profile.NotesColumns = [];

        var plan = ImportPlanner.Plan(profile, sheet, _f.Context());

        Assert.Equal(10m, Row(plan, 2).Payment!.Amount);
    }

    [Fact]
    public void Missing_total_with_readings_imports_the_readings_only_with_a_warning()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        var row = Row(plan, 5);
        Assert.Null(row.Bill);
        Assert.Equal(2, row.Readings.Count);
        Assert.True(Has(row, ImportIssueCodes.TotalMissing, ImportIssueSeverity.Warning));
        Assert.False(row.IsSkipped);
    }

    [Fact]
    public void A_lower_dial_without_digits_is_an_error_that_skips_the_whole_row()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        var row = Row(plan, 6);
        Assert.True(Has(row, ImportIssueCodes.ReadingLowerThanPrevious, ImportIssueSeverity.Error));
        Assert.Equal(["B", "20", "109"], row.Issues.Single(i => i.Code == ImportIssueCodes.ReadingLowerThanPrevious).Args);
        Assert.True(row.IsSkipped);
        Assert.Null(row.Bill);
        Assert.Null(row.Payment);
        Assert.Empty(row.Readings);
    }

    [Fact]
    public void A_lower_dial_on_a_meter_with_digits_is_a_rollover_warning()
    {
        _f.Cold.Digits = 4;
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        var row = Row(plan, 6);
        Assert.True(Has(row, ImportIssueCodes.ReadingRollover, ImportIssueSeverity.Warning));
        Assert.False(row.HasErrors);
        Assert.Equal(2, row.Readings.Count);
        Assert.NotNull(row.Bill);
    }

    [Fact]
    public void The_first_reading_of_a_register_without_readings_is_Initial_the_rest_Regular()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        var cold = plan.Rows.SelectMany(r => r.Readings).Where(r => r.RegisterId == _f.ColdRegister.Id).OrderBy(r => r.Reading.Date).ToList();
        Assert.Equal([ReadingKind.Initial, ReadingKind.Regular, ReadingKind.Regular, ReadingKind.Regular], cold.Select(r => r.Reading.Kind));
        Assert.Equal([100.5m, 103.2m, 106m, 109m], cold.Select(r => r.Reading.Value));
    }

    [Fact]
    public void No_Initial_reading_is_planned_when_the_register_already_has_readings()
    {
        _f.ColdRegister.Readings.Add(new MeterReading { RegisterId = _f.ColdRegister.Id, Date = new DateOnly(2013, 10, 31), Value = 90, Kind = ReadingKind.Initial });

        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.DoesNotContain(plan.Rows.SelectMany(r => r.Readings).Where(r => r.RegisterId == _f.ColdRegister.Id), r => r.Reading.Kind == ReadingKind.Initial);
    }

    [Fact]
    public void The_reading_keeps_the_date_of_a_date_cell_and_is_the_last_day_of_a_text_period()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.Equal(new DateOnly(2013, 11, 1), Row(plan, 2).Readings[0].Reading.Date);
        Assert.Equal(new DateOnly(2014, 2, 28), Row(plan, 5).Readings[0].Reading.Date);
    }

    [Fact]
    public void The_reading_date_column_wins_when_mapped()
    {
        var plan = ImportPlanner.Plan(_f.ElectricityProfile(), Read(SyntheticWorkbook.Electricity), _f.Context(_f.Power));

        Assert.Equal(
            [new DateOnly(2014, 1, 25), new DateOnly(2014, 2, 26)],
            plan.Rows.Where(r => !r.HasErrors).Select(r => r.Readings.Single().Reading.Date));
    }

    [Fact]
    public void An_unreadable_total_is_an_error_naming_the_column_and_text()
    {
        var plan = ImportPlanner.Plan(_f.ElectricityProfile(), Read(SyntheticWorkbook.Electricity), _f.Context(_f.Power));

        var row = Row(plan, 4);
        Assert.Equal(["D", "n/a"], row.Issues.Single(i => i.Code == ImportIssueCodes.NumberInvalid).Args);
        Assert.True(row.IsSkipped);
        Assert.Equal(new ImportCounts(Readings: 2, Bills: 2, Payments: 0, SkippedRows: 1), plan.Counts);
    }

    [Fact]
    public void Net_vat_percent_and_total_that_agree_are_all_kept()
    {
        var plan = ImportPlanner.Plan(_f.HeatProfile(), Read(SyntheticWorkbook.Heat), _f.Context(_f.Hot));

        var bill = Row(plan, 2).Bill!;
        Assert.Equal((100.00m, 12m, 12.00m, 112.00m), (bill.Net, bill.VatPercent, bill.Vat, bill.Gross));
        Assert.Empty(Row(plan, 2).Issues);
    }

    [Fact]
    public void Inconsistent_amounts_import_the_total_alone_with_a_warning()
    {
        var plan = ImportPlanner.Plan(_f.HeatProfile(), Read(SyntheticWorkbook.Heat), _f.Context(_f.Hot));

        var row = Row(plan, 3);
        Assert.Equal((null, null, null, 113.50m), (row.Bill!.Net, row.Bill.VatPercent, row.Bill.Vat, row.Bill.Gross));
        Assert.True(Has(row, ImportIssueCodes.AmountsInconsistent, ImportIssueSeverity.Warning));
    }

    [Fact]
    public void Net_and_vat_amounts_without_a_total_give_the_total()
    {
        var profile = _f.WasteProfile();
        profile.TotalColumn = 0;

        var plan = ImportPlanner.Plan(profile, Read(SyntheticWorkbook.Waste), _f.Context());

        var bill = Row(plan, 2).Bill!;
        Assert.Equal((10.00m, 21.00m, 2.10m, 12.10m), (bill.Net, bill.VatPercent, bill.Vat, bill.Gross));
    }

    [Fact]
    public void Net_vat_and_total_within_two_cents_are_consistent()
    {
        var sheet = new SheetData("S", [new SheetRow(1, []), Cells(2, SheetCell.Date(new DateOnly(2014, 5, 1)), SheetCell.Number(10m), SheetCell.Number(2.10m), SheetCell.Number(12.12m))], 4);
        var profile = _f.WasteProfile();
        profile.PaidColumn = 0;
        profile.PaidDateColumn = 0;

        var plan = ImportPlanner.Plan(profile, sheet, _f.Context());

        Assert.Equal(12.12m, Row(plan, 2).Bill!.Gross);
        Assert.DoesNotContain(Row(plan, 2).Issues, i => i.Code == ImportIssueCodes.AmountsInconsistent);
    }

    [Fact]
    public void Total_only_rows_keep_just_the_total()
    {
        var plan = ImportPlanner.Plan(_f.HeatProfile(), Read(SyntheticWorkbook.Heat), _f.Context(_f.Hot));

        var bill = Row(plan, 4).Bill!;
        Assert.Equal((null, null, null, 120.00m), (bill.Net, bill.VatPercent, bill.Vat, bill.Gross));
        Assert.Equal(120.00m, Row(plan, 4).Payment!.Amount);
    }

    [Fact]
    public void Of_two_rows_with_the_same_period_the_later_one_supplies_the_bill()
    {
        var plan = ImportPlanner.Plan(_f.WasteProfile(), Read(SyntheticWorkbook.Waste), _f.Context());

        var earlier = Row(plan, 4);
        var later = Row(plan, 5);
        Assert.True(Has(earlier, ImportIssueCodes.PeriodSuperseded, ImportIssueSeverity.Warning));
        Assert.Equal(["2014-03", "5"], earlier.Issues.Single(i => i.Code == ImportIssueCodes.PeriodSuperseded).Args);
        Assert.True(earlier.IsSkipped);
        Assert.True(Has(later, ImportIssueCodes.PeriodSupersedes, ImportIssueSeverity.Warning));
        Assert.NotNull(later.Bill);
        Assert.Equal(new ImportCounts(Readings: 0, Bills: 3, Payments: 2, SkippedRows: 1), plan.Counts);
    }

    [Fact]
    public void An_existing_bill_of_the_contract_and_period_makes_the_row_a_duplicate()
    {
        var existing = new Bill { ContractId = _f.Contract.Id, Period = new YearMonth(2014, 1), Gross = 1 };
        var context = new ImportContext(_f.Contract, [], [existing]);

        var plan = ImportPlanner.Plan(_f.WasteProfile(), Read(SyntheticWorkbook.Waste), context);

        Assert.True(Has(Row(plan, 2), ImportIssueCodes.DuplicatePeriod, ImportIssueSeverity.Warning));
        Assert.Null(Row(plan, 2).Bill);
        Assert.Null(Row(plan, 2).Payment);
        Assert.NotNull(Row(plan, 3).Bill);
    }

    [Fact]
    public void Deleted_and_foreign_bills_do_not_count_as_duplicates()
    {
        var deleted = new Bill { ContractId = _f.Contract.Id, Period = new YearMonth(2014, 1), Gross = 1, IsDeleted = true };
        var foreign = new Bill { ContractId = Guid.NewGuid(), Period = new YearMonth(2014, 2), Gross = 1 };
        var context = new ImportContext(_f.Contract, [], [deleted, foreign]);

        var plan = ImportPlanner.Plan(_f.WasteProfile(), Read(SyntheticWorkbook.Waste), context);

        Assert.NotNull(Row(plan, 2).Bill);
        Assert.NotNull(Row(plan, 3).Bill);
    }

    [Fact]
    public void A_reading_that_exists_on_the_date_is_skipped_with_a_warning_but_the_bill_is_kept()
    {
        _f.PowerRegister.Readings.Add(new MeterReading { RegisterId = _f.PowerRegister.Id, Date = new DateOnly(2014, 1, 25), Value = 1000, Kind = ReadingKind.Initial });

        var plan = ImportPlanner.Plan(_f.ElectricityProfile(), Read(SyntheticWorkbook.Electricity), _f.Context(_f.Power));

        var row = Row(plan, 2);
        Assert.True(Has(row, ImportIssueCodes.ReadingExists, ImportIssueSeverity.Warning));
        Assert.Empty(row.Readings);
        Assert.NotNull(row.Bill);
    }

    [Fact]
    public void A_reading_before_the_meter_was_installed_is_an_error()
    {
        _f.Power.InstalledOn = new DateOnly(2014, 2, 1);

        var plan = ImportPlanner.Plan(_f.ElectricityProfile(), Read(SyntheticWorkbook.Electricity), _f.Context(_f.Power));

        Assert.True(Has(Row(plan, 2), ImportIssueCodes.ReadingOutsideInstallation, ImportIssueSeverity.Error));
        Assert.True(Row(plan, 2).IsSkipped);
        Assert.False(Row(plan, 3).HasErrors);
    }

    [Fact]
    public void A_reading_higher_than_a_later_existing_one_is_an_error_without_digits()
    {
        _f.PowerRegister.Readings.Add(new MeterReading { RegisterId = _f.PowerRegister.Id, Date = new DateOnly(2014, 3, 10), Value = 1100, Kind = ReadingKind.Regular });

        var plan = ImportPlanner.Plan(_f.ElectricityProfile(), Read(SyntheticWorkbook.Electricity), _f.Context(_f.Power));

        Assert.False(Row(plan, 2).HasErrors);
        Assert.True(Has(Row(plan, 3), ImportIssueCodes.ReadingHigherThanNext, ImportIssueSeverity.Error));
    }

    [Fact]
    public void Rows_covered_by_a_plan_get_equalized_bills_and_the_others_regular_bills()
    {
        var equalized = new ImportFixtures(BillingMode.Equalized);
        var plans = new[] { Plan(equalized.Contract, new DateOnly(2014, 1, 1), 30.00m) };

        var plan = ImportPlanner.Plan(
            equalized.PropaneProfile(), Read(SyntheticWorkbook.Propane), new ImportContext(equalized.Contract, [equalized.Gas], [], null, null, plans));
        var regular = ImportPlanner.Plan(_f.PropaneProfile(), Read(SyntheticWorkbook.Propane), _f.Context(_f.Gas));

        Assert.All(plan.Rows, r => Assert.Equal(BillKind.Equalized, r.Bill!.Kind));
        Assert.All(regular.Rows, r => Assert.Equal(BillKind.Regular, r.Bill!.Kind));
        Assert.Equal(new ImportCounts(2, 2, 0, 0), plan.Counts);
    }

    [Fact]
    public void A_contract_that_became_equalized_imports_the_earlier_rows_as_regular_bills()
    {
        // Rows: 01.2014 and 02.2014. The plan starts on 1 February 2014: January was billed by consumption (Regular),
        // February by the plan (Equalized). The contract's current mode (Equalized) does not decide.
        var f = new ImportFixtures(BillingMode.Equalized);
        var plans = new[] { Plan(f.Contract, new DateOnly(2014, 2, 1), 30.00m) };

        var plan = ImportPlanner.Plan(
            f.PropaneProfile(), Read(SyntheticWorkbook.Propane), new ImportContext(f.Contract, [f.Gas], [], null, null, plans));

        Assert.Equal(
            [(new YearMonth(2014, 1), BillKind.Regular), (new YearMonth(2014, 2), BillKind.Equalized)],
            plan.Rows.Select(r => (r.Bill!.Period, r.Bill.Kind)));
    }

    [Fact]
    public void An_equalized_contract_without_plans_imports_regular_bills()
    {
        var f = new ImportFixtures(BillingMode.Equalized);

        var plan = ImportPlanner.Plan(f.PropaneProfile(), Read(SyntheticWorkbook.Propane), f.Context(f.Gas));

        Assert.All(plan.Rows, r => Assert.Equal(BillKind.Regular, r.Bill!.Kind));
    }

    [Fact]
    public void Bills_carry_the_contract_property_supplier_and_category()
    {
        var plan = ImportPlanner.Plan(_f.PropaneProfile(), Read(SyntheticWorkbook.Propane), _f.Context(_f.Gas));

        var bill = plan.Rows[0].Bill!;
        Assert.Equal((_f.Contract.PropertyId, _f.Contract.SupplierId, _f.Contract.CategoryId, _f.Contract.Id), (bill.PropertyId, bill.SupplierId, bill.CategoryId, bill.ContractId));
        Assert.False(bill.IsDraft);
    }

    [Fact]
    public void Notes_columns_are_joined_with_the_header_as_prefix()
    {
        var profile = _f.WaterProfile();
        profile.NotesColumns = [6, 1];
        var sheet = Read(SyntheticWorkbook.Water);

        var plan = ImportPlanner.Plan(profile, sheet, _f.Context(_f.Cold, _f.Hot));

        Assert.Equal("Comment: tariff changed; Month: 12.2013", Row(plan, 3).Bill!.Notes);
        Assert.Equal("Month: 01.11.2013", Row(plan, 2).Bill!.Notes);
    }

    [Fact]
    public void Notes_of_a_row_without_a_bill_go_to_its_first_reading()
    {
        var profile = _f.WaterProfile();
        profile.NotesColumns = [1];

        var plan = ImportPlanner.Plan(profile, Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.Equal("Month: Feb 2014", Row(plan, 5).Readings[0].Reading.Note);
    }

    [Fact]
    public void Quantity_on_bill_is_stored()
    {
        var sheet = new SheetData("S", [new SheetRow(1, []), Cells(2, SheetCell.Date(new DateOnly(2014, 5, 1)), SheetCell.Number(10m), SheetCell.String("3,5"))], 3);
        var profile = new ImportProfile { ContractId = _f.Contract.Id, PeriodColumn = 1, TotalColumn = 2, QuantityColumn = 3 };

        var plan = ImportPlanner.Plan(profile, sheet, _f.Context());

        Assert.Equal(3.5m, Row(plan, 2).Bill!.ConsumptionOnBill);
    }

    [Fact]
    public void An_unreadable_or_empty_period_is_an_error()
    {
        var sheet = new SheetData(
            "S",
            [
                new SheetRow(1, []),
                Cells(2, SheetCell.String("someday"), SheetCell.Number(10m)),
                Cells(3, SheetCell.Empty, SheetCell.Number(11m)),
                Cells(4, SheetCell.Empty, SheetCell.Empty),
                Cells(5, SheetCell.Date(new DateOnly(2014, 5, 1)), SheetCell.Number(12m)),
            ],
            2);
        var profile = new ImportProfile { ContractId = _f.Contract.Id, PeriodColumn = 1, TotalColumn = 2, LastDataRow = 5 };

        var plan = ImportPlanner.Plan(profile, sheet, _f.Context());

        Assert.True(Has(Row(plan, 2), ImportIssueCodes.PeriodInvalid, ImportIssueSeverity.Error));
        Assert.True(Has(Row(plan, 3), ImportIssueCodes.PeriodMissing, ImportIssueSeverity.Error));
        Assert.DoesNotContain(plan.Rows, r => r.RowNumber == 4);
        Assert.NotNull(Row(plan, 5).Bill);
        Assert.Equal(new ImportCounts(0, 1, 0, 2), plan.Counts);
    }

    [Fact]
    public void Without_a_last_row_the_import_stops_at_the_first_empty_period()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.DoesNotContain(plan.Rows, r => r.RowNumber == 8);
    }

    [Fact]
    public void A_last_row_reads_past_empty_rows_and_stops_there()
    {
        var profile = _f.WaterProfile();
        profile.LastDataRow = 8;

        var plan = ImportPlanner.Plan(profile, Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.Contains(plan.Rows, r => r.RowNumber == 8);
        Assert.DoesNotContain(plan.Rows, r => r.RowNumber == 7);
    }

    [Fact]
    public void A_row_with_nothing_to_import_is_skipped_with_a_warning()
    {
        var sheet = new SheetData("S", [new SheetRow(1, []), Cells(2, SheetCell.Date(new DateOnly(2014, 5, 1)), SheetCell.Empty)], 2);
        var profile = new ImportProfile { ContractId = _f.Contract.Id, PeriodColumn = 1, TotalColumn = 2, LastDataRow = 2 };

        var plan = ImportPlanner.Plan(profile, sheet, _f.Context());

        Assert.True(Has(Row(plan, 2), ImportIssueCodes.NothingToImport, ImportIssueSeverity.Warning));
        Assert.Equal(1, plan.Counts.SkippedRows);
    }

    [Fact]
    public void A_paid_marker_without_a_total_is_a_warning_and_no_payment()
    {
        var sheet = new SheetData("S", [new SheetRow(1, []), Cells(2, SheetCell.Date(new DateOnly(2014, 5, 1)), SheetCell.Empty, SheetCell.String("***"))], 3);
        var profile = new ImportProfile { ContractId = _f.Contract.Id, PeriodColumn = 1, TotalColumn = 2, PaidColumn = 3, LastDataRow = 2 };

        var plan = ImportPlanner.Plan(profile, sheet, _f.Context());

        Assert.True(Has(Row(plan, 2), ImportIssueCodes.PaidWithoutBill, ImportIssueSeverity.Warning));
        Assert.Null(Row(plan, 2).Payment);
    }

    [Fact]
    public void An_unreadable_paid_date_falls_back_to_the_first_of_the_month_with_a_warning()
    {
        var sheet = new SheetData("S", [new SheetRow(1, []), Cells(2, SheetCell.Date(new DateOnly(2014, 5, 1)), SheetCell.Number(10m), SheetCell.String("***"), SheetCell.String("tomorrow"))], 4);
        var profile = new ImportProfile { ContractId = _f.Contract.Id, PeriodColumn = 1, TotalColumn = 2, PaidColumn = 3, PaidDateColumn = 4 };

        var plan = ImportPlanner.Plan(profile, sheet, _f.Context());

        Assert.Equal(new DateOnly(2014, 5, 1), Row(plan, 2).Payment!.PaidOn);
        Assert.True(Has(Row(plan, 2), ImportIssueCodes.PaidDateInvalid, ImportIssueSeverity.Warning));
    }

    [Fact]
    public void A_profile_without_a_period_column_gives_a_problem_and_no_rows()
    {
        var profile = _f.WaterProfile();
        profile.PeriodColumn = 0;

        var plan = ImportPlanner.Plan(profile, Read(SyntheticWorkbook.Water), _f.Context(_f.Cold, _f.Hot));

        Assert.Equal(ErrorCodes.ImportPeriodColumnRequired, Assert.Single(plan.Problems).Code);
        Assert.Empty(plan.Rows);
    }

    [Fact]
    public void A_mapped_register_that_is_not_among_the_meters_gives_a_problem()
    {
        var plan = ImportPlanner.Plan(_f.WaterProfile(), Read(SyntheticWorkbook.Water), _f.Context(_f.Cold));

        Assert.Equal(ErrorCodes.ImportRegisterMissing, Assert.Single(plan.Problems).Code);
    }

    [Fact]
    public void Validator_flags_missing_pieces()
    {
        var profile = new ImportProfile { HeaderRow = 3, FirstDataRow = 2 };

        var codes = ImportProfileValidator.Validate(profile).Errors.Select(e => e.Code).ToList();

        Assert.Contains(ErrorCodes.ImportContractRequired, codes);
        Assert.Contains(ErrorCodes.ImportPeriodColumnRequired, codes);
        Assert.Contains(ErrorCodes.ImportRowsInvalid, codes);
        Assert.Contains(ErrorCodes.ImportNothingMapped, codes);
    }

    [Fact]
    public void Validator_requires_marker_text_for_a_paid_column()
    {
        var profile = _f.WaterProfile();
        profile.PaidMarker = " ";

        Assert.Equal(ErrorCodes.ImportPaidMarkerRequired, Assert.Single(ImportProfileValidator.Validate(profile).Errors).Code);
    }

    [Fact]
    public void Planning_a_thousand_rows_is_quick_and_complete()
    {
        var rows = new List<SheetRow> { new(1, []) };
        for (var i = 0; i < 1000; i++)
        {
            rows.Add(Cells(i + 2, SheetCell.Date(new DateOnly(1930, 1, 1).AddMonths(i)), SheetCell.Number(i), SheetCell.Number(10m + i)));
        }

        var profile = new ImportProfile
        {
            ContractId = _f.Contract.Id,
            PeriodColumn = 1,
            Registers = [new RegisterColumnMapping { RegisterId = _f.PowerRegister.Id, ReadingColumn = 2 }],
            TotalColumn = 3,
        };
        _f.Power.InstalledOn = new DateOnly(1900, 1, 1);

        var plan = ImportPlanner.Plan(profile, new SheetData("Big", rows, 3), _f.Context(_f.Power));

        Assert.Equal(new ImportCounts(1000, 1000, 0, 0), plan.Counts);
    }

    private static SheetRow Cells(int number, params SheetCell[] cells) => new(number, cells);
}
