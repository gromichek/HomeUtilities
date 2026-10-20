namespace HomeUtilities.ViewModels;

/// <summary>Content of sections that are not implemented yet.</summary>
public sealed class PlaceholderViewModel : ViewModelBase
{
    private readonly ILocalizer _localizer;

    public PlaceholderViewModel(ILocalizer localizer)
    {
        _localizer = localizer;
        RefreshTexts();
    }

    public string ComingSoonText { get; private set; } = string.Empty;

    protected override void RefreshTexts()
    {
        ComingSoonText = _localizer["ComingSoon"];
        OnPropertyChanged(nameof(ComingSoonText));
    }
}
