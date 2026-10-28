using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;

namespace HomeUtilities.Core.Reports;

/// <summary>
/// Costs per month: every bill is spread over the months of its service period (a bill without one counts in its period), in
/// EUR. Equalized bills count as billed, a settlement counts in its month, one-off expenses are included.
/// </summary>
internal static class CostsReport
{
    private sealed record Group(ReportColumn Column, string Sort, Dictionary<YearMonth, Acc> Cells, Guid? CategoryId, Guid? ContractId);

    public static ReportModel Create(ReportData data, ReportFilter filter, IReadOnlyList<Property> properties)
    {
        var measure = filter.Measure is ReportMeasure.Net or ReportMeasure.Vat ? filter.Measure : ReportMeasure.Gross;
        var model = new ReportModel(ReportKind.Costs, filter)
        {
            HasRowTotals = true,
            ToCell = (_, acc) => new ReportCell(acc.Any ? acc.A : null, acc.Approx),
        };

        var categories = data.Categories.ToDictionary(c => c.Id);
        var contracts = data.Contracts.ToDictionary(c => c.Id);
        var order = properties.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var groups = new Dictionary<string, Group>();

        foreach (var bill in data.Bills.Where(b => !b.IsDraft))
        {
            if (!order.ContainsKey(bill.PropertyId) || (filter.CategoryIds is { Count: > 0 } ids && !ids.Contains(bill.CategoryId)))
            {
                continue;
            }

            foreach (var share in ReportMoney.Shares(bill).Where(s => filter.Contains(s.Month)))
            {
                var group = GroupOf(groups, properties[order[bill.PropertyId]], bill, filter.Grouping, measure, categories, contracts);
                group.Cells.TryGetValue(share.Month, out var acc);
                acc.A += share.Of(measure);
                acc.Any = true;
                acc.Approx |= share.IsSpread;
                group.Cells[share.Month] = acc;
            }
        }

        var ordered = groups.Values.OrderBy(g => order[g.Column.PropertyId]).ThenBy(g => g.Sort, StringComparer.Ordinal).ToList();
        var propertiesWithColumns = ordered.Select(g => g.Column.PropertyId).Distinct().Count();
        foreach (var perProperty in ordered.GroupBy(g => g.Column.PropertyId))
        {
            var members = perProperty.ToList();
            foreach (var group in members)
            {
                model.Add(group.Column, group.Cells, Drill(group));
            }

            // A subtotal per property when several properties are shown and the property has several columns.
            if (propertiesWithColumns > 1 && members.Count > 1)
            {
                var first = members[0].Column;
                var sum = new Dictionary<YearMonth, Acc>();
                foreach (var cells in members.Select(m => m.Cells))
                {
                    foreach (var (month, acc) in cells)
                    {
                        sum[month] = sum.TryGetValue(month, out var known) ? known + acc : acc;
                    }
                }

                var subtotal = first with { Key = $"{first.PropertyId}|total", Subject = ColumnSubject.Property, Name = null, Service = null, IsSubtotal = true };
                model.Add(subtotal, sum, (from, to) => new ReportDrill(DrillTarget.Bills, subtotal.PropertyId, from, to));
            }
        }

        return model;
    }

    private static Func<YearMonth, YearMonth, ReportDrill?> Drill(Group group) =>
        (from, to) => new ReportDrill(DrillTarget.Bills, group.Column.PropertyId, from, to, group.CategoryId, group.ContractId);

    private static Group GroupOf(
        Dictionary<string, Group> groups,
        Property property,
        Bill bill,
        CostGrouping grouping,
        ReportMeasure measure,
        Dictionary<Guid, Category> categories,
        Dictionary<Guid, Contract> contracts)
    {
        categories.TryGetValue(bill.CategoryId, out var category);
        var contract = bill.ContractId is { } contractId && contracts.TryGetValue(contractId, out var found) ? found : null;
        var service = contract?.Service ?? category?.SystemKey ?? ServiceKind.Other;

        string key;
        string sort;
        ReportColumn column;
        Guid? categoryId = null;
        Guid? contractFilter = null;
        switch (grouping)
        {
            case CostGrouping.Service:
                key = $"{property.Id}|service|{service}";
                sort = $"{(int)service:D3}";
                column = ReportBuilder.Column(key, property, ColumnSubject.Service, measure, ReportValueKind.Money, service: service);
                break;
            case CostGrouping.Contract when contract is not null:
                key = $"{property.Id}|contract|{contract.Id}";
                sort = "0" + contract.Title;
                column = ReportBuilder.Column(key, property, ColumnSubject.Contract, measure, ReportValueKind.Money, contract.Title, contract.Service);
                contractFilter = contract.Id;
                break;
            case CostGrouping.Contract:
                key = $"{property.Id}|oneoff";
                sort = "1";
                column = ReportBuilder.Column(key, property, ColumnSubject.OneOff, measure, ReportValueKind.Money);
                break;
            case CostGrouping.Property:
                key = $"{property.Id}|property";
                sort = string.Empty;
                column = ReportBuilder.Column(key, property, ColumnSubject.Property, measure, ReportValueKind.Money);
                break;
            default:
                key = $"{property.Id}|category|{bill.CategoryId}";
                sort = $"{category?.SortOrder ?? int.MaxValue:D10}{category?.Name}";
                column = ReportBuilder.Column(key, property, ColumnSubject.Category, measure, ReportValueKind.Money, category?.Name, category?.SystemKey, categoryId: bill.CategoryId);
                categoryId = bill.CategoryId;
                break;
        }

        if (!groups.TryGetValue(key, out var group))
        {
            group = new Group(column, sort, [], categoryId, contractFilter);
            groups[key] = group;
        }

        return group;
    }
}
