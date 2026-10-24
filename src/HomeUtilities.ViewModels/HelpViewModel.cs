using CommunityToolkit.Mvvm.ComponentModel;

namespace HomeUtilities.ViewModels;

/// <summary>Shows the user guide (whole, or scrolled to the section of <see cref="Topic"/>) in the page's WebView.</summary>
public sealed partial class HelpViewModel : ViewModelBase
{
    private readonly IHelpContentService _content;
    private readonly ILanguageService _language;
    private readonly ILauncherService _launcher;
    private readonly ILocalizer _localizer;

    private string? _languageOverride;

    [ObservableProperty]
    private string _html = string.Empty;

    [ObservableProperty]
    private HelpTopic? _topic;

    public HelpViewModel(
        IHelpContentService content,
        ILanguageService language,
        ILauncherService launcher,
        ILocalizer localizer)
    {
        _content = content;
        _language = language;
        _launcher = launcher;
        _localizer = localizer;
        RefreshTexts();
    }

    public override Task OnAppearingAsync() => ReloadAsync();

    /// <summary>
    /// True when a WebView navigation to <paramref name="url"/> must be cancelled and handed to
    /// <see cref="InterceptAsync"/>: external links (system browser) and links to the other language's guide file.
    /// "#anchor" links and the page itself stay in the WebView.
    /// </summary>
    public bool ShouldIntercept(string? url) => IsExternal(url) || GuideLanguageOf(url) is not null;

    /// <summary>Opens an external link in the system browser, or reloads the page in the linked guide's language.</summary>
    public Task InterceptAsync(string url) => RunSafeAsync(async () =>
    {
        if (IsExternal(url))
        {
            await _launcher.OpenAsync(new Uri(url));
        }
        else if (GuideLanguageOf(url) is { } language)
        {
            _languageOverride = language;
            Topic = null;
            await ReloadAsync();
        }
    });

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionHelp"];

        // The language changed while the page is open: show the guide in the new language.
        _languageOverride = null;
        if (Html.Length > 0)
        {
            _ = RunSafeAsync(ReloadAsync);
        }
    }

    private async Task ReloadAsync()
    {
        var language = _languageOverride ?? _language.CurrentLanguage;
        var anchor = Topic is { } topic ? HelpTopics.GetAnchor(topic, language) : null;
        Html = (await _content.LoadAsync(language, anchor)).Html;
    }

    private static bool IsExternal(string? url) =>
        url is not null
        && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

    private static string? GuideLanguageOf(string? url)
    {
        var path = url?.Split('#')[0] ?? string.Empty;
        foreach (var language in new[] { "en", HelpContentService.RussianLanguage })
        {
            if (path.EndsWith($"{language}.md", StringComparison.OrdinalIgnoreCase))
            {
                return language;
            }
        }

        return null;
    }
}
