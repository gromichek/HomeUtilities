using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.Core.Payments;
using HomeUtilities.Core.Taxes;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.Data.Sample;
using HomeUtilities.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests.Sample;

public class SampleDataGeneratorTests
{
    private sealed class Rig : IDisposable
    {
        private readonly TestDb _test = new();

        public Rig()
        {
            using var db = _test.NewContext();
            DataSeeder.SeedAsync(db, CancellationToken.None).GetAwaiter().GetResult();
            Properties = new PropertyStore(_test.Factory);
            Suppliers = new SupplierStore(_test.Factory);
            Contracts = new ContractStore(_test.Factory);
            Meters = new MeterStore(_test.Factory);
            Tariffs = new TariffStore(_test.Factory);
            VatRates = new VatRateStore(_test.Factory);
            Bills = new BillStore(_test.Factory);
            Payments = new PaymentStore(_test.Factory);
            Generator = new SampleDataGenerator(Properties, Suppliers, new CategoryStore(_test.Factory), Contracts, Meters, Tariffs, new EqualizedPlanStore(_test.Factory), Bills, Payments, new ChargeInputLoader(_test.Factory), _test.Clock);
            Loader = new ChargeInputLoader(_test.Factory);
        }

        public TariffStore Tariffs { get; }

        public BillStore Bills { get; }

        public PaymentStore Payments { get; }

        public VatRateStore VatRates { get; }

        public ChargeInputLoader Loader { get; }

        public PropertyStore Properties { get; }

        public SupplierStore Suppliers { get; }

        public ContractStore Contracts { get; }

        public MeterStore Meters { get; }

        public SampleDataGenerator Generator { get; }

        public FakeClock Clock => _test.Clock;

        public Microsoft.EntityFrameworkCore.IDbContextFactory<HomeUtilitiesDbContext> Factory => _test.Factory;

        public HomeUtilitiesDbContext NewContext() => _test.NewContext();

        public void Dispose() => _test.Dispose();
    }

    [Fact]
    public async Task Fill_on_an_empty_database_creates_the_expected_data()
    {
        using var rig = new Rig();

        Assert.True(await rig.Generator.FillAsync());

        var properties = await rig.Properties.ListAsync(true);
        Assert.Equal(["Flat in town", "Cottage", "Garage"], properties.Select(p => p.Name));
        var flat = properties[0];
        Assert.Equal(52.3m, flat.GetParameter(PropertyParameterKind.TotalArea, rig.Clock.Today));
        Assert.Equal(3m, flat.GetParameter(PropertyParameterKind.DeclaredResidents, rig.Clock.Today));
        Assert.Equal(2m, flat.GetParameter(PropertyParameterKind.DeclaredResidents, rig.Clock.Today.AddMonths(-7)));

        var contracts = await rig.Contracts.ListAsync();
        Assert.Equal(10, contracts.Count);
        Assert.Equal([6, 2, 2], properties.Select(p => contracts.Count(c => c.PropertyId == p.Id)));
        Assert.Contains(contracts, c => c.Service == ServiceKind.InternetTv && c.BillingTiming == HomeUtilities.Core.Billing.BillingTiming.InAdvance);
        Assert.Contains(contracts, c => c is { ReadingDayFrom: 25, ReadingDayTo: 28 });
        Assert.Contains(contracts, c => c is { ReadingDayFrom: 28, ReadingDayTo: 3 });
    }

    [Fact]
    public async Task Everything_passes_the_validators_and_uses_fictitious_payment_data()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();

        foreach (var property in await rig.Properties.ListAsync(true))
        {
            Assert.True(PropertyValidator.Validate(property).IsValid, property.Name);
        }

        var suppliers = await rig.Suppliers.ListAsync();
        Assert.Equal(7, suppliers.Count);
        foreach (var supplier in suppliers)
        {
            Assert.True(SupplierValidator.Validate(supplier).IsValid, supplier.Name);
            Assert.True(Iban.IsValid(supplier.PaymentDetails!.Iban!), supplier.Name);
            Assert.All(supplier.Links, l => Assert.EndsWith(".example.com", l.TryGetUri()!.Host.Split('/')[0]));
            Assert.Contains(supplier.Links, l => l.Kind == LinkKind.SubmitReadings);
        }

        foreach (var contract in await rig.Contracts.ListAsync())
        {
            Assert.True(ContractValidator.Validate(contract).IsValid, contract.Title);
        }

        var meters = await AllMetersAsync(rig);
        foreach (var meter in meters)
        {
            Assert.True(MeterValidator.Validate(meter).IsValid, meter.Location);
            foreach (var register in meter.Registers)
            {
                foreach (var reading in register.Readings)
                {
                    Assert.True(MeterReadingValidator.Validate(reading, register, meter).IsValid, $"{meter.Location} {reading.Date}");
                }
            }
        }

