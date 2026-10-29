using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Import;

namespace HomeUtilities.Import.Tests;

/// <summary>Contract, meters and profiles that fit <see cref="SyntheticWorkbook"/>.</summary>
public sealed class ImportFixtures
{
    public ImportFixtures(BillingMode mode = BillingMode.Actual)
    {
        Contract = new Contract
        {
            PropertyId = Guid.NewGuid(),
            SupplierId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Title = "Test contract",
            ValidFrom = new DateOnly(2000, 1, 1),
            BillingMode = mode,
        };
    }

    public Contract Contract { get; }

    public Meter Cold { get; } = NewMeter(MeterResource.ColdWater);

    public Meter Hot { get; } = NewMeter(MeterResource.HotWater);

    public Meter Power { get; } = NewMeter(MeterResource.Electricity);

    public Meter Gas { get; } = NewMeter(MeterResource.Gas);

    public MeterRegister ColdRegister => Cold.Registers[0];

    public MeterRegister HotRegister => Hot.Registers[0];

    public MeterRegister PowerRegister => Power.Registers[0];

    public MeterRegister GasRegister => Gas.Registers[0];

    public ImportContext Context(params Meter[] meters) => new(Contract, meters, []);

    public ImportProfile WaterProfile() => new()
    {
        SheetName = SyntheticWorkbook.Water,
        ContractId = Contract.Id,
        PeriodColumn = 1,
        Registers =
        [
            new RegisterColumnMapping { RegisterId = ColdRegister.Id, ReadingColumn = 2 },
            new RegisterColumnMapping { RegisterId = HotRegister.Id, ReadingColumn = 3 },
        ],
        TotalColumn = 4,
        PaidColumn = 5,
        NotesColumns = [6],
    };

    public ImportProfile HeatProfile() => new()
    {
        SheetName = SyntheticWorkbook.Heat,
        ContractId = Contract.Id,
        PeriodColumn = 1,
        Registers = [new RegisterColumnMapping { RegisterId = HotRegister.Id, ReadingColumn = 2 }],
        NetColumn = 3,
        VatPercentColumn = 4,
        TotalColumn = 5,
        PaidColumn = 6,
        NotesColumns = [7],
    };

    public ImportProfile WasteProfile() => new()
    {
        SheetName = SyntheticWorkbook.Waste,
        ContractId = Contract.Id,
        PeriodColumn = 1,
        NetColumn = 2,
        VatColumn = 3,
        TotalColumn = 4,
        PaidColumn = 5,
        PaidDateColumn = 6,
    };

    public ImportProfile ElectricityProfile() => new()
    {
        SheetName = SyntheticWorkbook.Electricity,
        ContractId = Contract.Id,
        PeriodColumn = 1,
        Registers = [new RegisterColumnMapping { RegisterId = PowerRegister.Id, ReadingColumn = 2, DateColumn = 3 }],
        TotalColumn = 4,
    };

    public ImportProfile PropaneProfile() => new()
    {
        SheetName = SyntheticWorkbook.Propane,
        ContractId = Contract.Id,
        PeriodColumn = 1,
        Registers = [new RegisterColumnMapping { RegisterId = GasRegister.Id, ReadingColumn = 2 }],
        TotalColumn = 3,
    };

    private static Meter NewMeter(MeterResource resource)
    {
        var meter = new Meter
        {
            PropertyId = Guid.NewGuid(),
            Resource = resource,
            Unit = resource == MeterResource.Electricity ? MeterUnit.KWh : MeterUnit.M3,
            InstalledOn = new DateOnly(2000, 1, 1),
        };
        meter.Registers.Add(MeterRegister.CreateDefault(meter.Id));
        return meter;
    }
}
