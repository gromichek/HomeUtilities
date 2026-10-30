using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Export;
using HomeUtilities.Core.Import;
using HomeUtilities.Core.Reports;

namespace HomeUtilities.Import.Tests;

/// <summary>
/// Invented data for the export tests: a flat with a water contract (cold and hot meter, the cold meter replaced on 2014-03-15),
/// a profile shaped like the owner's water sheet (A Month · B Cold · C Hot · D Total · E Paid · F Comment) and bills from
/// 2013-12 (LVL) to 2014-04 with two payments.
/// </summary>
public sealed class ExportScenario
{
    public static readonly ExportLabels Labels = new(
        "Date", "Consumption", "Net", "VAT %", "VAT", "Calculated", "Bill total", "Difference", "Paid", "Paid on", "Notes", "Currency",
        "Total", "Month", "Sum", "One-off",
        (meter, register) => meter.Resource + (register.Kind == RegisterKind.Total ? string.Empty : " " + register.Kind));

    public ExportScenario(ImportPeriodRule rule = ImportPeriodRule.PreviousMonth)
    {
        Property = new Property { Name = "Flat" };
        Supplier = new Supplier { Name = "Aqua" };
        Contract = new Contract
        {
            PropertyId = Property.Id,
            SupplierId = Supplier.Id,
            CategoryId = Guid.NewGuid(),
            Title = "Water",
            Service = ServiceKind.ColdWater,
            ValidFrom = new DateOnly(2013, 1, 1),
        };
        if (rule == ImportPeriodRule.ReadingWindow)
        {
            Contract.ReadingDayFrom = 20;
            Contract.ReadingDayTo = 25;
        }

        Cold = NewMeter(MeterResource.ColdWater, new DateOnly(2013, 1, 1), "CW-1");
        Hot = NewMeter(MeterResource.HotWater, new DateOnly(2013, 1, 1), "HW-1");
        Cold.RemovedOn = new DateOnly(2014, 3, 15);
        ColdNew = NewMeter(MeterResource.ColdWater, new DateOnly(2014, 3, 15), "CW-20140");
        ColdNew.ReplacedMeterId = Cold.Id;

        var d = rule == ImportPeriodRule.ReadingWindow ? 22 : 1;
        var shift = rule == ImportPeriodRule.ReadingWindow ? 0 : 1;
        DateOnly Read(int year, int month) => new DateOnly(year, month, d).AddMonths(shift);
        AddReading(Cold, Read(2013, 12), 100m, ReadingKind.Regular);
        AddReading(Cold, Read(2014, 1), 105m, ReadingKind.Regular);
        AddReading(Cold, Read(2014, 2), 110m, ReadingKind.Regular);
        AddReading(Cold, new DateOnly(2014, 3, 15), 110m, ReadingKind.Final);
        AddReading(ColdNew, new DateOnly(2014, 3, 15), 0m, ReadingKind.Initial);
        AddReading(ColdNew, Read(2014, 3), 6m, ReadingKind.Regular);
        AddReading(ColdNew, Read(2014, 4), 12m, ReadingKind.Regular);
        AddReading(Hot, Read(2013, 12), 50m, ReadingKind.Regular);
        AddReading(Hot, Read(2014, 1), 53m, ReadingKind.Regular);
        AddReading(Hot, Read(2014, 2), 56m, ReadingKind.Regular);
        AddReading(Hot, Read(2014, 3), 59m, ReadingKind.Regular);
        AddReading(Hot, Read(2014, 4), 62m, ReadingKind.Regular);

        // 10.20 + 21% (2.14) = 12.34 LVL.
        Bills.Add(NewBill(new YearMonth(2013, 12), 12.34m, Currency.LVL, net: 10.20m, vat: 2.14m, percent: 21m, notes: "Comment: tariff changed"));
        Bills.Add(NewBill(new YearMonth(2014, 1), 13.00m, Currency.EUR));
        Bills.Add(NewBill(new YearMonth(2014, 2), 11.50m, Currency.EUR));
        Bills.Add(NewBill(new YearMonth(2014, 3), 15.00m, Currency.EUR));
        Bills.Add(NewBill(new YearMonth(2014, 4), 9.99m, Currency.EUR));
        Payments.Add(NewPayment(Bills[0], new DateOnly(2014, 1, 10)));
        Payments.Add(NewPayment(Bills[1], new DateOnly(2014, 2, 12)));

        var coldRegister = Cold.Registers[0];
        var hotRegister = Hot.Registers[0];
        Profile = new ImportProfile
        {
            SheetName = "Water",
            ContractId = Contract.Id,
            PeriodColumn = 1,
            PeriodRule = rule,
            Registers =
            [
                new RegisterColumnMapping { RegisterId = coldRegister.Id, ReadingColumn = 2 },
                new RegisterColumnMapping { RegisterId = hotRegister.Id, ReadingColumn = 3 },
            ],
            TotalColumn = 4,
            PaidColumn = 5,
            NotesColumns = [6],
            ColumnHeaders =
            [
                new ImportColumnHeader { Field = "Period", Column = "A", Header = "Month" },
                new ImportColumnHeader { Field = ImportColumnHeaders.ReadingField(coldRegister.Id), Column = "B", Header = "Cold m3" },
                new ImportColumnHeader { Field = ImportColumnHeaders.ReadingField(hotRegister.Id), Column = "C", Header = "Hot m3" },
                new ImportColumnHeader { Field = "Total", Column = "D", Header = "Total" },
                new ImportColumnHeader { Field = "Paid", Column = "E", Header = "Paid" },
                new ImportColumnHeader { Field = "Notes", Column = "F", Header = "Comment" },
            ],
        };
    }

