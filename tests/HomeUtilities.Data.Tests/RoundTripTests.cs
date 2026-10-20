using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Consumption;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.Core.Tariffs;
using Microsoft.EntityFrameworkCore;

namespace HomeUtilities.Data.Tests;

public class RoundTripTests
{
    private static readonly PaymentDetails Details =
        new("Invented Water Ltd", "40000000000", "LV00TEST0000000000000", "TESTLV22", "Test Bank", "Water {period}");

    private static Property NewProperty() => new() { Name = "Cottage", Kind = PropertyKind.Cottage };

    private static Category NewCategory() => new() { Name = "Water", SystemKey = null };

    [Fact]
    public async Task Property_with_parameters()
    {
        using var test = new TestDb();
        var property = NewProperty();
        property.Address = "1 Test Street";
        property.Parameters.Add(new PropertyParameter
        {
            Kind = PropertyParameterKind.TotalArea,
            ValidFrom = new DateOnly(2020, 1, 1),
            Value = 87.35m,
        });
        property.Parameters.Add(new PropertyParameter
        {
            Kind = PropertyParameterKind.Custom,
            Name = "Garden",
            ValidFrom = new DateOnly(2021, 5, 1),
            Value = 12m,
        });
        await using (var db = test.NewContext())
        {
            db.Properties.Add(property);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.Properties.Include(p => p.Parameters).SingleAsync();
        Assert.Equal(property.Id, loaded.Id);
        Assert.Equal("1 Test Street", loaded.Address);
        Assert.Equal(PropertyKind.Cottage, loaded.Kind);
        Assert.Equal(2, loaded.Parameters.Count);
        Assert.Equal(87.35m, loaded.GetParameter(PropertyParameterKind.TotalArea, new DateOnly(2024, 1, 1)));
        Assert.Equal(12m, loaded.GetParameter("Garden", new DateOnly(2024, 1, 1)));
    }

    [Fact]
    public async Task Supplier_with_links_and_payment_details()
    {
        using var test = new TestDb();
        var supplier = new Supplier { Name = "Invented Water Ltd", PaymentDetails = Details };
        supplier.Links.Add(new Link { OwnerId = supplier.Id, Kind = LinkKind.CustomerPortal, Url = "https://example.test/portal", SortOrder = 1, Title = "Portal" });
        supplier.Links.Add(new Link { OwnerId = supplier.Id, Kind = LinkKind.Website, Url = "https://example.test", SortOrder = 2 });
        await using (var db = test.NewContext())
        {
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.Suppliers.SingleAsync();
        Assert.Equal(Details, loaded.PaymentDetails);
        Assert.Equal(2, loaded.Links.Count);
        Assert.Equal(supplier.Links[0].Id, loaded.Links[0].Id);
        Assert.Equal(LinkKind.CustomerPortal, loaded.Links[0].Kind);
        Assert.Equal("Portal", loaded.Links[0].Title);
        Assert.Equal(supplier.Links[0].CreatedAt, loaded.Links[0].CreatedAt);
    }

    [Fact]
    public async Task Supplier_without_payment_details_stays_null_and_empty_details_stay_empty()
    {
        using var test = new TestDb();
        var none = new Supplier { Name = "None" };
        var empty = new Supplier { Name = "Empty", PaymentDetails = PaymentDetails.Empty };
        await using (var db = test.NewContext())
        {
            db.Suppliers.AddRange(none, empty);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        Assert.Null((await read.Suppliers.SingleAsync(s => s.Name == "None")).PaymentDetails);
        Assert.Equal(PaymentDetails.Empty, (await read.Suppliers.SingleAsync(s => s.Name == "Empty")).PaymentDetails);
    }

    [Fact]
    public async Task Contract_with_all_owned_data()
    {
        using var test = new TestDb();
        var (property, supplier, category) = (NewProperty(), new Supplier { Name = "S" }, NewCategory());
        var contract = new Contract
        {
            PropertyId = property.Id,
            SupplierId = supplier.Id,
            CategoryId = category.Id,
            Service = ServiceKind.ColdWater,
            Title = "Water",
            CustomerNumber = "C-1",
            ValidFrom = new DateOnly(2020, 1, 1),
            ValidTo = new DateOnly(2030, 12, 31),
            Currency = Currency.LVL,
            BillingMode = BillingMode.Equalized,
            ReadingDayFrom = 25,
            ReadingDayTo = 3,
            BillDay = 10,
            PaymentDueDay = 20,
            BillingTiming = BillingTiming.InAdvance,
            PaymentDetails = Details,
        };
        contract.Links.Add(new Link { OwnerId = contract.Id, Kind = LinkKind.SubmitReadings, Url = "https://example.test/readings" });
        var definition = new CustomFieldDefinition
        {
            OwnerId = contract.Id,
            Name = "Losses m3",
            Kind = CustomFieldKind.Number,
            Target = CustomFieldTarget.Bill,
            SortOrder = 1,
        };
        contract.CustomFields.Add(definition);
        await using (var db = test.NewContext())
        {
            db.AddRange(property, supplier, category, contract);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.Contracts.SingleAsync();
        Assert.Equal(Currency.LVL, loaded.Currency);
        Assert.Equal(BillingTiming.InAdvance, loaded.BillingTiming);
        Assert.Equal(new DateOnly(2030, 12, 31), loaded.ValidTo);
        Assert.Equal((25, 3), (loaded.ReadingDayFrom, loaded.ReadingDayTo));
        Assert.Equal(Details, loaded.PaymentDetails);
        Assert.Equal("https://example.test/readings", Assert.Single(loaded.Links).Url);
        var field = Assert.Single(loaded.CustomFields);
        Assert.Equal(definition.Id, field.Id);
        Assert.Equal((CustomFieldKind.Number, CustomFieldTarget.Bill), (field.Kind, field.Target));
        Assert.Equal(new(2026, 10), YearMonth.From(new DateOnly(2026, 10, 5)));
    }

    [Fact]
    public async Task Meter_with_registers_readings_and_contract_link()
    {
        using var test = new TestDb();
        var (property, supplier, category) = (NewProperty(), new Supplier { Name = "S" }, NewCategory());
        var contract = new Contract { PropertyId = property.Id, SupplierId = supplier.Id, CategoryId = category.Id, Title = "Power" };
        var meter = new Meter
        {
            PropertyId = property.Id,
            Resource = MeterResource.Electricity,
            Unit = MeterUnit.KWh,
            Digits = 6,
            InstalledOn = new DateOnly(2019, 3, 1),
            VerificationIntervalMonths = 96,
        };
        var day = new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Day };
        var night = new MeterRegister { MeterId = meter.Id, Kind = RegisterKind.Night, Name = "Night" };
        day.Readings.Add(new MeterReading { RegisterId = day.Id, Date = new DateOnly(2026, 9, 30), Value = 12345.5m, Kind = ReadingKind.Regular, SubmittedOn = new DateOnly(2026, 10, 1) });
        day.Readings.Add(new MeterReading { RegisterId = day.Id, Date = new DateOnly(2026, 10, 31), Value = 12400m, Kind = ReadingKind.Estimated });
        meter.Registers.Add(day);
        meter.Registers.Add(night);
        var link = new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2019, 3, 1) };
        await using (var db = test.NewContext())
        {
            db.AddRange(property, supplier, category, contract, meter, link);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.Meters.Include(m => m.Registers).ThenInclude(r => r.Readings).SingleAsync();
        Assert.Equal(6, loaded.Digits);
        Assert.Equal(2, loaded.Registers.Count);
        var loadedDay = loaded.Registers.Single(r => r.Kind == RegisterKind.Day);
        Assert.Equal(2, loadedDay.Readings.Count);
        Assert.Contains(loadedDay.Readings, r => r.Value == 12345.5m && r.SubmittedOn == new DateOnly(2026, 10, 1));
        Assert.Empty(loaded.Registers.Single(r => r.Kind == RegisterKind.Night).Readings);
        var loadedLink = await read.ContractMeters.SingleAsync();
        Assert.Equal((contract.Id, meter.Id), (loadedLink.ContractId, loadedLink.MeterId));
    }

    [Fact]
    public async Task VatRate_with_versions()
    {
        using var test = new TestDb();
        var vat = new VatRate { Code = "LV-STANDARD", Name = "Standard" };
        vat.Versions.Add(new VatRateVersion { VatRateId = vat.Id, ValidFrom = new DateOnly(2012, 7, 1), Percent = 22m });
        vat.Versions.Add(new VatRateVersion { VatRateId = vat.Id, ValidFrom = new DateOnly(2025, 1, 1), Percent = 21m });
        await using (var db = test.NewContext())
        {
            db.VatRates.Add(vat);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.VatRates.Include(v => v.Versions).SingleAsync();
        Assert.Equal(22m, loaded.PercentAt(new DateOnly(2020, 1, 1)));
        Assert.Equal(21m, loaded.PercentAt(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public async Task TariffComponent_with_rates_and_tiers()
    {
        using var test = new TestDb();
        var (property, supplier, category) = (NewProperty(), new Supplier { Name = "S" }, NewCategory());
        var contract = new Contract { PropertyId = property.Id, SupplierId = supplier.Id, CategoryId = category.Id, Title = "Power" };
        var meterId = Guid.NewGuid();
        var component = new TariffComponent
        {
            ContractId = contract.Id,
            Name = "Energy",
            ChargeKind = ServiceKind.Electricity,
            Basis = ChargeBasis.PerUnit,
            RegisterKinds = [RegisterKind.Day, RegisterKind.Night],
            MeterIds = [meterId],
            VatCode = "LV-STANDARD",
        };
        component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = new DateOnly(2025, 1, 1), Price = 0.1856m, Currency = Currency.EUR });
        component.Rates.Add(new TariffRate
        {
            ComponentId = component.Id,
            ValidFrom = new DateOnly(2026, 1, 1),
            Price = 0m,
            PriceIncludesVat = true,
            TierPeriod = TierPeriod.Year,
            Tiers = [new TariffTier(100m, 1.41m), new TariffTier(null, 2.02m)],
        });
        await using (var db = test.NewContext())
        {
            db.AddRange(property, supplier, category, contract, component);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.TariffComponents.Include(c => c.Rates).SingleAsync();
        Assert.Equal([RegisterKind.Day, RegisterKind.Night], loaded.RegisterKinds);
        Assert.Equal([meterId], loaded.MeterIds);
        Assert.Equal(2, loaded.Rates.Count);
        var tiered = loaded.RateAt(new DateOnly(2026, 6, 1))!;
        Assert.Equal(TierPeriod.Year, tiered.TierPeriod);
        Assert.Equal([new TariffTier(100m, 1.41m), new TariffTier(null, 2.02m)], tiered.Tiers);
        Assert.Empty(loaded.RateAt(new DateOnly(2025, 6, 1))!.Tiers);
    }

    [Fact]
    public async Task Bill_with_lines_custom_fields_attachments_service_period_and_snapshot()
    {
        using var test = new TestDb();
        var (property, supplier, category) = (NewProperty(), new Supplier { Name = "S" }, NewCategory());
        var contract = new Contract { PropertyId = property.Id, SupplierId = supplier.Id, CategoryId = category.Id, Title = "Water" };
        var componentId = Guid.NewGuid();
        var consumption = new ConsumptionResult[] { null! };
        var snapshot = new CalculatedCharge(
            contract.Id,
            new YearMonth(2026, 9),
            Currency.EUR,
            [
                new ChargeLine(componentId, "Water", ServiceKind.ColdWater, category.Id, ChargeBasis.PerUnit, 5m, 1.41m, 7.05m, 21m, 1.48m, 8.53m, false, true, consumption),
            ],
            7.05m,
            1.48m,
            8.53m,
            true,
            [new ChargeWarning(ChargeWarningCodes.ContractInactive, componentId)]);
        var bill = new Bill
        {
            PropertyId = property.Id,
            CategoryId = category.Id,
            ContractId = contract.Id,
            SupplierId = supplier.Id,
            Period = new YearMonth(2026, 9),
            ServiceFrom = new DateOnly(2026, 8, 15),
            ServiceTo = new DateOnly(2026, 9, 14),
            Kind = BillKind.Regular,
            Net = 7.05m,
            VatPercent = 21m,
            Vat = 1.48m,
            Gross = 8.53m,
            ConsumptionOnBill = 5.2m,
            CalculatedSnapshot = snapshot,
            AttachmentIds = [Guid.NewGuid(), Guid.NewGuid()],
        };
        var definition = new CustomFieldDefinition { OwnerId = contract.Id, Name = "Losses", Kind = CustomFieldKind.Number };
        bill.CustomFields.Add(CustomFieldValue.From(definition, 0.2m));
        var line = new BillLine { BillId = bill.Id, Description = "Cold water", Quantity = 5m, Unit = "m3", UnitPrice = 1.41m, Net = 7.05m, VatPercent = 21m, Vat = 1.48m, Gross = 8.53m, SortOrder = 1 };
        line.CustomFields.Add(CustomFieldValue.From(new CustomFieldDefinition { Name = "Note", Kind = CustomFieldKind.Text }, "ok"));
        bill.Lines.Add(line);
        await using (var db = test.NewContext())
        {
            db.AddRange(property, supplier, category, contract, bill);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.Bills.Include(b => b.Lines).SingleAsync();
        Assert.Equal(new YearMonth(2026, 9), loaded.Period);
        Assert.Equal((new DateOnly(2026, 8, 15), new DateOnly(2026, 9, 14)), (loaded.ServiceFrom, loaded.ServiceTo));
        Assert.Equal(bill.AttachmentIds, loaded.AttachmentIds);
        Assert.Equal(0.2m, Assert.Single(loaded.CustomFields).AsDecimal());
        Assert.Equal("ok", Assert.Single(Assert.Single(loaded.Lines).CustomFields).Value);
        var loadedSnapshot = loaded.CalculatedSnapshot!;
        Assert.Equal((contract.Id, new YearMonth(2026, 9), 8.53m), (loadedSnapshot.ContractId, loadedSnapshot.Period, loadedSnapshot.TotalGross));
        var loadedLine = Assert.Single(loadedSnapshot.Lines);
        Assert.Equal((componentId, 5m, 1.41m, 8.53m), (loadedLine.ComponentId, loadedLine.Quantity, loadedLine.UnitPrice, loadedLine.Gross));
        Assert.Empty(loadedLine.Consumption);
        Assert.Equal(ChargeWarningCodes.ContractInactive, Assert.Single(loadedSnapshot.Warnings).Code);
    }

    [Fact]
    public async Task Bill_without_snapshot_reads_null()
    {
        using var test = new TestDb();
        var (property, category) = (NewProperty(), NewCategory());
        var bill = Bill.CreateOneOff(property.Id, category.Id, "Kindergarten", new YearMonth(2026, 10), test.Clock);
        await using (var db = test.NewContext())
        {
            db.AddRange(property, category, bill);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.Bills.SingleAsync();
        Assert.Null(loaded.CalculatedSnapshot);
        Assert.Null(loaded.ContractId);
    }

    [Fact]
    public async Task Payment_with_allocations()
    {
        using var test = new TestDb();
        var (property, category) = (NewProperty(), NewCategory());
        var bill = Bill.CreateOneOff(property.Id, category.Id, "Tax", new YearMonth(2026, 10), test.Clock);
        var payment = new Payment { PropertyId = property.Id, PaidOn = new DateOnly(2026, 10, 12), Amount = 50m, Method = PaymentMethod.Card };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = 40m, Note = "part" });
        await using (var db = test.NewContext())
        {
            db.AddRange(property, category, bill, payment);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.Payments.Include(p => p.Allocations).SingleAsync();
        Assert.Equal(PaymentMethod.Card, loaded.Method);
        var allocation = Assert.Single(loaded.Allocations);
        Assert.Equal((bill.Id, 40m, "part"), (allocation.BillId, allocation.Amount, allocation.Note));
    }

    [Fact]
    public async Task Built_in_and_user_categories()
    {
        using var test = new TestDb();
        var builtIn = new Category { SystemKey = ServiceKind.Electricity, Color = "#FFCC00" };
        var user = new Category { Name = "Kindergarten", Color = "#00AAFF", Notes = "monthly" };
        await using (var db = test.NewContext())
        {
            db.Categories.AddRange(builtIn, user);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loadedBuiltIn = await read.Categories.SingleAsync(c => c.Id == builtIn.Id);
        Assert.True(loadedBuiltIn.IsBuiltIn);
        Assert.Equal((ServiceKind.Electricity, null), (loadedBuiltIn.SystemKey, loadedBuiltIn.Name));
        var loadedUser = await read.Categories.SingleAsync(c => c.Id == user.Id);
        Assert.False(loadedUser.IsBuiltIn);
        Assert.Equal(("Kindergarten", "monthly"), (loadedUser.Name, loadedUser.Notes));
    }

    [Theory]
    [InlineData("6.5597")]
    [InlineData("0.702804")]
    [InlineData("413.816")]
    [InlineData("-12.10")]
    [InlineData("12345678.123456")]
    public async Task Decimals_round_trip_exactly(string text)
    {
        using var test = new TestDb();
        var value = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        var (property, supplier, category) = (NewProperty(), new Supplier { Name = "S" }, NewCategory());
        var contract = new Contract { PropertyId = property.Id, SupplierId = supplier.Id, CategoryId = category.Id, Title = "T" };
        var meter = new Meter { PropertyId = property.Id, InstalledOn = new DateOnly(2020, 1, 1) };
        var register = MeterRegister.CreateDefault(meter.Id);
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = new DateOnly(2026, 1, 1), Value = value });
        meter.Registers.Add(register);
        await using (var db = test.NewContext())
        {
            db.AddRange(property, supplier, category, contract, meter);
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var loaded = await read.MeterReadings.SingleAsync();
        Assert.Equal(value, loaded.Value);
    }

    [Fact]
    public async Task Bills_ordered_by_period_in_sql_are_chronological()
    {
        using var test = new TestDb();
        var (property, category) = (NewProperty(), NewCategory());
        var periods = new[] { new YearMonth(2026, 2), new YearMonth(2025, 12), new YearMonth(2026, 10), new YearMonth(2004, 5), new YearMonth(2026, 1) };
        await using (var db = test.NewContext())
        {
            db.AddRange(property, category);
            db.Bills.AddRange(periods.Select(p => Bill.CreateOneOff(property.Id, category.Id, "x", p, test.Clock)));
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var ordered = await read.Bills.OrderBy(b => b.Period).Select(b => b.Period).ToListAsync();
        Assert.Equal(periods.OrderBy(p => p).ToList(), ordered);
        Assert.Equal(2, await read.Bills.CountAsync(b => b.Period >= new YearMonth(2026, 2)));
    }

    [Fact]
    public async Task DateTimeOffset_round_trips_as_utc_and_orders_chronologically()
    {
        using var test = new TestDb();
        var early = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var late = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero).AddTicks(1);
        await using (var db = test.NewContext())
        {
            db.PreserveTimestamps = true;
            db.Categories.Add(new Category { Name = "Late", CreatedAt = late, UpdatedAt = late });
            db.Categories.Add(new Category { Name = "Early", CreatedAt = early, UpdatedAt = early });
            await db.SaveChangesAsync();
        }

        await using var read = test.NewContext();
        var names = await read.Categories.OrderBy(c => c.UpdatedAt).Select(c => c.Name).ToListAsync();
        Assert.Equal(["Early", "Late"], names);
        var loaded = await read.Categories.SingleAsync(c => c.Name == "Late");
        Assert.Equal(late, loaded.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, loaded.UpdatedAt.Offset);
    }
}
