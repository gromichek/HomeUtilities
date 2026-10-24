namespace HomeUtilities.ViewModels;

/// <summary>The rendered user guide. <paramref name="IsError"/> is true when the guide could not be loaded.</summary>
public sealed record HelpDocument(string Html, bool IsError);

/// <summary>Turns the bundled user guide (Markdown) into a self-contained HTML page for the help WebView.</summary>
public interface IHelpContentService
{
    /// <summary>
    /// Loads the guide for <paramref name="language"/> (<c>ru</c> → <c>ru.md</c>, anything else → <c>en.md</c>) and
    /// scrolls to <paramref name="anchor"/> when given. Never throws: a missing or broken file gives an error page.
    /// </summary>
    Task<HelpDocument> LoadAsync(string language, string? anchor = null);
}
