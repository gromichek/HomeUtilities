using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Reports;
using HomeUtilities.Core.Tariffs;
using static HomeUtilities.Core.Tests.Fakes.MeterBuilder;

namespace HomeUtilities.Core.Tests.Reports;

/// <summary>Small builders for the report tests: property "Flat" and what hangs on it.</summary>
internal sealed class ReportScenario
{
    public ReportScenario()
    {
        Properties.Add(Flat);
        Water = AddCategory("Water", 1, ServiceKind.ColdWater, nameless: true);
        Maintenance = AddCategory("Maintenance", 2, ServiceKind.Maintenance, nameless: true);
    }

    public Property Flat { get; } = new() { Name = "Flat", SortOrder = 1 };

    public Category Water { get; }

    public Category Maintenance { get; }

    public List<Property> Properties { get; } = [];

    public List<Category> Categories { get; } = [];

    public List<Contract> Contracts { get; } = [];

    public List<ContractMeter> Links { get; } = [];

    public List<Meter> Meters { get; } = [];

    public List<Bill> Bills { get; } = [];

    public List<TariffComponent> Components { get; } = [];

    public ReportData Data => new(Properties, Categories, Contracts, Links, Meters, Bills, Components, LatvianVatDefaults.Create());

    public Property AddProperty(string name, int sortOrder = 2)
    {
        var property = new Property { Name = name, SortOrder = sortOrder };
        Properties.Add(property);
        return property;
    }

    public Category AddCategory(string name, int sortOrder, ServiceKind? key = null, bool nameless = false)
    {
        var category = new Category { Name = nameless ? null : name, SystemKey = key, SortOrder = sortOrder };
        Categories.Add(category);
        return category;
    }

    public Contract AddContract(
        string title,
        Category? category = null,
        Property? property = null,
        BillingMode mode = BillingMode.Actual,
        ServiceKind service = ServiceKind.ColdWater)
    {
        var contract = new Contract
        {
            PropertyId = (property ?? Flat).Id,
            Title = title,
            Service = service,
            CategoryId = (category ?? Water).Id,
            ValidFrom = new DateOnly(2020, 1, 1),
            BillingMode = mode,
        };
        Contracts.Add(contract);
        return contract;
    }

    public Bill AddBill(Contract? contract, decimal gross, YearMonth period, Category? category = null, Property? property = null, string? title = null)
    {
        var bill = new Bill
        {
            PropertyId = contract?.PropertyId ?? (property ?? Flat).Id,
            CategoryId = contract?.CategoryId ?? (category ?? Maintenance).Id,
            ContractId = contract?.Id,
            Period = period,
            Gross = gross,
            Kind = contract is null ? BillKind.OneOff : BillKind.Regular,
            Title = title,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        Bills.Add(bill);
        return bill;
    }

    /// <summary>A cold water meter of the flat (installed 2020-01-01) with total readings; linked to the contract if given.</summary>
    public Meter AddMeter(Contract? contract, params (DateOnly Date, decimal Value)[] readings)
    {
        var meter = Meter(new DateOnly(2020, 1, 1), 6);
        meter.PropertyId = Flat.Id;
        foreach (var (date, value) in readings)
        {
            meter.Add(date, value);
        }

        Meters.Add(meter);
        if (contract is not null)
        {
            Links.Add(new ContractMeter { ContractId = contract.Id, MeterId = meter.Id, ValidFrom = new DateOnly(2020, 1, 1) });
        }

        return meter;
    }

    /// <summary>A per-unit component of the contract with a net price per unit.</summary>
    public TariffComponent AddPerUnit(Contract contract, string name, decimal price)
    {
        var component = new TariffComponent
        {
            ContractId = contract.Id,
            Name = name,
            ChargeKind = contract.Service,
            Basis = ChargeBasis.PerUnit,
            VatCode = "LV-STANDARD",
        };
        component.Rates.Add(new TariffRate { ComponentId = component.Id, ValidFrom = new DateOnly(2026, 1, 1), Price = price, Currency = Currency.EUR });
        Components.Add(component);
        return component;
    }

    public static ReportFilter Filter(YearMonth from, YearMonth to, Action<ReportFilterBuilder>? configure = null)
    {
        var builder = new ReportFilterBuilder(from, to);
        configure?.Invoke(builder);
        return builder.Build();
    }
}

internal sealed class ReportFilterBuilder(YearMonth from, YearMonth to)
{
    public IReadOnlyCollection<Guid>? PropertyIds { get; set; }

    public IReadOnlyCollection<Guid>? CategoryIds { get; set; }

    public ReportRows Rows { get; set; } = ReportRows.Months;

    public bool YearOverYear { get; set; }

    public CostGrouping Grouping { get; set; } = CostGrouping.Category;

    public ReportMeasure Measure { get; set; } = ReportMeasure.Gross;

    public bool PerMeter { get; set; }

    public ReportFilter Build() =>
        new(from, to, PropertyIds, CategoryIds, Rows, YearOverYear, Grouping, Measure, PerMeter);
}
