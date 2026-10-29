using CommunityToolkit.Mvvm.ComponentModel;

namespace HomeUtilities.ViewModels;

/// <summary>A sheet column the owner can choose; <see cref="Index"/> 0 is "not used".</summary>
/// <param name="Index">1-based column number, 0 = none.</param>
/// <param name="Text">Letter and header text, e.g. "D — Total".</param>
public sealed record ImportColumnOption(int Index, string Text);

/// <summary>The choice of one column for one target field of the import (period, total, a register's readings…).</summary>
public sealed partial class ImportColumnPickerViewModel : ObservableObject
{
    [ObservableProperty]
    private IReadOnlyList<ImportColumnOption> _options;

    [ObservableProperty]
    private ImportColumnOption? _selected;

    /// <summary>Creates a picker.</summary>
    /// <param name="label">The field name shown above the picker.</param>
    /// <param name="options">All columns including "not used" first.</param>
    /// <param name="column">The initial column; 0 = none.</param>
    public ImportColumnPickerViewModel(string label, IReadOnlyList<ImportColumnOption> options, int column)
    {
        Label = label;
        _options = options;
        _selected = options.FirstOrDefault(o => o.Index == column) ?? options.FirstOrDefault();
    }

    /// <summary>A picker with no columns, used before a sheet is loaded.</summary>
    public static ImportColumnPickerViewModel Unmapped => new(string.Empty, [new ImportColumnOption(0, string.Empty)], 0);

    /// <summary>The field name.</summary>
    public string Label { get; set; }

    /// <summary>The chosen column number; 0 = none.</summary>
    public int Column => Selected?.Index ?? 0;

    /// <summary>Replaces the options (the header texts changed) and keeps the chosen column.</summary>
    public void SetOptions(IReadOnlyList<ImportColumnOption> options)
    {
        var column = Column;
        Options = options;
        Selected = options.FirstOrDefault(o => o.Index == column) ?? options.FirstOrDefault();
    }

    /// <summary>Selects a column.</summary>
    public void Select(int column) => Selected = Options.FirstOrDefault(o => o.Index == column) ?? Options.FirstOrDefault();
}

/// <summary>The reading and date columns of one meter register.</summary>
public sealed class ImportRegisterMappingViewModel(Guid registerId, string label, ImportColumnPickerViewModel reading, ImportColumnPickerViewModel date)
{
    /// <summary>The register.</summary>
    public Guid RegisterId { get; } = registerId;

    /// <summary>Meter and register name.</summary>
    public string Label { get; } = label;

    /// <summary>Column with the dial values.</summary>
    public ImportColumnPickerViewModel Reading { get; } = reading;

    /// <summary>Column with the reading dates.</summary>
    public ImportColumnPickerViewModel Date { get; } = date;
}

/// <summary>One row of the sheet preview of the mapping step: the row number and the cell texts.</summary>
public sealed record ImportSheetPreviewRow(string RowNumber, IReadOnlyList<string> Cells);
