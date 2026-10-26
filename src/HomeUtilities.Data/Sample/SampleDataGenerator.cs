using System.Globalization;
using System.Numerics;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Payments;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Data.Sample;

/// <summary>
/// Fills an empty database with invented data (three properties, suppliers, contracts, meters, a year of readings and a year
/// of bills with their payments).
/// Deterministic: a fixed random seed and dates relative to today. Everything here is fictitious: names, numbers,
/// <c>example.com</c> addresses and IBANs with a valid checksum for a made-up bank code.
/// </summary>
public sealed class SampleDataGenerator(
    IPropertyStore properties,
    ISupplierStore suppliers,
    ICategoryStore categories,
    IContractStore contracts,
    IMeterStore meters,
    ITariffStore tariffs,
    IBillStore bills,
    IPaymentStore payments,
    IChargeInputLoader chargeLoader,
    IClock clock) : ISampleDataGenerator
{
    /// <summary>Number of monthly readings generated per register.</summary>
    public const int MonthsOfReadings = 13;

    private const int ReplacementMonthsAgo = 5;

    private readonly Random _random = new(20261024);
    private readonly List<Bill> _transferBills = [];
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
        cPowerFlat.CustomFields.Add(new CustomFieldDefinition
        {
            OwnerId = cPowerFlat.Id,
            Name = "Supplier reference",
            Kind = CustomFieldKind.Text,
            Target = CustomFieldTarget.Bill,
            SortOrder = 0,
        });
        var cPowerCottage = NewContract(cottage, power, ServiceKind.Electricity, "Electricity (day/night, solar)", BillingMode.Actual, $"CN-{++cn}", start, 28, 3);
        var cLand = NewContract(cottage, tax, ServiceKind.PropertyTaxLand, "Land tax", BillingMode.Assessed, $"CN-{++cn}", start, null, null);
        var cPowerGarage = NewContract(garage, power, ServiceKind.Electricity, "Electricity (fixed)", BillingMode.Fixed, $"CN-{++cn}", start, null, null);
        var cGarageTax = NewContract(garage, tax, ServiceKind.PropertyTaxBuilding, "Property tax", BillingMode.Assessed, $"CN-{++cn}", start, null, null);
        foreach (var contract in new[] { cWater, cHeat, cPowerFlat, cWaste, cMaint, cNet, cPowerCottage, cLand, cPowerGarage, cGarageTax })
        {
            await contracts.SaveAsync(contract, ct);
        }

        var hotMeters = await AddFlatMetersAsync(flat, cWater, cHeat, cPowerFlat, ct);
        await AddCottageMeterAsync(cottage, cPowerCottage, ct);
        await AddTariffsAsync(cWater, cHeat, cPowerFlat, cWaste, cMaint, cNet, cPowerCottage, hotMeters, ct);
        var supplierOf = new Dictionary<Guid, Supplier> { [water.Id] = water, [heat.Id] = heat, [power.Id] = power, [waste.Id] = waste, [manager.Id] = manager, [net.Id] = net };
        await AddBillsAsync(flat, [cWater, cHeat, cPowerFlat, cWaste, cMaint], cNet, supplierOf, ct);
        return true;
    }

    // ---- Bills ------------------------------------------------------------------------------------------------------

    /// <summary>The month (months ago) whose bill of each contract is still unpaid, so the list shows unpaid and overdue bills.</summary>
    private const int LatestBillsMonthsAgo = 1;

    /// <summary>The month of the waste bill that stays unpaid for good: its due date passed long ago (overdue).</summary>
    private const int OverdueWasteMonthsAgo = 3;

    /// <summary>The month of the maintenance bill that is partly paid by a manual payment.</summary>
    private const int PartlyPaidMonthsAgo = 2;

    /// <summary>The month of the electricity bill with detail lines and a custom field.</summary>
    private const int DetailedBillMonthsAgo = 2;

    /// <summary>
    /// A year of bills of the flat: the in-arrears contracts for the last twelve months, the prepaid internet for ten months back
    /// to next month, and a one-off boiler repair. Totals are near the calculation (a few clearly above or below, water with
    /// the quantity on the bill); nearly all are paid with the "Paid" quick action, one is partly paid, one is overdue and one
    /// (next month's internet) is prepaid.
    /// </summary>
    private async Task AddBillsAsync(
        Property flat, IReadOnlyList<Contract> inArrears, Contract internet, IReadOnlyDictionary<Guid, Supplier> supplierOf, CancellationToken ct)
    {
        var current = YearMonth.From(_today);
        var counter = 0;
        _transferBills.Clear();
        foreach (var monthsAgo in Enumerable.Range(1, 12).Reverse())
        {
            foreach (var contract in inArrears)
            {
                counter++;
                await AddContractBillAsync(flat, contract, current.AddMonths(-monthsAgo), monthsAgo, counter, supplierOf, ct);
            }
        }

        for (var offset = -10; offset <= 1; offset++)
        {
            counter++;
            await AddContractBillAsync(flat, internet, current.AddMonths(offset), -offset, counter, supplierOf, ct);
        }

        await AddBoilerRepairAsync(flat, current.AddMonths(-4), ct);
        await AddTransferForTwoContractsAsync(flat, ct);
        await AddInternetPrepaymentAsync(flat, internet, current.AddMonths(2), supplierOf, ct);
        await AddMaintenanceRefundAsync(flat, inArrears.First(c => c.Service == ServiceKind.Maintenance), current.AddMonths(-5), ct);
    }

    /// <summary>
    /// One bank transfer without a contract that pays the latest waste bill and the latest maintenance bill together
    /// (the owner pays several bills of the flat at once).
    /// </summary>
    private async Task AddTransferForTwoContractsAsync(Property flat, CancellationToken ct)
    {
        if (_transferBills.Count == 0)
        {
            return;
        }

        var total = _transferBills.Sum(b => b.Gross);
        var payment = new Payment
        {
            PropertyId = flat.Id,
            PaidOn = _today,
            Amount = total,
            Currency = Currency.EUR,
            Method = PaymentMethod.BankTransfer,
            Reference = "Waste and maintenance " + BillText(_transferBills[0].Period),
            Notes = "Invented sample payment: one transfer for the bills of two contracts.",
        };
        foreach (var bill in _transferBills)
        {
            payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = bill.Gross });
        }

        await payments.SaveAsync(payment, ct);
    }

    private static string BillText(YearMonth period) => period.FirstDay.ToString("MM.yyyy", CultureInfo.InvariantCulture);

    /// <summary>A payment for the internet bill of a month that has not been entered yet: all of it is a prepayment.</summary>
    private async Task AddInternetPrepaymentAsync(
        Property flat, Contract internet, YearMonth month, IReadOnlyDictionary<Guid, Supplier> supplierOf, CancellationToken ct)
    {
        var template = PaymentDetails.Effective(internet, supplierOf[internet.SupplierId]).PurposeTemplate;
        var amount = 22.95m;
        var payment = new Payment
        {
            ContractId = internet.Id,
            PropertyId = flat.Id,
            PaidOn = _today,
            Amount = amount,
            Currency = Currency.EUR,
            Method = PaymentMethod.BankTransfer,
            Reference = template is null
                ? null
                : PaymentPurpose.Render(template, new PaymentPurposeContext(internet.CustomerNumber, internet.ContractNumber, null, month, amount, null)),
            Notes = "Invented sample payment: paid before the bill arrived (prepayment).",
        };
        await payments.SaveAsync(payment, ct);
    }

    /// <summary>
    /// An annual settlement of the maintenance contract that ends in the owner's favour (a negative bill) and the refund the
    /// manager transferred (a negative payment allocated to it).
    /// </summary>
    private async Task AddMaintenanceRefundAsync(Property flat, Contract maintenance, YearMonth period, CancellationToken ct)
    {
        var bill = Bill.CreateFor(maintenance, period, clock);
        bill.Kind = BillKind.Settlement;
        bill.Number = $"SET-{period.Year}-0001";
        bill.IssuedOn = DayOf(period.AddMonths(1), 10);
        bill.DueOn = bill.IssuedOn;
        bill.Notes = "Invented sample settlement: the yearly reconciliation returns money to the owner.";
        ApplyAmounts(bill, -12.40m, 21m);
        await bills.SaveAsync(bill, ct);

        var refund = new Payment
        {
            ContractId = maintenance.Id,
            PropertyId = flat.Id,
            PaidOn = DayOf(period.AddMonths(1), 18),
            Amount = bill.Gross,
            Currency = bill.Currency,
            Method = PaymentMethod.BankTransfer,
            Reference = "Refund " + bill.Number,
        };
        refund.Allocations.Add(new PaymentAllocation { PaymentId = refund.Id, BillId = bill.Id, Amount = bill.Gross });
        await payments.SaveAsync(refund, ct);
    }

    private async Task AddContractBillAsync(
        Property flat,
        Contract contract,
        YearMonth period,
        int monthsAgo,
        int counter,
        IReadOnlyDictionary<Guid, Supplier> supplierOf,
        CancellationToken ct)
    {
        var input = await chargeLoader.LoadAsync(contract.Id, period, ct);

        // Heating: the supplier prints the quantities of its share; the sample bill carries them (invented values).
        var billQuantities = new List<BillQuantity>();
        if (input is not null)
        {
            foreach (var component in input.Components.Where(c => c.Basis == ChargeBasis.QuantityFromBill).OrderBy(c => c.SortOrder))
            {
                var quantity = component.QuantityUnit == MeterUnit.MWh
                    ? 0.18m + period.Month % 4 * 0.01m
                    : 0.10m + period.Month % 3 * 0.05m;
                billQuantities.Add(new BillQuantity(component.Id, quantity));
            }

            if (billQuantities.Count > 0)
            {
                input = input with { QuantitiesFromBill = billQuantities.ToDictionary(q => q.ComponentId, q => q.Quantity) };
            }
        }

        var calculated = input is null ? null : ChargeCalculator.Calculate(input);
        var hasCalculation = calculated is { IsComplete: true, Lines.Count: > 0 };
        var vatPercent = calculated?.Lines.FirstOrDefault()?.VatPercent ?? 21m;

        // What the supplier billed: the calculation with a small deviation; some contracts have no usable calculation.
        var calculatedTotal = hasCalculation ? calculated!.TotalGross : 0m;
        var total = contract.Service switch
        {
            ServiceKind.Heating when billQuantities.Count > 0 && hasCalculation => calculatedTotal,
            ServiceKind.Heating => MoneyMath.Round(calculatedTotal + 46m + (decimal)_random.NextDouble() * 8m),
            ServiceKind.Waste => MoneyMath.Round(13.9m + (decimal)_random.NextDouble() * 1.2m),
            ServiceKind.InternetTv => 22.95m,
            _ when !hasCalculation => MoneyMath.Round(30m + (decimal)_random.NextDouble() * 10m),
            _ => MoneyMath.Round(calculatedTotal * (1m + Deviation(contract, monthsAgo))),
        };

        // The bill with detail lines bills exactly the calculated total, so its lines add up to it.
        var detailed = contract.Service == ServiceKind.Electricity && monthsAgo == DetailedBillMonthsAgo && hasCalculation;
        if (detailed)
        {
            total = calculatedTotal;
        }

        var bill = Bill.CreateFor(contract, period, clock);
        bill.Number = $"INV-{period.Year}-{counter:0000}";
        var issued = DayOf(contract.BillingTiming == BillingTiming.InAdvance ? period.AddMonths(-1) : period.AddMonths(1), contract.BillDay ?? 5);
        bill.IssuedOn = issued > _today ? _today : issued;
        var due = BillDefaults.DueDate(contract, period) ?? bill.IssuedOn.Value;
        bill.DueOn = due < bill.IssuedOn ? bill.IssuedOn : due;
        ApplyAmounts(bill, total, vatPercent);
        bill.BillQuantities = billQuantities;
        if (hasCalculation && !calculated!.Lines.Any(l => l.IsFromBill))
        {
            bill.CalculatedSnapshot = calculated;
        }

        // Water: the quantity the supplier billed is a little above the metered one (losses in the building).
        if (contract.Service == ServiceKind.ColdWater && calculated is not null)
        {
            var metered = BillComparison.MeteredQuantity(calculated);
            if (metered is { } quantity)
            {
                bill.ConsumptionOnBill = MoneyMath.RoundQuantity(quantity + 0.2m + (decimal)_random.NextDouble() * 0.5m);
            }
        }

        if (detailed)
        {
            AddDetailLines(bill, calculated!);
            var reference = contract.CustomFields.First();
            bill.CustomFields.Add(CustomFieldValue.From(reference, "REF-" + period.ToString().Replace("-", string.Empty, StringComparison.Ordinal)));
        }

        await bills.SaveAsync(bill, ct);

        var overdueWaste = contract.Service == ServiceKind.Waste && monthsAgo == OverdueWasteMonthsAgo;
        var latestUnpaid = monthsAgo == LatestBillsMonthsAgo && contract.BillingTiming == BillingTiming.InArrears;
        var partlyPaid = contract.Service == ServiceKind.Maintenance && monthsAgo == PartlyPaidMonthsAgo;
        if (latestUnpaid && contract.Service is ServiceKind.Waste or ServiceKind.Maintenance)
        {
            _transferBills.Add(bill);
        }

        if (overdueWaste || latestUnpaid)
        {
            return;
        }

        if (partlyPaid)
        {
            // Not yet due, so the badge shows "partially paid" (yellow) instead of "overdue".
            bill.DueOn = _today.AddDays(10);
            await bills.SaveAsync(bill, ct);
            await payments.SaveAsync(NewPayment(bill, contract, supplierOf, MoneyMath.Round(total * 0.6m), quick: false), ct);
            return;
        }

        await payments.SaveAsync(NewPayment(bill, contract, supplierOf, total, quick: true), ct);
    }

    /// <summary>A few bills clearly above (+9 %) or below (−7 %) the calculation; the rest within −1 % … +3 %.</summary>
    private decimal Deviation(Contract contract, int monthsAgo)
    {
        if (contract.Service == ServiceKind.ColdWater)
        {
            if (monthsAgo == LatestBillsMonthsAgo)
            {
                return 0.09m;
            }

            if (monthsAgo == 4)
            {
                return -0.07m;
            }
        }

        return -0.01m + (decimal)_random.NextDouble() * 0.04m;
    }

    private static void ApplyAmounts(Bill bill, decimal total, decimal vatPercent)
    {
        var split = MoneyMath.FromGross(total, vatPercent);
        bill.Net = split.Net;
        bill.VatPercent = vatPercent;
        bill.Vat = split.Vat;
        bill.Gross = split.Gross;
    }

    /// <summary>Lines of a bill that mirror the calculation (the bill total is the calculated total, so the lines add up).</summary>
    private static void AddDetailLines(Bill bill, CalculatedCharge calculated)
    {
        var order = 0;
        foreach (var line in calculated.Lines)
        {
            bill.Lines.Add(new BillLine
            {
                BillId = bill.Id,
                Description = line.ComponentName,
                Quantity = line.Quantity,
                Unit = line.Quantity is null ? null : "kWh",
                UnitPrice = line.UnitPrice,
                Net = line.Net,
                VatPercent = line.VatPercent,
                Vat = line.Vat,
                Gross = line.Gross,
                SortOrder = order++,
            });
        }
    }

    private async Task AddBoilerRepairAsync(Property flat, YearMonth period, CancellationToken ct)
    {
        var bill = Bill.CreateOneOff(flat.Id, _categoryIds[ServiceKind.Maintenance], "Boiler repair", period, clock);
        bill.Number = "REP-0042";
        bill.IssuedOn = DayOf(period, 12);
        bill.DueOn = DayOf(period, 26);
        bill.Notes = "Invented sample expense: new thermostat and a service call.";
        ApplyAmounts(bill, 186.45m, 21m);
        await bills.SaveAsync(bill, ct);

        var payment = new Payment
        {
            PropertyId = flat.Id,
            PaidOn = DayOf(period, 14),
            Amount = bill.Gross,
            Currency = bill.Currency,
            Method = PaymentMethod.Cash,
            Reference = bill.Title,
        };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = bill.Gross });
        await payments.SaveAsync(payment, ct);
    }

    private Payment NewPayment(Bill bill, Contract contract, IReadOnlyDictionary<Guid, Supplier> supplierOf, decimal amount, bool quick)
    {
        var paidOn = (bill.DueOn ?? _today).AddDays(-3);
        if (paidOn > _today)
        {
            paidOn = _today;
        }

        if (bill.IssuedOn is { } issued && paidOn < issued)
        {
            paidOn = issued;
        }

        var template = PaymentDetails.Effective(contract, supplierOf[contract.SupplierId]).PurposeTemplate;
        var payment = new Payment
        {
            ContractId = contract.Id,
            PropertyId = bill.PropertyId,
            PaidOn = paidOn,
            Amount = amount,
            Currency = bill.Currency,
            Method = PaymentMethod.BankTransfer,
            Reference = template is null
                ? null
                : PaymentPurpose.Render(template, new PaymentPurposeContext(contract.CustomerNumber, contract.ContractNumber, bill.Number, bill.Period, amount, null)),
            CreatedByQuickAction = quick,
        };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = amount });
        return payment;
    }

    private static DateOnly DayOf(YearMonth month, int day) =>
        new(month.Year, month.Month, Math.Min(day, DateTime.DaysInMonth(month.Year, month.Month)));

    /// <summary>Tariffs of the sample contracts: all bases, a stepped price, hot meters only, one price change and one price that starts next month (a "no price" warning).</summary>
    private async Task AddTariffsAsync(
        Contract water,
        Contract heat,
        Contract power,
        Contract waste,
        Contract maintenance,
        Contract internet,
        Contract cottagePower,
        IReadOnlyList<Meter> hotMeters,
        CancellationToken ct)
    {
        var start = _today.AddYears(-6);
        var changeMonth = YearMonth.From(_today).AddMonths(-3);
        var changeDate = new DateOnly(changeMonth.Year, changeMonth.Month, 1);
        var nextMonth = YearMonth.From(_today).AddMonths(1).FirstDay;
        var waterHeating = Component(heat, "Hot water", ServiceKind.WaterHeating, ChargeBasis.PerUnit, 0, "LV-REDUCED", Rate(start, 3.20m));
        waterHeating.MeterIds = [.. hotMeters.Select(m => m.Id)];
        var heatCirculation = Component(heat, "Heat for heating/circulation", ServiceKind.Heating, ChargeBasis.QuantityFromBill, 1, "LV-REDUCED", Rate(start, 70m));
        heatCirculation.QuantityUnit = MeterUnit.MWh;
        var hotWaterDifference = Component(heat, "Hot water difference", ServiceKind.WaterHeating, ChargeBasis.QuantityFromBill, 2, "LV-REDUCED", Rate(start, 3.20m));
        hotWaterDifference.QuantityUnit = MeterUnit.M3;
        var day = Component(cottagePower, "Energy, day", ServiceKind.Electricity, ChargeBasis.PerUnit, 0, Rate(start, 0.1245m));
        day.RegisterKinds = [RegisterKind.Day];
        var night = Component(cottagePower, "Energy, night", ServiceKind.Electricity, ChargeBasis.PerUnit, 1, Rate(start, 0.0733m));
        night.RegisterKinds = [RegisterKind.Night];
        var export = Component(cottagePower, "Solar export", ServiceKind.Electricity, ChargeBasis.NetExport, 2, Rate(start, 0.061m));
        export.RegisterKinds = [RegisterKind.Export];

        List<TariffComponent> components =
        [
            // One component with two prices: the water price changed three months ago.
            Component(water, "Water supply", ServiceKind.ColdWater, ChargeBasis.PerUnit, 0, Rate(start, 1.32m), Rate(changeDate, 1.41m)),
            Component(water, "Sewerage", ServiceKind.Sewerage, ChargeBasis.PerUnit, 1, Rate(start, 0.86m)),
            waterHeating,
            heatCirculation,
            hotWaterDifference,
            Component(maintenance, "Maintenance", ServiceKind.Maintenance, ChargeBasis.PerArea, 0, Rate(start, 0.6711m, includesVat: true)),
            Component(waste, "Waste", ServiceKind.Waste, ChargeBasis.PerResident, 0, Rate(start, 5.4m)),

            // The only price starts next month, so the charges of the current and earlier months show "no price valid".
            Component(waste, "Container rental", ServiceKind.Waste, ChargeBasis.FixedMonthly, 1, Rate(nextMonth, 1.5m)),
            Component(internet, "Internet and TV", ServiceKind.InternetTv, ChargeBasis.FixedMonthly, 0, Rate(start, 22.95m, includesVat: true)),
            Component(power, "Energy", ServiceKind.Electricity, ChargeBasis.PerUnit, 0, SteppedRate(start)),
            Component(power, "Distribution", ServiceKind.Electricity, ChargeBasis.PerUnit, 1, Rate(start, 0.0391m)),
            day,
            night,
            export,

            // The supplier adds a lump sum the app cannot check (the only "amount from the bill" component).
            Component(cottagePower, "Grid fee, as billed", ServiceKind.Electricity, ChargeBasis.FromBill, 3),
        ];

        foreach (var component in components)
        {
            await tariffs.SaveAsync(component, ct);
        }
    }

    private static TariffComponent Component(
        Contract contract, string name, ServiceKind kind, ChargeBasis basis, int sortOrder, params TariffRate[] rates) =>
        Component(contract, name, kind, basis, sortOrder, "LV-STANDARD", rates);

    private static TariffComponent Component(
        Contract contract, string name, ServiceKind kind, ChargeBasis basis, int sortOrder, string vatCode, params TariffRate[] rates)
    {
        var component = new TariffComponent
        {
            ContractId = contract.Id,
            Name = name,
            ChargeKind = kind,
            Basis = basis,
            VatCode = vatCode,
            SortOrder = sortOrder,
        };
        foreach (var rate in rates)
        {
            rate.ComponentId = component.Id;
            component.Rates.Add(rate);
        }

        return component;
    }

    private static TariffRate Rate(DateOnly validFrom, decimal price, bool includesVat = false) =>
        new() { ValidFrom = validFrom, Price = price, PriceIncludesVat = includesVat, Currency = Currency.EUR };

    /// <summary>Electricity energy with two steps per month: the first 100 kWh are dearer than the rest.</summary>
    private static TariffRate SteppedRate(DateOnly validFrom)
    {
        var rate = Rate(validFrom, 0.1065m);
        rate.Tiers = [new TariffTier(100m, 0.1065m), new TariffTier(null, 0.0818m)];
        rate.TierPeriod = TierPeriod.Month;
        return rate;
    }

    private async Task<IReadOnlyList<Meter>> AddFlatMetersAsync(Property flat, Contract water, Contract heat, Contract power, CancellationToken ct)
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
        return [hotKitchen, hotBath];
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
