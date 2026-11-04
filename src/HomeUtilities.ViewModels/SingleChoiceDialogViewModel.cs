using CommunityToolkit.Mvvm.Input;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One option of a <see cref="SingleChoiceDialogViewModel"/>.</summary>
/// <param name="Value">What the option stands for.</param>
/// <param name="Text">The display name.</param>
/// <param name="Description">One-line explanation; empty = none.</param>
public readonly record struct SingleChoiceOption(object Value, string Text, string Description = "");

/// <summary>Implemented by the choice dialogs so the host can size a desktop popup to its list.</summary>
public interface IChoiceDialog
{
    /// <summary>Roughly how many list rows the dialog shows (used to size the popup).</summary>
    int RowCountHint { get; }
}

/// <summary>
/// A dialog with one radio row per option (and an optional explanation under each) and OK / Cancel. Reusable for any
/// "pick one of a few" choice, e.g. the report of the Reports screen.
/// </summary>
public sealed partial class SingleChoiceDialogViewModel : EditorDialogViewModel, IChoiceDialog
{
    /// <summary>Creates the dialog; <paramref name="selected"/> is the value marked at the start.</summary>
    public SingleChoiceDialogViewModel(ILocalizer localizer, string title, IEnumerable<SingleChoiceOption> options, object? selected, string message = "")
        : base(localizer)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(options);

        Title = title;
        Message = message ?? string.Empty;
        OkText = localizer["Dialog_Ok"];
        Items =
        [
            .. options.Select(o => new ReportOptionViewModel(o.Text, Equals(o.Value, selected), Select)
            {
                Value = o.Value,
                Description = o.Description,
            }),
        ];
    }

    /// <summary>
    /// An explanation shown as a wrapping text block above the choices (empty = none). Long texts scroll with the list; the
    /// system action sheet clips them (T-032b).
    /// </summary>
    public string Message { get; }

    /// <summary>True when <see cref="Message"/> is shown.</summary>
    public bool HasMessage => Message.Length > 0;

    /// <summary>The radio rows.</summary>
    public IReadOnlyList<ReportOptionViewModel> Items { get; }

    /// <summary>Text of the confirm button.</summary>
    public string OkText { get; }

    /// <summary>The marked value; meaningful after the dialog was confirmed.</summary>
    public object? SelectedValue => Items.FirstOrDefault(i => i.IsSelected)?.Value;

    /// <inheritdoc />
    public int RowCountHint => (Items.Count * (Items.Any(i => i.HasDescription) ? 2 : 1)) + MessageRows;

    // About 45 characters per line in a 420 wide popup, a text line is roughly half a list row high.
    private int MessageRows => HasMessage ? (int)Math.Ceiling(Message.Length / 45.0 / 2) : 0;

    protected override bool TryApply() => SelectedValue is not null;

    [RelayCommand]
    private void Confirm()
    {
        if (TryApply())
        {
            CloseSaved();
        }
    }

    private void Select(ReportOptionViewModel chosen)
    {
        foreach (var item in Items)
        {
            item.IsSelected = ReferenceEquals(item, chosen);
        }
    }
}

/// <summary>Choice dialogs with an explanation, built on <see cref="SingleChoiceDialogViewModel"/> (UI_GUIDELINES §3a).</summary>
public static class DialogServiceChoiceExtensions
{
    /// <summary>
    /// Shows the app's own choice dialog (title, wrapping explanation, radio list, Cancel / OK) and returns the value of the
    /// picked option, or null when the user cancelled.
    /// </summary>
    public static async Task<object?> ChooseWithMessageAsync(
        this IDialogService dialogs,
        ILocalizer localizer,
        string title,
        string message,
        IEnumerable<SingleChoiceOption> options,
        object? selected)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        var dialog = new SingleChoiceDialogViewModel(localizer, title, options, selected, message);
        return await dialogs.ShowEditorAsync(dialog) ? dialog.SelectedValue : null;
    }
}
