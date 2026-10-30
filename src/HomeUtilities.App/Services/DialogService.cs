using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using HomeUtilities.App.Views;
using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Services;

public sealed class DialogService(ILocalizer localizer, ILayoutModeService layoutMode) : IDialogService
{
    private const double PopupWidth = 480;

    // Choice dialogs (UI_GUIDELINES §3a): fixed width, height from the row count.
    private const double ChoicePopupWidth = 420;
    private const double ChoiceRowHeight = 50;
    private const double ChoiceChromeHeight = 130;
    private const double ChoiceMaxHeight = 560;

    /// <summary>Height of the multi-step wizard popup; its Grid needs a definite height so the step content scrolls.</summary>
    private const double WizardPopupHeight = 560;

    public Task AlertAsync(string title, string message) =>
        Page.DisplayAlertAsync(title, message, localizer["Dialog_Ok"]);

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Page.DisplayAlertAsync(title, message, accept, cancel);

    public async Task<string?> ChooseAsync(string title, IReadOnlyList<string> options)
    {
        var cancel = localizer["Cancel"];
        var result = await Page.DisplayActionSheetAsync(title, cancel, null, [.. options]);
        return result is null || result == cancel ? null : result;
    }

    // An action sheet gives three buttons on both platforms; dismissing it is the same as Cancel.
    public async Task<UnsavedChangesChoice> AskUnsavedChangesAsync()
    {
        var save = localizer["Dialog_Unsaved_Save"];
        var dontSave = localizer["Dialog_Unsaved_DontSave"];
        var result = await Page.DisplayActionSheetAsync(localizer["Dialog_Unsaved_Title"], localizer["Dialog_Unsaved_Cancel"], dontSave, save);
        return result == save
            ? UnsavedChangesChoice.Save
            : result == dontSave ? UnsavedChangesChoice.DontSave : UnsavedChangesChoice.Cancel;
    }

    public async Task<bool> ShowEditorAsync(EditorDialogViewModel editor)
    {
        var view = CreateView(editor);
        if (layoutMode.Current == LayoutMode.Compact)
        {
            var modal = new EditorDialogPage(view, editor);
            Action close = () => _ = modal.Navigation.PopModalAsync();
            editor.CloseRequested += close;
            try
            {
                await Page.Navigation.PushModalAsync(modal);
                await modal.Closed;
            }
            finally
            {
                editor.CloseRequested -= close;
            }
        }
        else
        {
            var host = Page;
            view.WidthRequest = PopupWidth;
            if (editor is IChoiceDialog choice)
            {
                // The list scrolls inside a popup of a definite height: just enough for the rows, at most 70 % of the window.
                var wanted = ChoiceChromeHeight + (choice.RowCountHint * ChoiceRowHeight);
                var limit = host.Height > 0 ? host.Height * 0.7 : ChoiceMaxHeight;
                view.WidthRequest = ChoicePopupWidth;
                view.HeightRequest = Math.Min(wanted, limit);
            }

            if (view is ReplaceMeterWizardView)
            {
                view.HeightRequest = host.Height > 0 ? Math.Min(WizardPopupHeight, host.Height * 0.9) : WizardPopupHeight;
            }

            Action close = () => _ = host.ClosePopupAsync();
            editor.CloseRequested += close;
            try
            {
                await host.ShowPopupAsync(view, new PopupOptions { CanBeDismissedByTappingOutsideOfPopup = false });
            }
            finally
            {
                editor.CloseRequested -= close;
            }
        }

        return editor.Saved;
    }

    // One view per dialog view model; add a case with every new child-item dialog.
    private static View CreateView(EditorDialogViewModel editor) => editor switch
    {
        ParameterEditorViewModel => new ParameterEditorView { BindingContext = editor },
        LinkEditorViewModel => new LinkEditorView { BindingContext = editor },
        CustomFieldEditorViewModel => new CustomFieldEditorView { BindingContext = editor },
        RegisterEditorViewModel => new RegisterEditorView { BindingContext = editor },
        MeterLinkEditorViewModel => new MeterLinkEditorView { BindingContext = editor },
        ReadingEditorViewModel => new ReadingEditorView { BindingContext = editor },
        BillLineEditorViewModel => new BillLineEditorView { BindingContext = editor },
        AllocationAmountEditorViewModel => new AllocationAmountEditorView { BindingContext = editor },
        TariffRateEditorViewModel => new TariffRateEditorView { BindingContext = editor },
        VatVersionEditorViewModel => new VatVersionEditorView { BindingContext = editor },
        EqualizedPlanEditorViewModel => new EqualizedPlanEditorView { BindingContext = editor },
        TaxYearEditorViewModel => new TaxYearEditorView { BindingContext = editor },
        ReplaceMeterWizardViewModel => new ReplaceMeterWizardView { BindingContext = editor },
        MultiSelectDialogViewModel => new MultiSelectDialogView { BindingContext = editor },
        ImportRowOverrideEditorViewModel => new ImportRowOverrideEditorView { BindingContext = editor },
        ReportPeriodDialogViewModel => new ReportPeriodDialogView { BindingContext = editor },
        SingleChoiceDialogViewModel => new SingleChoiceDialogView { BindingContext = editor },
        _ => throw new NotSupportedException($"No dialog view is registered for {editor.GetType().Name}."),
    };

    private static Page Page =>
        Shell.Current?.CurrentPage ?? Application.Current?.Windows.FirstOrDefault()?.Page
        ?? throw new InvalidOperationException("No page is available to show a dialog.");
}