        await using var db = rig.NewContext();
        foreach (var link in await db.ContractMeters.ToListAsync())
        {
            Assert.True(ContractMeterValidator.Validate(link).IsValid);
        }
    }

    [Fact]
    public async Task The_flat_has_four_water_meters_in_use_and_one_replacement_chain()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];

        var meters = await rig.Meters.ListByPropertyAsync(flat.Id);
        var water = meters.Where(m => m.Resource is MeterResource.ColdWater or MeterResource.HotWater).ToList();
        Assert.Equal(4, water.Count(m => m.RemovedOn is null));
        Assert.Equal(5, water.Count);
        Assert.Equal(2, water.Count(m => m.Resource == MeterResource.HotWater));
        var replaced = Assert.Single(water, m => m.RemovedOn is not null);
        var successor = Assert.Single(water, m => m.ReplacedMeterId == replaced.Id);
        Assert.Equal(replaced.RemovedOn, successor.InstalledOn);
        Assert.Equal(ReadingKind.Final, replaced.Registers[0].Readings.Last().Kind);
        Assert.Equal(ReadingKind.Initial, successor.Registers[0].Readings.First().Kind);
    }

    [Fact]
    public async Task Hot_water_meters_are_linked_to_water_and_heating_contracts()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var contracts = await rig.Contracts.ListByPropertyAsync(flat.Id);
        var water = contracts.Single(c => c.Service == ServiceKind.ColdWater);
        var heat = contracts.Single(c => c.Service == ServiceKind.Heating);
        var hot = (await rig.Meters.ListByPropertyAsync(flat.Id)).Where(m => m.Resource == MeterResource.HotWater && m.RemovedOn is null).Select(m => m.Id).ToList();

        var waterLinks = (await rig.Contracts.ListContractMetersAsync(water.Id)).Select(l => l.MeterId).ToList();
        var heatLinks = (await rig.Contracts.ListContractMetersAsync(heat.Id)).Select(l => l.MeterId).ToList();

        Assert.Equal(2, hot.Count);
        Assert.All(hot, id => Assert.Contains(id, waterLinks));
        Assert.Equal(hot.Order(), heatLinks.Order());
    }

    [Fact]
    public async Task Consumption_over_the_replaced_meter_chain_adds_up_both_meters()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var meters = await rig.Meters.ListByPropertyAsync(flat.Id);
        var replaced = meters.Single(m => m.ReplacedMeterId is null && m.RemovedOn is not null);
        var successor = meters.Single(m => m.ReplacedMeterId == replaced.Id);
        var chain = MeterChain.Build(meters, successor.Id);
        var from = replaced.Registers[0].Readings.First().Date;
        var to = successor.Registers[0].Readings.Last().Date;

        var result = ConsumptionCalculator.Calculate(chain, RegisterKind.Total, from, to);

        var oldPart = replaced.Registers[0].Readings.Last().Value - replaced.Registers[0].Readings.First().Value;
        var newPart = successor.Registers[0].Readings.Last().Value - successor.Registers[0].Readings.First().Value;
        Assert.True(result.IsComplete);
        Assert.Equal(oldPart + newPart, result.Quantity);
        Assert.True(result.Quantity > 20m);
    }

    [Fact]
    public async Task One_meter_rolls_over_and_consumption_stays_positive()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var meters = await rig.Meters.ListByPropertyAsync(flat.Id);
        var power = meters.Single(m => m.Resource == MeterResource.Electricity);
        var values = power.Registers[0].Readings.OrderBy(r => r.Date).Select(r => r.Value).ToList();
        Assert.Equal(5, power.Digits);
        Assert.Contains(values.Zip(values.Skip(1)), p => p.Second < p.First);

        var chain = MeterChain.Build(meters, power.Id);
        var readings = power.Registers[0].Readings.OrderBy(r => r.Date).ToList();
        var result = ConsumptionCalculator.Calculate(chain, RegisterKind.Total, readings[0].Date, readings[^1].Date);

        Assert.InRange(result.Quantity, 1_000m, 2_000m);
        Assert.True(result.IsComplete);
    }

    [Fact]
    public async Task One_meter_has_an_overdue_verification_and_the_cottage_meter_has_an_export_register()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var properties = await rig.Properties.ListAsync(true);
        var all = await AllMetersAsync(rig);

        Assert.Equal(1, all.Count(m => MeterVerification.GetStatus(m, rig.Clock.Today) == VerificationStatus.Overdue));
        var cottage = Assert.Single(all, m => m.PropertyId == properties[1].Id);
        Assert.Equal([RegisterKind.Day, RegisterKind.Night, RegisterKind.Export], cottage.Registers.Select(r => r.Kind));
        Assert.Equal(EnergyDirection.Export, cottage.Registers[2].Direction);
        Assert.DoesNotContain(all, m => m.PropertyId == properties[2].Id);
    }

    [Fact]
    public async Task Readings_cover_thirteen_months_with_submitted_and_estimated_ones()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var meters = await rig.Meters.ListByPropertyAsync(flat.Id);
        var kitchen = meters.Single(m => m.Location == "Kitchen cold");
        var readings = kitchen.Registers[0].Readings.OrderBy(r => r.Date).ToList();

        Assert.Equal(HomeUtilities.Data.Sample.SampleDataGenerator.MonthsOfReadings, readings.Count);
        Assert.All(readings, r => Assert.True(r.Date <= rig.Clock.Today));
        Assert.Single(readings, r => r.Kind == ReadingKind.Estimated);
        Assert.Contains(readings, r => r.SubmittedOn is not null);
        Assert.Contains(readings, r => r.SubmittedOn is null);
        Assert.Equal(readings.Select(r => r.Value).Order(), readings.Select(r => r.Value));
    }

    [Fact]
    public async Task Tariffs_cover_every_basis_pass_the_validator_and_use_catalog_codes()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();

        var components = await rig.Tariffs.ListAsync();
        var catalog = await rig.VatRates.ListAsync();

        Assert.Equal(15, components.Count);
        foreach (var component in components)
        {
            Assert.True(TariffComponentValidator.Validate(component).IsValid, component.Name);
            Assert.Contains(catalog, v => v.Code == component.VatCode);
        }

        Assert.Equal(Enum.GetValues<ChargeBasis>(), components.Select(c => c.Basis).Distinct().Order());
    }

    [Fact]
    public async Task Water_has_two_prices_with_a_change_three_months_ago_and_heating_counts_only_hot_meters()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var contracts = await rig.Contracts.ListByPropertyAsync(flat.Id);
        var water = await rig.Tariffs.ListByContractAsync(contracts.Single(c => c.Service == ServiceKind.ColdWater).Id);
        var heat = await rig.Tariffs.ListByContractAsync(contracts.Single(c => c.Service == ServiceKind.Heating).Id);

        var supply = water.Single(c => c.Name == "Water supply");
        Assert.Equal([1.32m, 1.41m], supply.Rates.Select(r => r.Price));
        var changeMonth = YearMonth.From(rig.Clock.Today).AddMonths(-3);
        Assert.Equal(new DateOnly(changeMonth.Year, changeMonth.Month, 1), supply.Rates[1].ValidFrom);
        Assert.Equal(1.41m, supply.RateAt(rig.Clock.Today)!.Price);
        Assert.Equal(0.86m, water.Single(c => c.Name == "Sewerage").Rates.Single().Price);

        var hotIds = (await rig.Meters.ListByPropertyAsync(flat.Id))
            .Where(m => m.Resource == MeterResource.HotWater).Select(m => m.Id).Order().ToList();
        var heating = heat.Single(c => c.Name == "Hot water");
        Assert.Equal(hotIds, heating.MeterIds.Order());
        Assert.Equal("LV-REDUCED", heating.VatCode);
        Assert.Equal(3.20m, heating.Rates.Single().Price);

        var heatShare = heat.Single(c => c.Name == "Heat for heating/circulation");
        Assert.Equal((ChargeBasis.QuantityFromBill, MeterUnit.MWh, 70m), (heatShare.Basis, heatShare.QuantityUnit, heatShare.Rates.Single().Price));
        var difference = heat.Single(c => c.Name == "Hot water difference");
        Assert.Equal((ChargeBasis.QuantityFromBill, MeterUnit.M3, 3.20m), (difference.Basis, difference.QuantityUnit, difference.Rates.Single().Price));
    }

    [Fact]
    public async Task Electricity_has_a_stepped_price_and_the_prices_of_the_other_contracts_match_the_task()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var all = await rig.Tariffs.ListAsync();

        var energy = all.Single(c => c.Name == "Energy");
        var rate = energy.Rates.Single();
        Assert.Equal([new TariffTier(100m, 0.1065m), new TariffTier(null, 0.0818m)], rate.Tiers);
        Assert.Equal(TierPeriod.Month, rate.TierPeriod);

        var maintenance = all.Single(c => c.Name == "Maintenance").Rates.Single();
        Assert.Equal((0.6711m, true), (maintenance.Price, maintenance.PriceIncludesVat));
        var internet = all.Single(c => c.Name == "Internet and TV").Rates.Single();
        Assert.Equal((22.95m, true), (internet.Price, internet.PriceIncludesVat));
        Assert.Equal(ChargeBasis.PerResident, all.Single(c => c.Name == "Waste").Basis);

        Assert.Equal([RegisterKind.Day], all.Single(c => c.Name == "Energy, day").RegisterKinds);
        Assert.Equal([RegisterKind.Export], all.Single(c => c.Name == "Solar export").RegisterKinds);
    }

    [Fact]
    public async Task Charges_of_the_sample_contracts_have_no_tariff_warnings_except_the_price_that_starts_next_month()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var period = YearMonth.From(rig.Clock.Today).AddMonths(-2);
        var forbidden = new[]
        {
            ChargeWarningCodes.UnknownVat, ChargeWarningCodes.InvalidTariff,
            ChargeWarningCodes.CurrencyMismatch, ChargeWarningCodes.MissingParameter,
        };
        var container = (await rig.Tariffs.ListAsync()).Single(c => c.Name == "Container rental");

        foreach (var contract in await rig.Contracts.ListAsync())
        {
            if (await rig.Tariffs.ListByContractAsync(contract.Id) is not { Count: > 0 })
            {
                continue;
            }

            var charge = ChargeCalculator.Calculate((await rig.Loader.LoadAsync(contract.Id, period))!);

            Assert.DoesNotContain(charge.Warnings, w => forbidden.Contains(w.Code));
            Assert.DoesNotContain(charge.Warnings, w => w.Code == ChargeWarningCodes.NoRateForDate && w.ComponentId != container.Id);
            Assert.True(charge.Lines.Count > 0, contract.Title);
        }
    }

    [Fact]
    public async Task Charges_of_the_last_three_months_are_calculated_for_every_contract_without_exceptions_and_show_every_case()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var thisMonth = YearMonth.From(rig.Clock.Today);
        var container = (await rig.Tariffs.ListAsync()).Single(c => c.Name == "Container rental");
        var charges = new List<CalculatedCharge>();

        foreach (var contract in await rig.Contracts.ListAsync())
        {
            for (var back = 1; back <= 3; back++)
            {
                var input = await rig.Loader.LoadAsync(contract.Id, thisMonth.AddMonths(-back));
                Assert.NotNull(input);
                charges.Add(ChargeCalculator.Calculate(input));
            }
        }

        Assert.Equal(30, charges.Count);
        Assert.Contains(charges, c => c.Lines.Count > 0 && c.IsComplete && c.Warnings.Count == 0);
        Assert.Contains(charges, c => c.Lines.Count(l => l.ComponentName == "Energy") > 1);
        Assert.Contains(charges, c => c.Lines.Any(l => l.IsFromBill));
        Assert.Contains(charges, c => c.Warnings.Any(w => w.Code == ChargeWarningCodes.NoRateForDate && w.ComponentId == container.Id));
    }

    [Fact]
    public async Task The_replaced_meter_shows_two_segments_in_the_replacement_month()
    {
        using var rig = new Rig();
        await rig.Generator.FillAsync();
        var flat = (await rig.Properties.ListAsync(true))[0];
        var water = (await rig.Contracts.ListByPropertyAsync(flat.Id)).Single(c => c.Service == ServiceKind.ColdWater);
        var replacementMonth = YearMonth.From(rig.Clock.Today).AddMonths(-5);

        // the replacement is on the 15th of the month five months ago; the reading window (25–28) of that month
        // and the next one end on the new meter, so one of the two months spans both meters
        var spans = new List<int>();
        foreach (var month in new[] { replacementMonth, replacementMonth.AddMonths(1) })
        {
            var charge = ChargeCalculator.Calculate((await rig.Loader.LoadAsync(water.Id, month))!);
            spans.Add(charge.Lines.SelectMany(l => l.Consumption).Max(c => c.Segments.Count));
        }

        Assert.Contains(2, spans);
    }

    [Fact]
    public async Task Fill_is_refused_when_a_property_exists_and_changes_nothing()
    {
        using var rig = new Rig();
        await rig.Properties.SaveAsync(new Property { Name = "Mine", Kind = PropertyKind.House });

        Assert.False(await rig.Generator.FillAsync());

        Assert.Equal(["Mine"], (await rig.Properties.ListAsync(true)).Select(p => p.Name));
        Assert.Empty(await rig.Suppliers.ListAsync());
    }

    [Fact]
    public async Task Output_is_deterministic_for_a_fixed_today()
    {
        string[] Snapshot(IEnumerable<Meter> meters) =>
            [.. meters.OrderBy(m => m.Location).SelectMany(m => m.Registers.OrderBy(r => r.Kind)
                .SelectMany(r => r.Readings.OrderBy(x => x.Date).Select(x => $"{m.Location}|{r.Kind}|{x.Date:O}|{x.Value}|{x.Kind}")))];

        using var first = new Rig();
        using var second = new Rig();
        await first.Generator.FillAsync();
        await second.Generator.FillAsync();

        var a = Snapshot(await AllMetersAsync(first));
        var b = Snapshot(await AllMetersAsync(second));
        Assert.NotEmpty(a);
        Assert.Equal(a, b);
    }

    // ---- Bills ----------------------------------------------------------------------------------------------------

    private static async Task<(List<Bill> Bills, List<Payment> Payments, Dictionary<Guid, Contract> Contracts)> LoadBillsAsync(Rig rig)
    {
        await rig.Generator.FillAsync();
        var bills = (await rig.Bills.ListAsync()).ToList();
        var payments = (await rig.Payments.ListAsync()).ToList();
        var contracts = (await rig.Contracts.ListAsync()).ToDictionary(c => c.Id);
        return (bills, payments, contracts);
    }

    [Fact]
    public async Task Bills_a_year_of_the_flat_plus_a_one_off_expense_all_valid()
    {
        using var rig = new Rig();
        var (bills, payments, contracts) = await LoadBillsAsync(rig);

        // 5 in-arrears contracts x 12 months + 12 internet bills (10 months back to next month) + 1 repair + 1 settlement
        // + the cottage electricity (12 equalized + 1 settlement) + the taxes (4 garage + 2 land installments)
        Assert.Equal(5 * 12 + 12 + 1 + 1 + (12 + 1) + (4 + 2), bills.Count);
        foreach (var bill in bills)
        {
            var contract = bill.ContractId is { } id ? contracts[id] : null;
            var result = BillValidator.Validate(bill, contract);
            Assert.True(result.IsValid, $"{bill.Number}: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }

        var byId = bills.ToDictionary(b => b.Id);
        foreach (var payment in payments)
        {
            var result = PaymentValidator.Validate(payment, byId);
            Assert.True(result.IsValid, $"{payment.Reference}: {string.Join(", ", result.Errors.Select(e => e.Code))}");
        }

        var oneOff = Assert.Single(bills, b => b.Kind == BillKind.OneOff);
        Assert.Equal(("Boiler repair", null), (oneOff.Title, oneOff.ContractId));
    }

    [Fact]
    public async Task Bills_cover_every_status_paid_partially_paid_overdue_unpaid_and_prepaid()
    {
        using var rig = new Rig();
        var (bills, payments, _) = await LoadBillsAsync(rig);
        var today = rig.Clock.Today;

        var states = bills.Select(b => (Bill: b, State: BillStatus.Of(b, payments, today))).ToList();

        Assert.Contains(states, s => s.State.IsPaid && !s.State.IsPrepaid);
        var prepaid = Assert.Single(states, s => s.State.IsPrepaid);
        Assert.Equal(YearMonth.From(today).AddMonths(1), prepaid.Bill.Period);
        var partial = Assert.Single(states, s => s.State.Paid > 0 && s.State.Open > 0);
        Assert.False(partial.State.IsOverdue);
        Assert.Contains(states, s => s.State.IsOverdue);
        Assert.Contains(states, s => s.State.Paid == 0 && !s.State.IsOverdue && !s.State.IsPaid);
        Assert.All(states.Where(s => s.State.Paid > s.Bill.Gross), s => Assert.Fail("A bill is overpaid."));
    }

    [Fact]
    public async Task Bills_are_mostly_paid_with_the_quick_action_one_payment_per_bill()
    {
        using var rig = new Rig();
        var (bills, payments, _) = await LoadBillsAsync(rig);

        var quick = payments.Where(p => p.CreatedByQuickAction).ToList();
        Assert.True(quick.Count > bills.Count / 2);
        Assert.All(quick, p =>
        {
            var allocation = Assert.Single(p.Allocations);
            Assert.Equal(p.Amount, allocation.Amount);
            Assert.Equal(PaymentMethod.BankTransfer, p.Method);
            Assert.False(string.IsNullOrWhiteSpace(p.Reference));
        });
        Assert.Contains(payments, p => !p.CreatedByQuickAction);
    }

    [Fact]
    public async Task Bills_water_has_the_quantity_on_the_bill_a_snapshot_and_differences_above_and_below_five_percent()
    {
        using var rig = new Rig();
        var (bills, _, contracts) = await LoadBillsAsync(rig);
        var water = bills.Where(b => b.ContractId is { } id && contracts[id].Service == ServiceKind.ColdWater).ToList();

        Assert.Equal(12, water.Count);
        Assert.All(water, b =>
        {
            Assert.NotNull(b.CalculatedSnapshot);
            Assert.NotNull(b.ConsumptionOnBill);
        });
        var tones = water
            .Select(b => BillComparison.ToneOf(BillComparison.Compare(b, b.CalculatedSnapshot, BillComparison.MeteredQuantity(b.CalculatedSnapshot)).DifferencePercent))
            .ToList();
        Assert.Contains(DifferenceTone.Higher, tones);
        Assert.Contains(DifferenceTone.Lower, tones);
        Assert.Contains(DifferenceTone.Neutral, tones);
    }

    [Fact]
    public async Task Bills_one_electricity_bill_has_lines_that_add_up_and_a_custom_field_value()
    {
        using var rig = new Rig();
        var (bills, _, contracts) = await LoadBillsAsync(rig);

        var detailed = Assert.Single(bills, b => b.Lines.Count > 0);

        Assert.Equal(ServiceKind.Electricity, contracts[detailed.ContractId!.Value].Service);
        Assert.Empty(BillChecks.Warnings(detailed));
        var field = Assert.Single(detailed.CustomFields);
        Assert.Equal(("Supplier reference", CustomFieldKind.Text), (field.Name, field.Kind));
        Assert.Equal(contracts[detailed.ContractId.Value].CustomFields.Single().Id, field.DefinitionId);
    }

    [Fact]
    public async Task Bills_of_the_heating_contract_carry_the_quantities_and_equal_the_complete_calculation()
    {
        using var rig = new Rig();
        var (bills, _, contracts) = await LoadBillsAsync(rig);

        var heating = bills.Where(b => b.ContractId is { } id && contracts[id].Service == ServiceKind.Heating).ToList();

        Assert.Equal(12, heating.Count);
        foreach (var bill in heating)
        {
            Assert.Equal(2, bill.BillQuantities.Count);
            var snapshot = bill.CalculatedSnapshot;
            Assert.NotNull(snapshot);
            Assert.True(snapshot.IsComplete);
            Assert.Equal(snapshot.TotalGross, bill.Gross);

            // The stored quantities reproduce the snapshot: the difference to the calculation is 0.00.
            var input = await rig.Loader.LoadAsync(bill.ContractId!.Value, bill.Period);
            var calculated = ChargeCalculator.Calculate(input!);
            Assert.True(calculated.IsComplete);
            Assert.Equal(0m, BillComparison.Compare(bill, calculated, null).Difference);
        }
    }

    [Fact]
    public async Task Exactly_one_heating_bill_carries_a_line_amount_from_the_bill_that_differs_by_cents_and_still_equals_the_calculation()
    {
        using var rig = new Rig();
        var (bills, _, contracts) = await LoadBillsAsync(rig);

        var heating = bills.Where(b => b.ContractId is { } id && contracts[id].Service == ServiceKind.Heating).ToList();

        var withAmount = Assert.Single(heating, b => b.BillQuantities.Any(q => q.Amount is not null));
        var typed = Assert.Single(withAmount.BillQuantities, q => q.Amount is not null);
        var input = await rig.Loader.LoadAsync(withAmount.ContractId!.Value, withAmount.Period);
        Assert.Equal(typed.Amount, input!.AmountsFromBill![typed.ComponentId]);

        var calculated = ChargeCalculator.Calculate(input);
        Assert.True(calculated.IsComplete);
        Assert.DoesNotContain(calculated.Warnings, w => w.Code == ChargeWarningCodes.AmountMismatch);
        var line = Assert.Single(calculated.Lines, l => l.ComponentId == typed.ComponentId);
        Assert.True(line.AmountFromBill);

        // The amount is a few cents off quantity * price, yet the bill equals the calculation.
        var difference = Math.Abs(typed.Amount!.Value - MoneyMath.Round(line.Quantity!.Value * line.UnitPrice!.Value));
        Assert.InRange(difference, 0.01m, 0.05m);
        Assert.Equal(0m, BillComparison.Compare(withAmount, calculated, null).Difference);
        Assert.Equal(calculated.TotalGross, withAmount.Gross);
    }

    [Fact]
    public async Task Bills_the_internet_contract_is_paid_in_advance_and_due_before_its_month()
    {
        using var rig = new Rig();
        var (bills, _, contracts) = await LoadBillsAsync(rig);

        var internet = bills.Where(b => b.ContractId is { } id && contracts[id].Service == ServiceKind.InternetTv).OrderBy(b => b.Period).ToList();

        Assert.Equal(12, internet.Count);
        Assert.All(internet, b => Assert.True(b.DueOn <= b.Period.FirstDay));
        Assert.All(internet, b => Assert.Equal(22.95m, b.Gross));
    }

    // ---- Payments, prepayment, refund, balances (T-020) ---------------------------------------------------------------

    [Fact]
    public async Task Payments_one_transfer_without_contract_pays_the_bills_of_two_contracts_of_the_flat()
    {
        using var rig = new Rig();
        var (bills, payments, contracts) = await LoadBillsAsync(rig);
        var flat = (await rig.Properties.ListAsync(true))[0];

        var transfer = Assert.Single(payments, p => p.ContractId is null && p.Allocations.Count == 2);

        Assert.Equal(flat.Id, transfer.PropertyId);
        Assert.False(transfer.CreatedByQuickAction);
        Assert.Equal(2, transfer.Allocations.Count);
        var paid = transfer.Allocations.Select(a => bills.Single(b => b.Id == a.BillId)).ToList();
        Assert.Equal(
            new[] { ServiceKind.Maintenance, ServiceKind.Waste }.Order(),
            paid.Select(b => contracts[b.ContractId!.Value].Service).Order());
        Assert.Equal(paid.Sum(b => b.Gross), transfer.Amount);
        Assert.Equal(0m, PaymentRules.Unallocated(transfer));
        Assert.All(paid, b => Assert.True(BillStatus.Of(b, payments, rig.Clock.Today).IsPaid));
        Assert.All(paid, b => Assert.Equal(flat.Id, b.PropertyId));
    }

    [Fact]
    public async Task Payments_the_internet_prepayment_is_not_allocated_and_the_bill_is_not_entered_yet()
    {
        using var rig = new Rig();
        var (bills, payments, contracts) = await LoadBillsAsync(rig);
        var internet = contracts.Values.Single(c => c.Service == ServiceKind.InternetTv);

        var prepayment = Assert.Single(payments, p => p.ContractId == internet.Id && p.Allocations.Count == 0);

        Assert.Equal(22.95m, prepayment.Amount);
        Assert.Equal(22.95m, PaymentRules.Unallocated(prepayment));
        Assert.StartsWith("Internet ", prepayment.Reference, StringComparison.Ordinal);
        var month = YearMonth.From(rig.Clock.Today).AddMonths(2);
        Assert.EndsWith(month.FirstDay.ToString("MM.yyyy", System.Globalization.CultureInfo.InvariantCulture), prepayment.Reference, StringComparison.Ordinal);
        Assert.DoesNotContain(bills, b => b.ContractId == internet.Id && b.Period >= month);
    }

    [Fact]
    public async Task Payments_the_refund_is_negative_and_settles_a_negative_settlement_bill()
    {
        using var rig = new Rig();
        var (bills, payments, contracts) = await LoadBillsAsync(rig);

        var settlement = Assert.Single(bills, b => b.Kind == BillKind.Settlement && b.Gross < 0);
        var refund = Assert.Single(payments, p => p.Amount < 0);

        Assert.Equal(-12.40m, settlement.Gross);
        Assert.Equal(ServiceKind.Maintenance, contracts[settlement.ContractId!.Value].Service);
        Assert.Equal(-12.40m, refund.Amount);
        Assert.Equal(refund.Amount, Assert.Single(refund.Allocations).Amount);
        Assert.Equal(settlement.Id, refund.Allocations[0].BillId);
        Assert.False(refund.CreatedByQuickAction);
        var state = BillStatus.Of(settlement, payments, rig.Clock.Today);
        Assert.True(state.IsPaid);
        Assert.Equal(0m, state.Open);
    }

    [Fact]
    public async Task Payments_one_bill_is_partly_paid_and_the_other_payments_are_quick_or_manual()
    {
        using var rig = new Rig();
        var (bills, payments, _) = await LoadBillsAsync(rig);

        var partial = Assert.Single(payments, p => p.Allocations.Count == 1 && p.Allocations[0].Amount < bills.Single(b => b.Id == p.Allocations[0].BillId).Gross);

        Assert.False(partial.CreatedByQuickAction);
        Assert.Equal(partial.Amount, partial.Allocations[0].Amount);
    }

    [Fact]
    public async Task Balances_every_contract_has_one_and_the_sample_has_a_debt_and_a_prepayment()
    {
        using var rig = new Rig();
        var (bills, payments, contracts) = await LoadBillsAsync(rig);

        var balances = contracts.Values
            .Where(c => c.BillingMode != BillingMode.Equalized)
            .Where(c => bills.Any(b => b.ContractId == c.Id) || payments.Any(p => p.ContractId == c.Id))
            .ToDictionary(c => c.Service, c => ContractBalance.Of(c.Id, bills, payments, rig.Clock.Today));

        // every balance adds up: paid - billed
        Assert.All(balances.Values, b => Assert.Equal(b.Paid - b.Billed, b.Value));
        Assert.Contains(balances.Values, b => b.Value < 0);
        Assert.Contains(balances.Values, b => b.Value > 0);

        // internet: all bills paid, plus the 22.95 prepayment
        Assert.Equal(22.95m, balances[ServiceKind.InternetTv].Value);
        // waste: three bills (the overdue one and ...) -> a debt, the latest one is paid by the transfer
        Assert.True(balances[ServiceKind.Waste].Value < 0);
        // heating and electricity: the latest bill is unpaid
        Assert.True(balances[ServiceKind.Heating].Value < 0);
        Assert.True(balances[ServiceKind.Electricity].Value < 0);
    }

    // ---- Equalized plans and property tax ---------------------------------------------------------------------------

    [Fact]
    public async Task Equalized_the_cottage_electricity_has_two_plan_versions_and_twelve_paid_monthly_bills()
    {
        using var rig = new Rig();
        var (bills, payments, contracts) = await LoadBillsAsync(rig);
        var contract = Assert.Single(contracts.Values, c => c.BillingMode == BillingMode.Equalized);
        var plans = await new EqualizedPlanStore(rig.Factory).ListByContractAsync(contract.Id);
        var current = YearMonth.From(rig.Clock.Today);

        Assert.Equal(2, plans.Count);
        Assert.True(EqualizedPlanValidator.ValidateSet(plans).IsValid);
        Assert.True(plans[0].MonthlyAmount < plans[1].MonthlyAmount);
        Assert.Null(plans[1].ValidTo);

        var monthly = bills.Where(b => b.ContractId == contract.Id && b.Kind == BillKind.Equalized).OrderBy(b => b.Period).ToList();
        Assert.Equal(Enumerable.Range(1, 12).Reverse().Select(ago => current.AddMonths(-ago)), monthly.Select(b => b.Period));
        Assert.All(monthly, b => Assert.Equal(EqualizedPlans.AmountFor(plans, b.Period), b.Gross));
        Assert.All(monthly, b => Assert.True(BillStatus.Of(b, payments, rig.Clock.Today).IsPaid, b.Number));
        Assert.All(monthly, b => Assert.True(BillValidator.Validate(b, contract).IsValid, b.Number));

        var settlement = Assert.Single(bills, b => b.ContractId == contract.Id && b.Kind == BillKind.Settlement);
        Assert.Equal(current.AddMonths(-13), settlement.Period);
        Assert.True(BillStatus.Of(settlement, payments, rig.Clock.Today).IsPaid);
    }

    [Fact]
    public async Task Equalized_the_settlement_covers_twelve_months_after_the_settlement_bill_and_expects_a_surcharge()
    {
        using var rig = new Rig();
        var (bills, _, contracts) = await LoadBillsAsync(rig);
        var contract = Assert.Single(contracts.Values, c => c.BillingMode == BillingMode.Equalized);
        var plans = await new EqualizedPlanStore(rig.Factory).ListByContractAsync(contract.Id);
        var current = YearMonth.From(rig.Clock.Today);
        var contractBills = bills.Where(b => b.ContractId == contract.Id).ToList();

        var (from, to) = EqualizedSettlement.Range(plans, contractBills, current);
        var charges = new Dictionary<YearMonth, HomeUtilities.Core.Charges.CalculatedCharge>();
        for (var month = from; month <= to; month = month.AddMonths(1))
        {
            charges[month] = ChargeCalculator.Calculate((await rig.Loader.LoadAsync(contract.Id, month))!);
        }

        var result = EqualizedSettlement.Calculate(plans, contractBills, charges, from, to);

        Assert.Equal((current.AddMonths(-12), current), (from, to));
        Assert.Equal(13, result.Months.Count);
        Assert.Equal(1, result.MissingMonths);
        Assert.False(result.Months[^1].IsComplete);
        Assert.True(result.Expected > 0, $"expected a surcharge, got {result.Expected}");
        Assert.Equal(result.TotalActual - result.TotalBilled, result.Expected);
    }

    [Fact]
    public async Task Taxes_the_garage_has_four_installments_with_the_default_dates_and_the_land_tax_one_overdue()
    {
        using var rig = new Rig();
        var (bills, payments, contracts) = await LoadBillsAsync(rig);
        var today = rig.Clock.Today;
        var garage = Assert.Single(contracts.Values, c => c.Service == ServiceKind.PropertyTaxBuilding);
        var land = Assert.Single(contracts.Values, c => c.Service == ServiceKind.PropertyTaxLand);

        var garageBills = TaxInstallmentPlanner.ForYear(bills, garage.Id, today.Year);
        Assert.Equal(TaxInstallmentPlanner.DefaultDueDates(today.Year, 4), garageBills.Select(b => b.DueOn!.Value));
        Assert.Equal(96.40m, garageBills.Sum(b => b.Gross));
        Assert.Equal([24.10m, 24.10m, 24.10m, 24.10m], garageBills.Select(b => b.Gross));
        Assert.All(garageBills, b => Assert.True(BillValidator.Validate(b, garage).IsValid, b.Title));
        Assert.Equal("Property tax 2026, installment 2 of 4", garageBills[1].Title);
        Assert.Equal([true, true, true, false], garageBills.Select(b => BillStatus.Of(b, payments, today).IsPaid));
        Assert.DoesNotContain(garageBills, b => BillStatus.Of(b, payments, today).IsOverdue);

        var landBills = TaxInstallmentPlanner.ForYear(bills, land.Id, today.Year);
        Assert.Equal(TaxInstallmentPlanner.DefaultDueDates(today.Year, 2), landBills.Select(b => b.DueOn!.Value));
        Assert.Equal(58.30m, landBills.Sum(b => b.Gross));
        Assert.True(BillStatus.Of(landBills[0], payments, today).IsPaid);
        Assert.True(BillStatus.Of(landBills[1], payments, today).IsOverdue);
    }

    private static async Task<List<Meter>> AllMetersAsync(Rig rig)
    {
        var result = new List<Meter>();
        foreach (var property in await rig.Properties.ListAsync(true))
        {
            result.AddRange(await rig.Meters.ListByPropertyAsync(property.Id));
        }

        return result;
    }
}
