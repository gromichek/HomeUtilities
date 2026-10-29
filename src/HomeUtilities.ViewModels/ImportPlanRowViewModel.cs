using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Import;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One issue line of a planned row.</summary>
/// <param name="Text">Localized message.</param>
/// <param name="IsError">True for an error (the row is skipped), false for a warning.</param>
/// <param name="IsInfo">True for a note that is neither (e.g. which fallback column was used).</param>
public sealed record ImportIssueLine(string Text, bool IsError, bool IsInfo = false);

/// <summary>One sheet row of the dry run: what is read from it, what will be created, and the issues.</summary>
public sealed class ImportPlanRowViewModel
{
    /// <summary>Creates the row view from a planned row.</summary>
    /// <param name="row">The planned row.</param>
    /// <param name="localizer">Texts.</param>
    /// <param name="registerLabels">Short labels of the registers, by register id.</param>
    /// <param name="editPeriod">Opens the dialog that changes the period of the row; null = not offered.</param>
    public ImportPlanRowViewModel(PlannedRow row, ILocalizer localizer, IReadOnlyDictionary<Guid, string> registerLabels, Func<PlannedRow, Task>? editPeriod = null)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(localizer);

        Row = row;
        HeaderText = row.Period is { } period
            ? localizer.Get("ImportRowHeader", row.RowNumber, period.ToString())
            : localizer.Get("ImportRowHeaderNoPeriod", row.RowNumber);
        Issues = [.. row.Issues.Select(i => new ImportIssueLine(ValidationText.For(localizer, new ValidationError(i.Code, null, [.. i.Args])), i.Severity == ImportIssueSeverity.Error, i.Severity == ImportIssueSeverity.Info))];

        var actions = new List<string>();
        foreach (var reading in row.Readings)
        {
            var label = registerLabels.GetValueOrDefault(reading.RegisterId, string.Empty);
            actions.Add(localizer.Get("ImportActionReading", label, Number(reading.Reading.Value), reading.Reading.Date.ToString("d", CultureInfo.CurrentCulture)));
        }

        if (row.Bill is { } bill)
        {
            actions.Add(localizer.Get("ImportActionBill", Number(bill.Gross), bill.Currency));
        }

        if (row.Payment is { } payment)
        {
            actions.Add(localizer.Get("ImportActionPayment", Number(payment.Amount), payment.Currency, payment.PaidOn.ToString("d", CultureInfo.CurrentCulture)));
        }

        if (row.IsSkipped)
        {
            actions.Add(localizer["ImportActionSkipped"]);
        }

        Actions = actions;
        EditPeriodText = localizer["ImportEditPeriod"];
        CanEditPeriod = editPeriod is not null && row.RowDate is not null;
        EditPeriodCommand = new AsyncRelayCommand(() => editPeriod?.Invoke(row) ?? Task.CompletedTask);
    }

    /// <summary>Text of the button that changes the period of the row.</summary>
    public string EditPeriodText { get; }

    /// <summary>True when the row has a date, so that its period can be corrected.</summary>
    public bool CanEditPeriod { get; }

    /// <summary>Opens the dialog for the period of the row.</summary>
    public IAsyncRelayCommand EditPeriodCommand { get; }

    /// <summary>The planned row.</summary>
    public PlannedRow Row { get; }

    /// <summary>"Row 5 · 2014-01".</summary>
    public string HeaderText { get; }

    /// <summary>What will be created (or "Skipped").</summary>
    public IReadOnlyList<string> Actions { get; }

    /// <summary>The issues.</summary>
    public IReadOnlyList<ImportIssueLine> Issues { get; }

    /// <summary>True when the row has errors.</summary>
    public bool HasErrors => Row.HasErrors;

    /// <summary>True when the row has warnings and no errors.</summary>
    public bool HasWarnings => Row.HasWarnings;

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.CurrentCulture);
}
