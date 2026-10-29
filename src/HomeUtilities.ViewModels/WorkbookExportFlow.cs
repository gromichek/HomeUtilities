using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Export;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// "Export to Excel" on the Imports screen: asks which contracts, which period and which currency, builds the workbook like the
/// owner's own (one sheet per contract, laid out by the import profile) and hands it to the save dialog.
/// </summary>
public sealed class WorkbookExportFlow(
    IReportDataLoader loader,
    IPaymentStore payments,
    ISupplierStore suppliers,
    IImportProfileStore profiles,
    IExportWorkbookWriter writer,
    IFileSaveService saver,
    IDialogService dialogs,
    ISnackbarService snackbar,
    ILocalizer localizer,
    IClock clock)
{
    /// <summary>Media type of an .xlsx file.</summary>
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>The suggested file name: <c>HomeUtilities-export-YYYY-MM-DD.xlsx</c>.</summary>
    public static string FileName(DateOnly today) => $"HomeUtilities-export-{today:yyyy-MM-dd}.xlsx";

    /// <summary>Runs the dialogs and saves the file; does nothing when the owner cancels one of the dialogs.</summary>
    public async Task RunAsync()
    {
        var data = await loader.LoadAsync();
        var title = localizer["ExportExcel"];
        var supplierList = await suppliers.ListAsync();
        var contracts = data.Contracts.Where(c => !c.IsDeleted).ToList();
        if (contracts.Count == 0)
        {
            await dialogs.AlertAsync(title, localizer["ExportNothing"]);
            return;
        }

        var propertyNames = data.Properties.ToDictionary(p => p.Id, p => p.Name);
        var supplierNames = supplierList.ToDictionary(s => s.Id, s => s.Name);
        var order = data.Properties.Select((p, i) => (p.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var choices = contracts
            .OrderBy(c => order.GetValueOrDefault(c.PropertyId, int.MaxValue))
            .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
            .Select(c => new MultiSelectItemViewModel(
                c.Id,
                $"{propertyNames.GetValueOrDefault(c.PropertyId, "—")} · {supplierNames.GetValueOrDefault(c.SupplierId, "—")} {c.Title}".Trim(),
                string.Empty,
                isSelected: true))
            .ToList();

        var contractDialog = new MultiSelectDialogViewModel(localizer, localizer["ExportContractsTitle"], choices, choices.Select(c => c.Id).ToList());
        if (!await dialogs.ShowEditorAsync(contractDialog))
        {
            return;
        }

        var chosen = contractDialog.SelectedIds;
        if (await AskPeriodAsync(data) is not var (from, to))
        {
            return;
        }

        var currencyDialog = new SingleChoiceDialogViewModel(
            localizer,
            localizer["ExportCurrencyTitle"],
            [
                new SingleChoiceOption(false, localizer["ExportCurrencyStored"], localizer["ExportCurrencyStoredHint"]),
                new SingleChoiceOption(true, localizer["ExportCurrencyEur"], localizer["ExportCurrencyEurHint"]),
            ],
            false);
        if (!await dialogs.ShowEditorAsync(currencyDialog))
        {
            return;
        }

        var options = new WorkbookExportOptions(
            chosen.Count == contracts.Count ? null : chosen,
            from,
            to,
            currencyDialog.SelectedValue is true);
        var input = new WorkbookExportInput(data, await payments.ListAsync(), supplierList, await profiles.ListAsync());
        var sheets = WorkbookExporter.Build(input, options, Labels());
        if (sheets.Count == 0)
        {
            await dialogs.AlertAsync(title, localizer["ExportNothing"]);
            return;
        }

        var result = await saver.SaveAsync(FileName(clock.Today), ContentType, "Exports", stream =>
        {
            writer.Write(sheets, stream);
            return Task.CompletedTask;
        });
        if (result.Saved)
        {
            snackbar.Notify(result.Location is { } location ? localizer.Get("ExportSavedTo", location) : localizer["ExportSaved"]);
        }
    }

    // All = no limits; the other presets and a custom range limit the months. Null = cancelled.
    private async Task<(YearMonth? From, YearMonth? To)?> AskPeriodAsync(Core.Reports.ReportData data)
    {
        var current = YearMonth.From(clock.Today);
        var (all, allTo) = ReportPeriods.Resolve(ReportPreset.All, clock.Today, data.EarliestMonth, data.LatestMonth);
        var (earliest, latest) = ReportPeriodDialogViewModel.PickerRange(data.EarliestMonth, data.LatestMonth, current);
        var dialog = new ReportPeriodDialogViewModel(localizer, ReportPreset.All, all, allTo, earliest, latest);
        if (!await dialogs.ShowEditorAsync(dialog))
        {
            return null;
        }

        if (dialog.Preset == ReportPreset.All)
        {
            return (null, null);
        }

        var (from, to) = dialog.Preset == ReportPreset.Custom
            ? dialog.Range
            : ReportPeriods.Resolve(dialog.Preset, clock.Today, data.EarliestMonth, data.LatestMonth);
        return (from, to);
    }

    private ExportLabels Labels()
    {
        var labels = new ReportLabels(localizer);
        return new ExportLabels(
            localizer["ExportColDate"],
            localizer["ExportColConsumption"],
            localizer["ExportColNet"],
            localizer["ExportColVatPercent"],
            localizer["ExportColVat"],
            localizer["ExportColCalculated"],
            localizer["ExportColBillTotal"],
            localizer["ExportColDifference"],
            localizer["ExportColPaid"],
            localizer["ExportColPaidDate"],
            localizer["ExportColNotes"],
            localizer["ExportColCurrency"],
            localizer["ExportTotalSheet"],
            localizer["ExportTotalMonth"],
            localizer["ExportTotalColumn"],
            localizer["ExportOneOff"],
            (meter, register) =>
            {
                var text = $"{localizer["Enum_MeterResource_" + meter.Resource]}, {labels.UnitName(meter.Unit)}";
                return register.Kind == RegisterKind.Total && string.IsNullOrWhiteSpace(register.Name)
                    ? text
                    : $"{text} ({(string.IsNullOrWhiteSpace(register.Name) ? localizer["Enum_RegisterKind_" + register.Kind] : register.Name)})";
            });
    }
}
