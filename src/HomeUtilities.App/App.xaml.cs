using Microsoft.Extensions.DependencyInjection;
using HomeUtilities.App.Services;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly LayoutModeService _layoutModeService;

    // AppShell is resolved lazily in CreateWindow: its XAML uses StaticResource colours from App.xaml,
    // which exist only after InitializeComponent() has run. Injecting AppShell into this constructor
    // would build it too early and fail with "StaticResource not found".
    // ILanguageService is requested here only so that it is created (and applies the saved language to
    // CurrentCulture/CurrentUICulture) before any page or the Shell reads localized texts.
    public App(
        IServiceProvider services,
        ILanguageService languageService,
        LayoutModeService layoutModeService,
        IThemeService themeService,
        IErrorReporter errorReporter)
    {
        _ = languageService.CurrentLanguage;
        ViewModelBase.ErrorReporter = errorReporter;
        InitializeComponent();
        _services = services;
        _layoutModeService = layoutModeService;
        themeService.Set(themeService.Current);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<AppShell>());
        _layoutModeService.Attach(window);
        ShowRestoreFailureOnce(window);
        StartReminders(window);
        return window;
    }

    // Resolved here (not in the constructor): the scheduler reads localized texts and listens to data changes from now on.
    // On start and on resume the notifications are rescheduled; when the app is left, a change that still waits for the
    // debounce is handed to the system at once; a tap that started the app is opened once the window exists.
    private void StartReminders(Window window)
    {
        var scheduler = _services.GetRequiredService<ReminderScheduler>();
        var router = _services.GetRequiredService<NotificationTapRouter>();
        var tapHandler = _services.GetRequiredService<NotificationTapHandler>();
        var errorLog = _services.GetRequiredService<IErrorLog>();
        var justCreated = false;

        void Refresh(ReminderRescheduleReason reason) => _ = Task.Run(async () =>
        {
            try
            {
                scheduler.RequestReschedule(reason);
                await scheduler.ShowStartSummaryAsync();
            }
            catch (Exception ex)
            {
                errorLog.Append("Reminders on start", ex);
            }
        });

        // Android stops the activity (and may end the process) soon after the app is left: no waiting for the debounce.
        void Flush() => _ = Task.Run(scheduler.FlushAsync);

        window.Created += (_, _) =>
        {
            justCreated = true;
            Dispatcher.Dispatch(() => router.Attach(tapHandler.OpenAsync));
            Refresh(ReminderRescheduleReason.Start);
        };
        window.Resumed += (_, _) =>
        {
            // The first Resumed follows Created: the start run is already requested.
            if (justCreated)
            {
                justCreated = false;
                return;
            }

            Refresh(ReminderRescheduleReason.Resume);
        };
        window.Deactivated += (_, _) => Flush();
        window.Stopped += (_, _) => Flush();
    }

    // A failed startup restore is reported once, when the first page is on screen.
    private void ShowRestoreFailureOnce(Window window)
    {
        if (_services.GetRequiredService<StartupNotice>().RestoreFailure is not { } reason)
        {
            return;
        }

        var localizer = _services.GetRequiredService<ILocalizer>();
        var dialogs = _services.GetRequiredService<IDialogService>();
        window.Created += (_, _) => Dispatcher.Dispatch(async () =>
        {
            try
            {
                await dialogs.AlertAsync(localizer["BackupRestoreTitle"], localizer.Get("BackupRestoreFailed", reason));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Showing the restore failure failed: {ex}");
            }
        });
    }
}