    public Property Property { get; }

    public Supplier Supplier { get; }

    public Contract Contract { get; }

    public Meter Cold { get; }

    public Meter ColdNew { get; }

    public Meter Hot { get; }

    public List<Bill> Bills { get; } = [];

    public List<Payment> Payments { get; } = [];

    public ImportProfile Profile { get; }

    public ReportData Data() => new(
        [Property],
        [],
        [Contract],
        [
            new ContractMeter { ContractId = Contract.Id, MeterId = Cold.Id, ValidFrom = new DateOnly(2013, 1, 1), ValidTo = new DateOnly(2014, 3, 14) },
            new ContractMeter { ContractId = Contract.Id, MeterId = ColdNew.Id, ValidFrom = new DateOnly(2014, 3, 15) },
            new ContractMeter { ContractId = Contract.Id, MeterId = Hot.Id, ValidFrom = new DateOnly(2013, 1, 1) },
        ],
        [Cold, ColdNew, Hot],
        Bills,
        [],
        []);

    public WorkbookExportInput Input(bool withProfile = true) => new(
        Data(),
        Payments,
        [Supplier],
        withProfile ? [new SavedImportProfile { Name = "Water sheet", Profile = Profile }] : []);

    public IReadOnlyList<ExportSheet> Export(WorkbookExportOptions? options = null, bool withProfile = true) =>
        WorkbookExporter.Build(Input(withProfile), options ?? new WorkbookExportOptions(), Labels);

    /// <summary>A copy of the first meters without readings and replacements: what the database holds before the import.</summary>
    public IReadOnlyList<Meter> EmptyMeters()
    {
        Meter Fresh(Meter source)
        {
            var copy = new Meter { Id = source.Id, PropertyId = source.PropertyId, Resource = source.Resource, Unit = source.Unit, InstalledOn = source.InstalledOn, SerialNumber = source.SerialNumber };
            foreach (var register in source.Registers)
            {
                copy.Registers.Add(new MeterRegister { Id = register.Id, MeterId = copy.Id, Kind = register.Kind, Direction = register.Direction });
            }

            return copy;
        }

        return [Fresh(Cold), Fresh(Hot)];
    }

    private static Meter NewMeter(MeterResource resource, DateOnly installed, string serial)
    {
        var meter = new Meter { PropertyId = Guid.Empty, Resource = resource, Unit = MeterUnit.M3, InstalledOn = installed, SerialNumber = serial };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        return meter;
    }

    private static void AddReading(Meter meter, DateOnly date, decimal value, ReadingKind kind)
    {
        var register = meter.Registers[0];
        register.Readings.Add(new MeterReading { RegisterId = register.Id, Date = date, Value = value, Kind = kind });
    }

    private Bill NewBill(YearMonth period, decimal gross, Currency currency, decimal? net = null, decimal? vat = null, decimal? percent = null, string? notes = null) => new()
    {
        PropertyId = Property.Id,
        CategoryId = Contract.CategoryId,
        ContractId = Contract.Id,
        SupplierId = Supplier.Id,
        Period = period,
        Gross = gross,
        Net = net,
        Vat = vat,
        VatPercent = percent,
        Currency = currency,
        Notes = notes,
    };

    private Payment NewPayment(Bill bill, DateOnly paidOn)
    {
        var payment = new Payment { ContractId = Contract.Id, PaidOn = paidOn, Amount = bill.Gross, Currency = bill.Currency };
        payment.Allocations.Add(new PaymentAllocation { PaymentId = payment.Id, BillId = bill.Id, Amount = bill.Gross });
        return payment;
    }
}
