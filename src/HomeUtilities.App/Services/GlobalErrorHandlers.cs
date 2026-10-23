using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>
/// Last line of defence: exceptions that escape every command handler are written to the error log instead of
/// killing the app where the platform allows it. Installed once, as early as possible.
/// </summary>
public static class GlobalErrorHandlers
{
    private static IErrorLog? _log;
    private static bool _installed;

    /// <summary>Writes an unhandled exception to the error log; never throws.</summary>
    public static void Log(string source, Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            _log ??= new FileErrorLog(new AppPaths().ErrorLogPath);
            _log.Append(source, exception);
        }
        catch (Exception)
        {
            // Nothing sensible left to do.
        }
    }

    public static void Install(IErrorLog log)
    {
        _log = log;
        if (_installed)
        {
            return;
        }

        _installed = true;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }
}
