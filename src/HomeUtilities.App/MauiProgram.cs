using CommunityToolkit.Maui;
using HomeUtilities.App.Services;
using HomeUtilities.App.Views;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Export;
using HomeUtilities.Data.Backup;
using HomeUtilities.Data;
using HomeUtilities.Core.Reports;
using HomeUtilities.Import;
using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;
using Microsoft.Extensions.Logging;
#if WINDOWS
using Microsoft.Maui.LifecycleEvents;
#endif
#if ANDROID
using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models.AndroidOption;
#endif

namespace HomeUtilities.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts => fonts.AddFont("MaterialIconsOutlined-Regular.otf", "MaterialIcons"));

#if WINDOWS
        builder.ConfigureLifecycleEvents(events => events.AddWindows(windows =>
            windows.OnWindowCreated(window => HomeUtilities.App.WinUI.WindowCloseGuard.Attach(window))));
#endif

        var tapRouter = new NotificationTapRouter();
        builder.Services.AddSingleton(tapRouter);
#if ANDROID
        var channelTexts = new ResxLocalizer();
        builder
            .UseLocalNotification(config => config.AddAndroid(android => android.AddChannel(new AndroidNotificationChannelRequest
            {
                Id = AndroidNotificationService.ChannelId,
                Name = channelTexts["NotificationChannelName"],
                Description = channelTexts["NotificationChannelDescription"],
                // Show the reminder text on the lock screen (owner, 2026-11-01: only the sound came).
                LockScreenVisibility = Plugin.LocalNotification.Core.Models.AndroidOption.AndroidVisibilityType.Public,
            })));
        Services.ReminderAlarms.DeleteOldChannel(global::Android.App.Application.Context);
        // Subscribed this early so that the tap which started the app is not lost; the router keeps it until the window is ready.
        LocalNotificationCenter.Current.NotificationActionTapped += e =>
        {
            if (e.IsTapped)
            {
                tapRouter.Submit(e.Request.ReturningData);
            }
        };
        builder.Services.AddSingleton<HomeUtilities.ViewModels.INotificationService, AndroidNotificationService>();
#else
        builder.Services.AddSingleton<HomeUtilities.ViewModels.INotificationService, UnavailableNotificationService>();
