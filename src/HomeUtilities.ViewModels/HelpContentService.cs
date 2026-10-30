using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace HomeUtilities.ViewModels;

/// <summary>Reads the guide through <see cref="IAppAssetReader"/> and renders it with Markdig.</summary>
public sealed partial class HelpContentService : IHelpContentService
{
    public const string RussianLanguage = "ru";

    /// <summary>CSS class on links that leave the app (opened in the system browser).</summary>
    public const string ExternalLinkClass = "external";

    private const string GuideFolder = "guide";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .UseAutoIdentifiers(Markdig.Extensions.AutoIdentifiers.AutoIdentifierOptions.GitHub)
        .UseAutoLinks()
        .UseGenericAttributes()
        .Build();

    private static readonly System.Text.Json.JsonSerializerOptions AnchorJson =
        new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly IAppAssetReader _assets;
    private readonly IErrorLog _errorLog;
    private readonly ILocalizer _localizer;
    private readonly IThemeService _theme;

    public HelpContentService(IAppAssetReader assets, IErrorLog errorLog, ILocalizer localizer, IThemeService theme)
    {
        _assets = assets;
        _errorLog = errorLog;
        _localizer = localizer;
        _theme = theme;
    }

    /// <summary>Asset name of the guide file for a language.</summary>
    public static string FileFor(string language) =>
        $"{GuideFolder}/{(language == RussianLanguage ? RussianLanguage : "en")}.md";

    public async Task<HelpDocument> LoadAsync(string language, string? anchor = null)
    {
        var file = FileFor(language);
        try
        {
            string markdown;
            await using (var stream = await _assets.OpenReadAsync(file))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                markdown = await reader.ReadToEndAsync();
            }

            var document = Markdown.Parse(HtmlComment().Replace(markdown, string.Empty), Pipeline);
            await PrepareLinksAsync(document);
            var body = document.ToHtml(Pipeline);
            return new HelpDocument(WrapPage(body, anchor, _theme.Current), false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _errorLog.Append($"Help guide '{file}'", ex);
            return new HelpDocument(WrapPage($"<p class=\"error\">{WebUtility.HtmlEncode(_localizer["HelpUnavailable"])}</p>", null, _theme.Current), true);
        }
    }

    // Marks external links for the browser and inlines images, so the page needs neither network nor file access.
    private async Task PrepareLinksAsync(MarkdownDocument document)
    {
        foreach (var link in document.Descendants<LinkInline>().ToList())
        {
            var url = link.Url;
            if (string.IsNullOrEmpty(url))
            {
                continue;
            }

            if (link.IsImage)
            {
                if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && !IsExternal(url))
                {
                    link.Url = await InlineImageAsync(url) ?? url;
                }
            }
            else if (IsExternal(url))
            {
                link.GetAttributes().AddClass(ExternalLinkClass);
            }
        }

        foreach (var auto in document.Descendants<AutolinkInline>().Where(a => a.IsEmail == false))
        {
            auto.GetAttributes().AddClass(ExternalLinkClass);
        }
    }

    private async Task<string?> InlineImageAsync(string url)
    {
        try
        {
            var path = $"{GuideFolder}/{url.Replace('\\', '/').TrimStart('/')}";
            await using var stream = await _assets.OpenReadAsync(path);
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return $"data:{MimeTypeOf(url)};base64,{Convert.ToBase64String(buffer.ToArray())}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A missing picture must not hide the text around it.
            _errorLog.Append($"Help image '{url}'", ex);
            return null;
        }
    }

    private static string MimeTypeOf(string url) => Path.GetExtension(url).ToLowerInvariant() switch
    {
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        _ => "application/octet-stream",
    };

    private static bool IsExternal(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    private static string WrapPage(string body, string? anchor, AppThemeChoice theme)
    {
        // In-page "#section" links scroll in the page itself: the WebView then never reports a navigation (its URL differs per platform).
        var onClick = "document.addEventListener('click', function (ev) { var a = ev.target.closest ? ev.target.closest('a[href^=\"#\"]') : null;"
            + " if (a) { ev.preventDefault(); var e = document.getElementById(decodeURIComponent(a.getAttribute('href').substring(1))); if (e) { e.scrollIntoView(); } } });";
        var onLoad = string.IsNullOrEmpty(anchor)
            ? string.Empty
            : $" window.addEventListener('load', function () {{ var e = document.getElementById({System.Text.Json.JsonSerializer.Serialize(anchor, AnchorJson)}); if (e) {{ e.scrollIntoView(); }} }});";
        var script = $"<script>{onClick}{onLoad}</script>";
        var themeAttribute = theme == AppThemeChoice.System ? string.Empty : $" data-theme=\"{theme.ToString().ToLowerInvariant()}\"";
        return $"""
            <!DOCTYPE html>
            <html{themeAttribute}>
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="color-scheme" content="light dark">
            <style>{Css}</style>
            </head>
            <body>
            {body}
            {script}
            </body>
            </html>
            """;
    }

    private const string Css = """
        :root { color-scheme: light dark; --fg: #1c1b1f; --bg: #ffffff; --muted: #605d66; --line: #cac4d0; --head: #f3eff7; --accent: #512bd4; --code: #efeaf6; }
        @media (prefers-color-scheme: dark) { :root:not([data-theme=light]) { --fg: #e6e1e9; --bg: #1c1b1f; --muted: #aaa5b0; --line: #49454f; --head: #2b2930; --accent: #cfbcff; --code: #2b2930; } }
        :root[data-theme=dark] { color-scheme: dark; --fg: #e6e1e9; --bg: #1c1b1f; --muted: #aaa5b0; --line: #49454f; --head: #2b2930; --accent: #cfbcff; --code: #2b2930; }
        :root[data-theme=light] { color-scheme: light; }
        html { -webkit-text-size-adjust: 100%; }
        body { margin: 0; padding: 12px 16px 48px; max-width: 860px; font-family: 'Segoe UI', Roboto, sans-serif; font-size: 16px; line-height: 1.5; color: var(--fg); background: var(--bg); overflow-wrap: anywhere; }
        h1 { font-size: 1.6em; } h2 { font-size: 1.3em; margin-top: 1.6em; border-bottom: 1px solid var(--line); padding-bottom: 4px; } h3 { font-size: 1.1em; }
        a { color: var(--accent); }
        code { background: var(--code); padding: 1px 4px; border-radius: 4px; }
        img { max-width: 100%; height: auto; background: #ffffff; border-radius: 6px; }
        table { border-collapse: collapse; display: block; overflow-x: auto; max-width: 100%; margin: 12px 0; }
        th, td { border: 1px solid var(--line); padding: 6px 10px; text-align: left; vertical-align: top; }
        th { background: var(--head); }
        .error { color: var(--muted); margin-top: 40px; text-align: center; }
        """;

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();
}
