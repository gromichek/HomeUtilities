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

    /// <summary>When the process started (set by the app at startup); used for the cold-start line.</summary>
    public static DateTimeOffset? AppStarted { get; set; }

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

        sink.Note("Performance", Format("Startup to Overview", DateTimeOffset.UtcNow - started, items));
    }

    /// <summary>Starts a stopwatch for <see cref="Report"/>.</summary>
    public static Stopwatch Start() => Stopwatch.StartNew();
}