#endif

        var appPaths = new AppPaths();
        builder.Services.AddSingleton<IAppPaths>(appPaths);
        var errorLog = new FileErrorLog(appPaths.ErrorLogPath);
        GlobalErrorHandlers.Install(errorLog);
        builder.Services.AddSingleton<IErrorLog>(errorLog);
        builder.Services.AddSingleton<IErrorReporter, ErrorReporter>();
        var clock = new SystemClock();
        builder.Services.AddSingleton<IClock>(clock);

        // A staged restore (or wipe) replaces the database file, so it runs before anything can open the database.
        var backup = new BackupService(
            new BackupOptions(
                appPaths.DatabasePath,
                appPaths.AttachmentsFolder,
                appPaths.RestoreFolder,
                appPaths.BackupsFolder,
                AppInfo.Current.VersionString,
                DeviceInfo.Platform.ToString()),
            clock);
        // Off the UI thread: the restore blocks on async file work, which would deadlock on the UI thread.
        var restore = Task.Run(backup.ApplyPendingRestore).GetAwaiter().GetResult();
        if (restore is { Succeeded: false })
        {
            errorLog.Append("Startup restore", restore.Exception ?? new InvalidOperationException(restore.Message));
        }

        builder.Services.AddSingleton(new StartupNotice { RestoreFailure = restore is { Succeeded: false } ? restore.Message : null });
        builder.Services.AddSingleton<IBackupService>(backup);
        builder.Services.AddSingleton<IFileSaveService, FileSaveService>();
        builder.Services.AddSingleton<IBackupFileService, BackupFileService>();
        builder.Services.AddSingleton<IAppLifecycle, AppLifecycle>();
        builder.Services.AddSingleton<ILanguageService, LanguageService>();
        builder.Services.AddSingleton<ILocalizer, ResxLocalizer>();
        builder.Services.AddSingleton<IImageProcessor, ImageProcessor>();
        builder.Services.AddHomeUtilitiesData(appPaths.DatabasePath, appPaths.AttachmentsFolder);
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<IThemeService, ThemeService>();
        builder.Services.AddSingleton<LayoutModeService>();
        builder.Services.AddSingleton<ILayoutModeService>(sp => sp.GetRequiredService<LayoutModeService>());
        builder.Services.AddSingleton<HomeUtilities.ViewModels.IAppInfo, AppInfoService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IActiveEditorProvider, ShellActiveEditorProvider>();
        builder.Services.AddSingleton<INavigationGuard, NavigationGuard>();
        builder.Services.AddSingleton<ISectionSelectionMemory, SectionSelectionMemory>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddSingleton<ILauncherService, LauncherService>();
        builder.Services.AddSingleton<IAppAssetReader, AppAssetReader>();
        builder.Services.AddSingleton<IHelpContentService, HelpContentService>();
        builder.Services.AddSingleton<IMediaService, MediaService>();
        builder.Services.AddSingleton<IFileViewer, FileViewer>();
        builder.Services.AddSingleton<IClipboardService, ClipboardService>();
        builder.Services.AddSingleton<SnackbarService>();
        builder.Services.AddSingleton<ISnackbarService>(sp => sp.GetRequiredService<SnackbarService>());
        builder.Services.AddSingleton<ISettingsStore, PreferencesSettingsStore>();
        builder.Services.AddSingleton<ReminderSettingsStore>();
        builder.Services.AddSingleton<ReminderScheduler>();
        builder.Services.AddSingleton<NotificationTapHandler>();
        builder.Services.AddTransient<ReminderSettingsViewModel>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<ReportFilterState>();
        builder.Services.AddSingleton<IReportWorkbookWriter, ClosedXmlReportWriter>();
        builder.Services.AddSingleton<IExportWorkbookWriter, ClosedXmlExportWriter>();
        builder.Services.AddSingleton<IWorkbookReader, ClosedXmlWorkbookReader>();
        builder.Services.AddTransient<WorkbookExportFlow>();
        builder.Services.AddSingleton<IImportFileService, ImportFileService>();
        builder.Services.AddTransient<ImportWizardViewModel>();
        builder.Services.AddTransient<ImportsViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<OverviewViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<PlaceholderViewModel>();
        builder.Services.AddTransient<HelpViewModel>();
        builder.Services.AddTransient<PropertiesViewModel>();
        builder.Services.AddTransient<PropertyEditViewModel>();
        builder.Services.AddTransient<Func<PropertyEditViewModel>>(sp => () => sp.GetRequiredService<PropertyEditViewModel>());
        builder.Services.AddTransient<SuppliersViewModel>();
        builder.Services.AddTransient<SupplierEditViewModel>();
        builder.Services.AddTransient<Func<SupplierEditViewModel>>(sp => () => sp.GetRequiredService<SupplierEditViewModel>());
        builder.Services.AddTransient<ContractEditViewModel>();
        builder.Services.AddTransient<Func<ContractEditViewModel>>(sp => () => sp.GetRequiredService<ContractEditViewModel>());
        builder.Services.AddTransient<MeterEditViewModel>();
        builder.Services.AddTransient<Func<MeterEditViewModel>>(sp => () => sp.GetRequiredService<MeterEditViewModel>());
        builder.Services.AddTransient<ReadingsViewModel>();
        builder.Services.AddTransient<ChargesViewModel>();
        builder.Services.AddTransient<ReadingHistoryViewModel>();
        builder.Services.AddTransient<Func<ReadingHistoryViewModel>>(sp => () => sp.GetRequiredService<ReadingHistoryViewModel>());
        builder.Services.AddSingleton<QuickPayService>();
        builder.Services.AddSingleton<DraftService>();
        builder.Services.AddSingleton<BillTemplateLauncher>();
        builder.Services.AddSingleton<PrepaymentService>();
        builder.Services.AddSingleton<EqualizedSettlementService>();
        builder.Services.AddTransient<EqualizedSettlementViewModel>();
        builder.Services.AddSingleton<PaymentFilterState>();
        builder.Services.AddSingleton<BillFilterState>();
        builder.Services.AddTransient<BillsViewModel>();
        builder.Services.AddTransient<BillEditViewModel>();
        builder.Services.AddTransient<Func<BillEditViewModel>>(sp => () => sp.GetRequiredService<BillEditViewModel>());
        builder.Services.AddTransient<BillTemplatesViewModel>();
        builder.Services.AddTransient<BillTemplateEditViewModel>();
        builder.Services.AddTransient<Func<BillTemplateEditViewModel>>(sp => () => sp.GetRequiredService<BillTemplateEditViewModel>());
        builder.Services.AddTransient<PaymentsViewModel>();
        builder.Services.AddTransient<PaymentEditViewModel>();
        builder.Services.AddTransient<Func<PaymentEditViewModel>>(sp => () => sp.GetRequiredService<PaymentEditViewModel>());
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<CategoryEditViewModel>();
        builder.Services.AddTransient<Func<CategoryEditViewModel>>(sp => () => sp.GetRequiredService<CategoryEditViewModel>());
        builder.Services.AddTransient<TariffComponentEditViewModel>();
        builder.Services.AddTransient<VatRatesViewModel>();
        builder.Services.AddTransient<VatRateEditViewModel>();
        builder.Services.AddTransient<Func<VatRateEditViewModel>>(sp => () => sp.GetRequiredService<VatRateEditViewModel>());
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<OverviewPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<SettingsSectionPage>();
        builder.Services.AddTransient<PlaceholderPage>();
        builder.Services.AddTransient<HelpPage>();
        builder.Services.AddTransient<PropertiesPage>();
        builder.Services.AddTransient<PropertyEditPage>();
        builder.Services.AddTransient<ContractEditPage>();
        builder.Services.AddTransient<MeterEditPage>();
        builder.Services.AddTransient<ReadingsPage>();
        builder.Services.AddTransient<ChargesPage>();
        builder.Services.AddTransient<EqualizedSettlementPage>();
        builder.Services.AddTransient<ReadingHistoryPage>();
        builder.Services.AddTransient<SuppliersPage>();
        builder.Services.AddTransient<SupplierEditPage>();
        builder.Services.AddTransient<BillsPage>();
        builder.Services.AddTransient<BillEditPage>();
        builder.Services.AddTransient<BillTemplatesPage>();
        builder.Services.AddTransient<BillTemplateEditPage>();
        builder.Services.AddTransient<PaymentsPage>();
        builder.Services.AddTransient<PaymentEditPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<CategoryEditPage>();
        builder.Services.AddTransient<TariffComponentEditPage>();
        builder.Services.AddTransient<VatRatesPage>();
        builder.Services.AddTransient<VatRateEditPage>();
        builder.Services.AddTransient<ImportWizardPage>();
        builder.Services.AddTransient<ImportsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
