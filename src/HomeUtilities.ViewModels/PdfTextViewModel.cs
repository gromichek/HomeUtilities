using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>
/// "Show PDF text": the text extracted from a PDF, read-only and selectable, with Copy. This is how the owner sends the text of
/// a new or changed bill to whoever writes its layout; the text contains personal data.
/// </summary>
public sealed partial class PdfTextViewModel : ViewModelBase
{
    private readonly ILocalizer _localizer;
    private readonly IClipboardService _clipboard;
    private readonly ISnackbarService _snackbar;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string _hintText = string.Empty;

    [ObservableProperty]
    private string _copyText = string.Empty;

    [ObservableProperty]
    private string _helpText = string.Empty;

    public PdfTextViewModel(ILocalizer localizer, IClipboardService clipboard, ISnackbarService snackbar, INavigationService navigation)
    {
        _localizer = localizer;
        _clipboard = clipboard;
        _snackbar = snackbar;
        _navigation = navigation;
        RefreshTexts();
    }

    /// <summary>Shows the text of a PDF.</summary>
    public void Show(string text) => Text = text;

    /// <summary>Puts the whole text on the clipboard.</summary>
    [RelayCommand]
    private Task CopyAsync() => RunSafeAsync(async () =>
    {
        await _clipboard.SetTextAsync(Text);
        _snackbar.Notify(_localizer["PdfTextCopied"]);
    });

    /// <summary>The "?" button: the guide section about filling a bill from a PDF.</summary>
    [RelayCommand]
    private Task OpenHelpAsync() => RunSafeAsync(() => HelpNavigation.OpenAsync(_navigation, HelpTopic.BillFromPdf));

    protected override void RefreshTexts()
    {
        Title = _localizer["PdfTextTitle"];
        HintText = _localizer["PdfTextHint"];
        CopyText = _localizer["PdfTextCopy"];
        HelpText = _localizer["SectionHelp"];
    }
}
