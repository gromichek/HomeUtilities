using System.Globalization;
using System.Numerics;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;

namespace HomeUtilities.Data.Sample;

/// <summary>
/// Fills an empty database with invented data (three properties, suppliers, contracts, meters and a year of readings).
/// Deterministic: a fixed random seed and dates relative to today. Everything here is fictitious: names, numbers,
/// <c>example.com</c> addresses and IBANs with a valid checksum for a made-up bank code.
/// </summary>
public sealed class SampleDataGenerator(
    IPropertyStore properties,
    ISupplierStore suppliers,
    ICategoryStore categories,
    IContractStore contracts,
    IMeterStore meters,
    IClock clock) : ISampleDataGenerator
{
    /// <summary>Number of monthly readings generated per register.</summary>
    public const int MonthsOfReadings = 13;

    private const int ReplacementMonthsAgo = 5;

    private readonly Random _random = new(20261024);
    private Dictionary<ServiceKind, Guid> _categoryIds = [];
    private DateOnly _today;

    /// <inheritdoc />
    public async Task<bool> FillAsync(CancellationToken ct = default)
    {
        if ((await properties.ListAsync(includeArchived: true, ct)).Count > 0)
        {
            return false;
        }

        _today = clock.Today;
        var allCategories = await categories.ListAsync(ct);
        _categoryIds = Enum.GetValues<ServiceKind>().ToDictionary(
            k => k,
            k => BuiltInCategories.FindFor(k, allCategories)
                ?? throw new InvalidOperationException($"The built-in category for {k} is missing; initialize the database first."));

        var flat = await SaveAsync(NewFlat(), properties, ct);
        var cottage = await SaveAsync(NewCottage(), properties, ct);
        var garage = await SaveAsync(NewGarage(), properties, ct);

        var water = await SaveAsync(NewSupplier("Riverside Water", "40003000001", "+000 5550 0101", 1, "RIVERSIDE", "Water {CustomerNumber} {Period:MM.yyyy}"), suppliers, ct);
        var heat = await SaveAsync(NewSupplier("Northern Heat", "40003000002", "+000 5550 0102", 2, "NORTHHEAT", "Heating {CustomerNumber} {Period:MM.yyyy}"), suppliers, ct);
        var power = await SaveAsync(NewSupplier("BrightGrid Energy", "40003000003", "+000 5550 0103", 3, "BRIGHTGRID", "Electricity {CustomerNumber} {Period:MM.yyyy}"), suppliers, ct);
        var waste = await SaveAsync(NewSupplier("CleanCity Waste", "40003000004", "+000 5550 0104", 4, "CLEANCITY", "Waste {CustomerNumber} {Period:MM.yyyy}"), suppliers, ct);
        var manager = await SaveAsync(NewSupplier("Oak Street Management", "40003000005", "+000 5550 0105", 5, "OAKSTREET", "Maintenance {CustomerNumber} {Period:MM.yyyy}"), suppliers, ct);
        var net = await SaveAsync(NewSupplier("FiberNet", "40003000006", "+000 5550 0106", 6, "FIBERNET", "Internet {CustomerNumber} {Period:MM.yyyy}"), suppliers, ct);
        var tax = await SaveAsync(NewSupplier("Example Municipality Tax Office", "40003000007", "+000 5550 0107", 7, "EXMUNTAX", "Property tax {ContractNumber}"), suppliers, ct);

        var start = _today.AddYears(-6);
        var cn = 48200;

        var cWater = NewContract(flat, water, ServiceKind.ColdWater, "Water and sewerage", BillingMode.Actual, $"CN-{++cn}", start, 25, 28);
        var cHeat = NewContract(flat, heat, ServiceKind.Heating, "Heating and hot water heating", BillingMode.Actual, $"CN-{++cn}", start, 25, 28);
        var cPowerFlat = NewContract(flat, power, ServiceKind.Electricity, "Electricity", BillingMode.Actual, $"CN-{++cn}", start, 28, 3);
        var cWaste = NewContract(flat, waste, ServiceKind.Waste, "Waste (per resident)", BillingMode.Actual, $"CN-{++cn}", start, null, null);
        var cMaint = NewContract(flat, manager, ServiceKind.Maintenance, "Maintenance (per area)", BillingMode.Actual, $"CN-{++cn}", start, null, null);
        var cNet = NewContract(flat, net, ServiceKind.InternetTv, "Internet and TV", BillingMode.Fixed, $"CN-{++cn}", start, null, null);
        cNet.BillingTiming = BillingTiming.InAdvance;
        var cPowerCottage = NewContract(cottage, power, ServiceKind.Electricity, "Electricity (day/night, solar)", BillingMode.Actual, $"CN-{++cn}", start, 28, 3);
        var cLand = NewContract(cottage, tax, ServiceKind.PropertyTaxLand, "Land tax", BillingMode.Assessed, $"CN-{++cn}", start, null, null);
        var cPowerGarage = NewContract(garage, power, ServiceKind.Electricity, "Electricity (fixed)", BillingMode.Fixed, $"CN-{++cn}", start, null, null);
        var cGarageTax = NewContract(garage, tax, ServiceKind.PropertyTaxBuilding, "Property tax", BillingMode.Assessed, $"CN-{++cn}", start, null, null);
        foreach (var contract in new[] { cWater, cHeat, cPowerFlat, cWaste, cMaint, cNet, cPowerCottage, cLand, cPowerGarage, cGarageTax })
        {
            await contracts.SaveAsync(contract, ct);
        }

        await AddFlatMetersAsync(flat, cWater, cHeat, cPowerFlat, ct);
        await AddCottageMeterAsync(cottage, cPowerCottage, ct);
        return true;
    }

    private async Task AddFlatMetersAsync(Property flat, Contract water, Contract heat, Contract power, CancellationToken ct)
    {
        var installed = _today.AddYears(-8);

        var coldKitchen = NewMeter(flat, MeterResource.ColdWater, MeterUnit.M3, "Kitchen cold", installed, 5);
        coldKitchen.VerifiedOn = _today.AddYears(-7);
        coldKitchen.VerificationIntervalMonths = 72; // due a year ago: shows "Verification overdue"
        var hotKitchen = NewMeter(flat, MeterResource.HotWater, MeterUnit.M3, "Kitchen hot", installed, 5);
        hotKitchen.VerifiedOn = _today.AddYears(-2);
        hotKitchen.VerificationIntervalMonths = 72;
        var hotBath = NewMeter(flat, MeterResource.HotWater, MeterUnit.M3, "Bathroom hot", installed, 5);
        hotBath.VerifiedOn = _today.AddYears(-2);
        hotBath.VerificationIntervalMonths = 72;
        var coldBathOld = NewMeter(flat, MeterResource.ColdWater, MeterUnit.M3, "Bathroom cold", _today.AddYears(-6), 5);
        coldBathOld.VerifiedOn = _today.AddYears(-6);
        coldBathOld.VerificationIntervalMonths = 72;

        var replacedOn = new DateOnly(YearMonth.From(_today).AddMonths(-ReplacementMonthsAgo).Year, YearMonth.From(_today).AddMonths(-ReplacementMonthsAgo).Month, 15);

        AddMonthlyReadings(coldKitchen, RegisterKind.Total, 120, 3.1m, 0.5m, 26, firstMonthsAgo: null, lastMonthsAgo: 1, estimatedMonthsAgo: 7);
        AddMonthlyReadings(hotKitchen, RegisterKind.Total, 45, 1.4m, 0.3m, 26, null, 1);
        AddMonthlyReadings(hotBath, RegisterKind.Total, 90, 2.6m, 0.4m, 26, null, 1);
        AddMonthlyReadings(coldBathOld, RegisterKind.Total, 200, 3.6m, 0.5m, 26, null, ReplacementMonthsAgo + 1);
        foreach (var meter in new[] { coldKitchen, hotKitchen, hotBath, coldBathOld })
        {
            await meters.SaveAsync(meter, ct);
        }

        // Replacement of the bathroom cold meter; the old chain end gets its Final reading here.
        var oldMeter = await meters.GetAsync(coldBathOld.Id, ct) ?? throw new InvalidOperationException("Sample meter vanished.");
        var finalValue = oldMeter.Registers[0].Readings.Max(r => r.Value) + 1.2m;
        var replacement = MeterReplacement.Replace(oldMeter, replacedOn, finalValue, 0.004m, clock);
        var newMeter = replacement.NewMeter;
        newMeter.SerialNumber = SerialOf("BC2");
        newMeter.Digits = 5;
        newMeter.VerifiedOn = replacedOn;
        newMeter.VerificationIntervalMonths = 72;
        newMeter.Touch(clock);

        var links = new List<ContractMeter>
        {
            Link(water, coldKitchen), Link(water, hotKitchen), Link(water, hotBath), Link(water, coldBathOld),
            Link(heat, hotKitchen), Link(heat, hotBath),
        };
        foreach (var link in links)
        {
            await contracts.SaveContractMeterAsync(link, ct);
        }

        var close = new List<ContractMeter> { links[3] };
        close[0].ValidTo = replacedOn.AddDays(-1);

        await meters.SaveReplacementAsync(oldMeter, newMeter, close, [Link(water, newMeter, replacedOn)], ct);
        var newRegister = newMeter.Registers[0].Id;
        var after = MonthlyReadings(RegisterKind.Total, 3.6m, 3.6m, 0.5m, 26, firstMonthsAgo: ReplacementMonthsAgo, lastMonthsAgo: 1, estimatedMonthsAgo: null);
        await meters.AddReadingsAsync(after.Select(r => (newRegister, r)), ct);

        // Electricity with a 5-digit dial that rolls over from 99 9xx to 00 0xx during the year.
        var electricity = NewMeter(flat, MeterResource.Electricity, MeterUnit.KWh, "Hallway", installed, 5);
        electricity.VerifiedOn = _today.AddYears(-3);
        electricity.VerificationIntervalMonths = 96;
        AddMonthlyReadings(electricity, RegisterKind.Total, 99_000, 130m, 25m, 28, null, 1, decimals: 0, rollOverAt: 100_000m);
        await meters.SaveAsync(electricity, ct);
        await contracts.SaveContractMeterAsync(Link(power, electricity), ct);
    }

    private async Task AddCottageMeterAsync(Property cottage, Contract power, CancellationToken ct)
    {
        var meter = NewMeter(cottage, MeterResource.Electricity, MeterUnit.KWh, "Cottage hall", _today.AddYears(-5), 6);
        meter.VerifiedOn = _today.AddYears(-1);
        meter.VerificationIntervalMonths = 96;
        meter.Registers.Clear();
        foreach (var (kind, direction, baseValue, step) in new[]
        {
            (RegisterKind.Day, EnergyDirection.Import, 8_000m, 90m),
            (RegisterKind.Night, EnergyDirection.Import, 5_000m, 60m),
            (RegisterKind.Export, EnergyDirection.Export, 700m, 40m),
        })
        {
            var register = new MeterRegister { MeterId = meter.Id, Kind = kind, Direction = direction };
            register.Readings.AddRange(MonthlyReadings(kind, baseValue, step, step / 4, 28, MonthsOfReadings, 1, null, 0, null));
            foreach (var reading in register.Readings)
            {
                reading.RegisterId = register.Id;
            }

            meter.Registers.Add(register);
        }

        await meters.SaveAsync(meter, ct);
        await contracts.SaveContractMeterAsync(Link(power, meter), ct);
    }

    private void AddMonthlyReadings(
        Meter meter,
        RegisterKind kind,
        decimal startValue,
        decimal step,
        decimal spread,
        int day,
        int? firstMonthsAgo,
        int lastMonthsAgo,
        int? estimatedMonthsAgo = null,
        int decimals = 3,
        decimal? rollOverAt = null)
    {
        var register = meter.Registers.Single(r => r.Kind == kind);
                var first = firstMonthsAgo ?? MonthsOfReadings;
        foreach (var reading in MonthlyReadings(kind, startValue, step, spread, day, first, lastMonthsAgo, estimatedMonthsAgo, decimals, rollOverAt))
        {
            reading.RegisterId = register.Id;
            register.Readings.Add(reading);
        }
    }

    private List<MeterReading> MonthlyReadings(
        RegisterKind kind,
        decimal startValue,
        decimal step,
        decimal spread,
        int day,
        int firstMonthsAgo,
        int lastMonthsAgo,
        int? estimatedMonthsAgo,
        int decimals = 3,
        decimal? rollOverAt = null)
    {
        _ = kind;
        var result = new List<MeterReading>();
        var value = startValue;
        for (var ago = firstMonthsAgo; ago >= lastMonthsAgo; ago--)
        {
            var month = YearMonth.From(_today).AddMonths(-ago);
            var date = new DateOnly(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));
            if (ago != firstMonthsAgo)
            {
                var delta = step + ((decimal)_random.NextDouble() * 2m - 1m) * spread;
                value += Math.Max(0m, delta);
            }

            var shown = Math.Round(value, decimals);
            if (rollOverAt is { } limit && shown >= limit)
            {
                shown -= limit;
                value -= limit;
                rollOverAt = null;
            }

            result.Add(new MeterReading
            {
                Date = date,
                Value = shown,
                Kind = ago == estimatedMonthsAgo ? ReadingKind.Estimated : ReadingKind.Regular,
                SubmittedOn = ago > 2 ? date.AddDays(1) : null,
            });
        }

        return result;
    }

    private static ContractMeter Link(Contract contract, Meter meter, DateOnly? from = null) =>
        new() { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = from ?? meter.InstalledOn };

    private Meter NewMeter(Property property, MeterResource resource, MeterUnit unit, string location, DateOnly installed, int digits)
    {
        var meter = new Meter
        {
            PropertyId = property.Id,
            Resource = resource,
            Unit = unit,
            Location = location,
            Digits = digits,
            InstalledOn = installed,
            SerialNumber = SerialOf(location),
        };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        return meter;
    }

    private static string SerialOf(string seed) => "SN-" + (Math.Abs(seed.Aggregate(17, (h, c) => (h * 31) + c)) % 900000 + 100000).ToString(CultureInfo.InvariantCulture);

    private Contract NewContract(
        Property property, Supplier supplier, ServiceKind service, string title, BillingMode mode, string customerNumber, DateOnly validFrom, int? windowFrom, int? windowTo) =>
        new()
        {
            PropertyId = property.Id,
            SupplierId = supplier.Id,
            Service = service,
            Title = title,
            CategoryId = _categoryIds[service],
            CustomerNumber = customerNumber,
            ContractNumber = "C/" + customerNumber[3..],
            ValidFrom = validFrom,
            BillingMode = mode,
            ReadingDayFrom = windowFrom,
            ReadingDayTo = windowTo,
            BillDay = 5,
            PaymentDueDay = 20,
        };

    private Property NewFlat()
    {
        var flat = new Property
        {
            Name = "Flat in town",
            Kind = PropertyKind.Apartment,
            Address = "1 Example Street, apt. 12, Exampleville",
            Notes = "Invented sample data for testing.",
            SortOrder = 0,
        };
        var from = _today.AddYears(-6);
        flat.Parameters.Add(Parameter(flat, PropertyParameterKind.TotalArea, 52.3m, from));
        flat.Parameters.Add(Parameter(flat, PropertyParameterKind.DeclaredResidents, 2m, from));
        flat.Parameters.Add(Parameter(flat, PropertyParameterKind.DeclaredResidents, 3m, _today.AddMonths(-6)));
        return flat;
    }

    private Property NewCottage()
    {
        var cottage = new Property
        {
            Name = "Cottage",
            Kind = PropertyKind.Cottage,
            Address = "Lakeside 7, Sampletown",
            SortOrder = 1,
        };
        var from = _today.AddYears(-6);
        cottage.Parameters.Add(Parameter(cottage, PropertyParameterKind.TotalArea, 64m, from));
        cottage.Parameters.Add(Parameter(cottage, PropertyParameterKind.LandArea, 1200m, from));
        return cottage;
    }

    private Property NewGarage()
    {
        var garage = new Property
        {
            Name = "Garage",
            Kind = PropertyKind.Garage,
            Address = "Garage row 3, box 15, Exampleville",
            SortOrder = 2,
        };
        garage.Parameters.Add(Parameter(garage, PropertyParameterKind.TotalArea, 18m, _today.AddYears(-6)));
        return garage;
    }

    private static PropertyParameter Parameter(Property property, PropertyParameterKind kind, decimal value, DateOnly from) =>
        new() { PropertyId = property.Id, Kind = kind, Value = value, ValidFrom = from };

    private static Supplier NewSupplier(string name, string registration, string phone, int index, string slug, string purpose)
    {
        var host = slug.ToLowerInvariant();
        var supplier = new Supplier
        {
            Name = name,
            RegistrationNumber = registration,
            Phone = phone,
            Email = $"info@{host}.example.com",
            PaymentDetails = new PaymentDetails(
                name,
                registration,
                MakeIban("LV", "TEST", index),
                "TESTLV22",
                "Test Bank (fictitious)",
                purpose),
        };
        supplier.Links.Add(new Link { OwnerId = supplier.Id, Kind = LinkKind.Website, Url = $"https://www.{host}.example.com", SortOrder = 0 });
        supplier.Links.Add(new Link { OwnerId = supplier.Id, Kind = LinkKind.CustomerPortal, Url = $"https://portal.{host}.example.com", SortOrder = 1 });
        supplier.Links.Add(new Link { OwnerId = supplier.Id, Kind = LinkKind.SubmitReadings, Url = $"https://portal.{host}.example.com/readings", SortOrder = 2 });
        supplier.Links.Add(new Link { OwnerId = supplier.Id, Kind = LinkKind.PayOnline, Url = $"https://pay.{host}.example.com", SortOrder = 3 });
        supplier.Links.Add(new Link { OwnerId = supplier.Id, Kind = LinkKind.Tariffs, Url = $"https://www.{host}.example.com/tariffs", SortOrder = 4 });
        return supplier;
    }

    /// <summary>An IBAN with correct mod-97 check digits for the given fictitious bank code and account index.</summary>
    internal static string MakeIban(string country, string bankCode, int accountIndex)
    {
        var account = (1_000_000_000_000L + accountIndex).ToString(CultureInfo.InvariantCulture)[..13];
        var bban = bankCode + account;
        var digits = string.Concat((bban + country + "00").Select(c => char.IsAsciiDigit(c) ? c.ToString() : (c - 'A' + 10).ToString(CultureInfo.InvariantCulture)));
        var check = 98 - (int)(BigInteger.Parse(digits, CultureInfo.InvariantCulture) % 97);
        return $"{country}{check:00}{bban}";
    }

    private static async Task<T> SaveAsync<T>(T entity, IStore<T> store, CancellationToken ct)
        where T : SyncEntity
    {
        await store.SaveAsync(entity, ct);
        return entity;
    }
}
