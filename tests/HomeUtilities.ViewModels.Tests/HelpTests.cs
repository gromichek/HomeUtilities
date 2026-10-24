using System.Text;
using System.Text.RegularExpressions;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>Serves the guide from a dictionary; used to test missing files and image inlining.</summary>
internal sealed class FakeAssetReader : IAppAssetReader
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public Task<Stream> OpenReadAsync(string path) =>
        Files.TryGetValue(path, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new FileNotFoundException(path);
}

/// <summary>Serves the real <c>docs/user-guide</c> files the way the app package does (<c>guide/…</c>).</summary>
internal sealed class RepositoryGuideReader : IAppAssetReader
{
    public static readonly string GuideFolder = FindGuideFolder();

    public Task<Stream> OpenReadAsync(string path)
    {
        var relative = path["guide/".Length..];
        return Task.FromResult<Stream>(File.OpenRead(Path.Combine(GuideFolder, relative)));
    }

    private static string FindGuideFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "user-guide");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("docs/user-guide was not found above the test output folder.");
    }
}

public class HelpContentServiceTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeErrorLog _log = new();
    private readonly FakeThemeService _theme = new();

    private HelpContentService Create(IAppAssetReader reader) => new(reader, _log, new FakeLocalizer(_language), _theme);

    private static FakeAssetReader Reader(string en, string ru = "# ru") => new()
    {
        Files =
        {
            ["guide/en.md"] = Encoding.UTF8.GetBytes(en),
            ["guide/ru.md"] = Encoding.UTF8.GetBytes(ru),
        },
    };

    [Theory]
    [InlineData("ru", "guide/ru.md")]
    [InlineData("en", "guide/en.md")]
    [InlineData("lv", "guide/en.md")]
    public void FileFor_RussianUsesRuOtherwiseEn(string language, string expected) =>
        Assert.Equal(expected, HelpContentService.FileFor(language));

    [Fact]
    public async Task LoadAsync_ReadsTheFileOfTheLanguage()
    {
        var service = Create(Reader("# English guide", "# Руководство"));

        Assert.Contains("English guide", (await service.LoadAsync("en")).Html);
        Assert.Contains("Руководство", (await service.LoadAsync("ru")).Html);
    }

    [Fact]
    public async Task LoadAsync_RendersHeadingsWithIdsAndTables()
    {
        var service = Create(Reader("## 3. Properties\n\n| A | B |\n|---|---|\n| 1 | 2 |\n"));

        var html = (await service.LoadAsync("en")).Html;

        Assert.Contains("<h2 id=\"3-properties\">3. Properties</h2>", html);
        Assert.Contains("<table>", html);
        Assert.Contains("<td>2</td>", html);
    }

    [Fact]
    public async Task LoadAsync_StripsHtmlComments()
    {
        var service = Create(Reader("# Title\n\n<!-- maintained by sessions,\n     two lines -->\n\nText <!-- inline --> end\n"));

        var html = (await service.LoadAsync("en")).Html;

        Assert.DoesNotContain("<!--", html);
        Assert.DoesNotContain("maintained by sessions", html);
        Assert.Contains("Text", html);
    }

    [Fact]
    public async Task LoadAsync_MarksExternalLinksForTheBrowser()
    {
        var service = Create(Reader("[site](https://example.com/a) and [other](#3-properties) and [guide](ru.md)"));

        var html = (await service.LoadAsync("en")).Html;

        Assert.Contains("<a href=\"https://example.com/a\" class=\"external\">site</a>", html);
        Assert.Contains("<a href=\"#3-properties\">other</a>", html);
        Assert.DoesNotContain("href=\"ru.md\" class=\"external\"", html);
    }

    [Fact]
    public async Task LoadAsync_InlinesImagesAsDataUris()
    {
        var reader = Reader("![diagram](img/d.svg)");
        reader.Files["guide/img/d.svg"] = Encoding.UTF8.GetBytes("<svg/>");
        var service = Create(reader);

        var html = (await service.LoadAsync("en")).Html;

        Assert.Contains($"src=\"data:image/svg+xml;base64,{Convert.ToBase64String("<svg/>"u8.ToArray())}\"", html);
        Assert.DoesNotContain("img/d.svg", html);
    }

    [Fact]
    public async Task LoadAsync_MissingImage_KeepsTheTextAndLogs()
    {
        var service = Create(Reader("Before ![diagram](img/missing.svg) after"));

        var document = await service.LoadAsync("en");

        Assert.False(document.IsError);
        Assert.Contains("Before", document.Html);
        Assert.Single(_log.Entries);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_GivesLocalizedMessageAndLogs()
    {
        var service = Create(new FakeAssetReader());

        var document = await service.LoadAsync("ru");

        Assert.True(document.IsError);
        Assert.Contains("HelpUnavailable [en]", document.Html);
        Assert.Single(_log.Entries);
        Assert.IsType<FileNotFoundException>(_log.Entries[0].Exception);
    }

    [Fact]
    public async Task LoadAsync_WithAnchor_ScrollsToIt()
    {
        var service = Create(Reader("## 3. Properties"));

        var html = (await service.LoadAsync("en", "3-properties")).Html;

        Assert.Contains("getElementById(\"3-properties\")", html);
        Assert.DoesNotContain("getElementById", (await service.LoadAsync("en")).Html);
    }

    [Theory]
    [InlineData(AppThemeChoice.Dark, "<html data-theme=\"dark\">")]
    [InlineData(AppThemeChoice.Light, "<html data-theme=\"light\">")]
    [InlineData(AppThemeChoice.System, "<html>")]
    public async Task LoadAsync_AppliesTheTheme(AppThemeChoice theme, string expected)
    {
        _theme.Set(theme);
        var service = Create(Reader("# T"));

        Assert.Contains(expected, (await service.LoadAsync("en")).Html);
    }

    // The real files: every topic of the "?" buttons must point at a heading that exists, in both languages.
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task RealGuide_HasEveryHelpTopicAnchor(string language)
    {
        var service = Create(new RepositoryGuideReader());

        var document = await service.LoadAsync(language);

        Assert.False(document.IsError);
        foreach (var topic in Enum.GetValues<HelpTopic>())
        {
            var anchor = HelpTopics.GetAnchor(topic, language);
            Assert.True(Regex.IsMatch(document.Html, $"<h[1-6] id=\"{Regex.Escape(anchor)}\""), $"{topic}: anchor '{anchor}' not found in {language}.md");
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public async Task RealGuide_InlinesTheDiagramAndHasNoComments(string language)
    {
        var service = Create(new RepositoryGuideReader());

        var html = (await service.LoadAsync(language)).Html;

        Assert.Contains("src=\"data:image/svg+xml;base64,", html);
        Assert.DoesNotContain("img/relations", html);
        Assert.DoesNotContain("<!--", html);
        Assert.DoesNotContain("mermaid", html);
        Assert.Contains("<table>", html);
    }

    [Fact]
    public void RealGuide_DiagramSourcesHaveRenderedSvgs()
    {
        var sources = Directory.GetFiles(Path.Combine(RepositoryGuideReader.GuideFolder, "img"), "*.mmd");

        Assert.NotEmpty(sources);
        foreach (var source in sources)
        {
            Assert.True(File.Exists(Path.ChangeExtension(source, ".svg")), $"{Path.GetFileName(source)} was not rendered; run scripts/render-guide-diagrams.sh");
        }
    }
}

public class HelpViewModelTests
{
    private readonly FakeLanguageService _language = new();
    private readonly FakeLauncherService _launcher = new();
    private readonly FakeAssetReader _assets = new();
    private readonly FakeErrorLog _log = new();

    private HelpViewModel Create()
    {
        _assets.Files["guide/en.md"] = Encoding.UTF8.GetBytes("## 3. Properties\n\nEnglish");
        _assets.Files["guide/ru.md"] = Encoding.UTF8.GetBytes("## 3. Объекты\n\nРусский");
        var localizer = new FakeLocalizer(_language);
        var content = new HelpContentService(_assets, _log, localizer, new FakeThemeService());
        return new HelpViewModel(content, _language, _launcher, localizer);
    }

    [Fact]
    public async Task OnAppearing_ShowsTheGuideInTheCurrentLanguage()
    {
        var vm = Create();

        await vm.OnAppearingAsync();
        Assert.Contains("English", vm.Html);

        _language.SetLanguage("ru");
        await Task.Yield();
        Assert.Contains("Русский", vm.Html);
        Assert.Equal("SectionHelp [ru]", vm.Title);
    }

    [Fact]
    public async Task LanguageChange_ReloadsAnOpenPage()
    {
        var vm = Create();
        await vm.OnAppearingAsync();

        _language.SetLanguage("ru");
        await Task.Delay(50);

        Assert.Contains("Русский", vm.Html);
    }

    [Fact]
    public async Task Topic_ScrollsToTheAnchorOfTheLanguage()
    {
        var vm = Create();
        vm.Topic = HelpTopic.Properties;

        await vm.OnAppearingAsync();
        Assert.Contains("getElementById(\"3-properties\")", vm.Html);

        _language.SetLanguage("ru");
        await Task.Delay(50);
        Assert.Contains("getElementById(\"3-объекты\")", vm.Html);
    }

    [Fact]
    public async Task ExternalLink_OpensTheBrowserAndCancelsNavigation()
    {
        var vm = Create();

        Assert.True(vm.ShouldIntercept("https://example.com/portal"));
        await vm.InterceptAsync("https://example.com/portal");

        Assert.Equal([new Uri("https://example.com/portal")], _launcher.Opened);
    }

    [Theory]
    [InlineData("#3-properties")]
    [InlineData("about:blank#3-properties")]
    [InlineData("about:blank")]
    [InlineData("data:text/html;base64,AAAA")]
    [InlineData(null)]
    public void InternalNavigation_StaysInTheWebView(string? url) => Assert.False(Create().ShouldIntercept(url));

    [Fact]
    public async Task LinkToTheOtherGuideFile_ReloadsInThatLanguage()
    {
        var vm = Create();
        await vm.OnAppearingAsync();

        Assert.True(vm.ShouldIntercept("file:///android_asset/guide/ru.md#3-объекты"));
        await vm.InterceptAsync("file:///android_asset/guide/ru.md#3-объекты");

        Assert.Contains("Русский", vm.Html);
        Assert.Empty(_launcher.Opened);
    }

    [Fact]
    public async Task MissingGuide_ShowsTheMessageInsteadOfThrowing()
    {
        var vm = Create();
        _assets.Files.Clear();

        await vm.OnAppearingAsync();

        Assert.Contains("HelpUnavailable", vm.Html);
        Assert.NotEmpty(_log.Entries);
    }
}

public class HelpButtonTests
{
    [Fact]
    public async Task PropertiesHelpCommand_OpensTheGuideAtTheScreensTopic()
    {
        var language = new FakeLanguageService();
        var store = new FakePropertyStore();
        var navigation = new FakeNavigationService();
        var clock = new TestClock(new DateOnly(2026, 10, 24));
        var vm = new PropertiesViewModel(
            store, clock, new FakeLocalizer(language), navigation, new FakeLayoutModeService(),
            () => throw new InvalidOperationException("no editor expected"));

        await vm.OpenHelpCommand.ExecuteAsync(null);

        var visit = Assert.Single(navigation.Visited);
        Assert.Equal(Routes.HelpSection, visit.Route);
        Assert.Equal(HelpTopic.Properties, visit.Parameters![HelpNavigation.TopicParameter]);
        Assert.Equal("SectionHelp [en]", vm.HelpText);
    }

    [Fact]
    public void ShellMenu_HasATitleForHelp()
    {
        var vm = new ShellViewModel(new FakeLocalizer(new FakeLanguageService()), new FakeLayoutModeService());

        Assert.Equal("SectionHelp [en]", vm.HelpTitle);
    }
}
