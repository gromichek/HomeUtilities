using System.Diagnostics;
using System.Globalization;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Lightweight timing of page loads (T-028b). Anything slower than <see cref="Threshold"/> is written to the error log as
/// <c>Performance: &lt;page/VM&gt; &lt;ms&gt; ms (&lt;items&gt; items)</c>, so the owner can copy the lines from Settings → Error log.
/// Cheap enough to stay on in Release.
/// </summary>
public static class PerformanceLog
{
    /// <summary>Loads at or above this duration are logged.</summary>
    public static readonly TimeSpan Threshold = TimeSpan.FromMilliseconds(300);

    /// <summary>Where slow loads go; set once at app startup. When null (unit tests) nothing is logged.</summary>
    public static IErrorLog? Sink { get; set; }

    /// <summary>The line written for a slow load, e.g. <c>Performance: BillsViewModel 412 ms (100 items)</c>.</summary>
    public static string Format(string name, TimeSpan elapsed, int items) =>
        string.Create(CultureInfo.InvariantCulture, $"Performance: {name} {(long)elapsed.TotalMilliseconds} ms ({items} items)");

    /// <summary>Logs the load when it was slow; returns whether it was logged.</summary>
    public static bool Report(string name, TimeSpan elapsed, int items)
    {
        if (elapsed < Threshold || Sink is not { } sink)
        {
            return false;
        }

        sink.Note("Performance", Format(name, elapsed, items));
        return true;
    }

    /// <summary>Debug builds only: a section that skipped its reload because nothing changed (T-028i).</summary>
    [Conditional("DEBUG")]
    public static void ReportSkipped(string name) => Sink?.Note("Performance", $"Performance: {name} skipped (no change)");

    /// <summary>When the process started (set by the app at startup); the cold-start line is measured from it (see <see cref="StartupOrigin"/>).</summary>
    public static DateTimeOffset? AppStarted { get; set; }

    /// <summary>When the first window was created (set by the app in <c>CreateWindow</c>); see <see cref="StartupOrigin"/>.</summary>
    public static DateTimeOffset? FirstWindowCreated { get; set; }

    /// <summary>
    /// A window created later than this after the process start means the process was started without a window (Android starts it
    /// for a reminder alarm); the cold-start time is then measured from the window, not from the process.
    /// </summary>
    public static readonly TimeSpan MaxProcessToWindow = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The moment the cold start is measured from: the process start when the first window followed it shortly, otherwise the
    /// first window. Without a window time the process start is used.
    /// </summary>
    public static DateTimeOffset StartupOrigin(DateTimeOffset processStart, DateTimeOffset? firstWindow) =>
        firstWindow is { } window && window - processStart > MaxProcessToWindow ? window : processStart;

    private static int _startupReported;

    /// <summary>
    /// Logs, once per launch, how long it took from the start of the process until the Overview showed its data
    /// ("Performance: Startup to Overview 1234 ms (3 items)"). Always written (it is a measurement, not a warning).
    /// </summary>
    public static void ReportStartupOnce(int items)
    {
        if (AppStarted is not { } started || Sink is not { } sink || Interlocked.Exchange(ref _startupReported, 1) == 1)
        {
            return;
        }

        sink.Note("Performance", Format("Startup to Overview", DateTimeOffset.UtcNow - StartupOrigin(started, FirstWindowCreated), items));
    }

    /// <summary>Starts a stopwatch for <see cref="Report"/>.</summary>
    public static Stopwatch Start() => Stopwatch.StartNew();
}
