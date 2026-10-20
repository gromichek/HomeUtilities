using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Tariffs;

namespace HomeUtilities.Core.Tests.Charges;

/// <summary>Builds synthetic charge inputs for tests.</summary>
internal sealed class ChargeBuilder
{
    public Contract Contract { get; } = new()
    {
        PropertyId = Guid.CreateVersion7(),
        SupplierId = Guid.CreateVersion7(),
        Title = "Test contract",
        ValidFrom = new(2020, 1, 1),
        Currency = Currency.EUR,
    };

    public Property Property { get; } = new() { Name = "Test flat" };

    public List<TariffComponent> Components { get; } = [];

    public List<ContractMeter> Links { get; } = [];

    public List<Meter> Meters { get; } = [];

    public List<VatRate> Vat { get; } = [.. LatvianVatDefaults.Create()];

    public ChargeBuilder Window(int from, int to)
    {
        Contract.ReadingDayFrom = from;
        Contract.ReadingDayTo = to;
        return this;
    }

    public Meter AddMeter(Meter meter, bool link = true)
    {
        Meters.Add(meter);
        if (link)
        {
            Link(meter);
        }

        return meter;
    }

    public void Link(Meter meter, DateOnly? validTo = null) =>
        Links.Add(new ContractMeter { ContractId = Contract.Id, MeterId = meter.Id, ValidFrom = new(2020, 1, 1), ValidTo = validTo });

    public void AddParameter(PropertyParameterKind kind, DateOnly validFrom, decimal value, string? name = null) =>
        Property.Parameters.Add(new PropertyParameter { PropertyId = Property.Id, Kind = kind, Name = name, ValidFrom = validFrom, Value = value });

    public TariffComponent Add(
        string name,
        ChargeBasis basis,
        decimal price,
        string vatCode = "LV-STANDARD",
        bool includesVat = false,
        int sortOrder = 0,
        params RegisterKind[] registers)
    {
        var component = new TariffComponent
        {
            ContractId = Contract.Id,
            Name = name,
            ChargeKind = ServiceKind.ColdWater,
            Basis = basis,
            VatCode = vatCode,
            SortOrder = sortOrder,
            RegisterKinds = [.. registers],
        };
        component.Rates.Add(Rate(component, new(2026, 1, 1), price, includesVat));
        Components.Add(component);
        return component;
    }

    public static TariffRate Rate(TariffComponent component, DateOnly validFrom, decimal price, bool includesVat = false) =>
        new() { ComponentId = component.Id, ValidFrom = validFrom, Price = price, PriceIncludesVat = includesVat, Currency = Currency.EUR };

    public CalculatedCharge Calculate(YearMonth period) =>
        ChargeCalculator.Calculate(new ChargeInput(Contract, Property, Components, Links, Meters, Vat, period));
}
