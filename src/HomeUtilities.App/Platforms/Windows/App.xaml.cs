namespace HomeUtilities.App.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            // Handled = true keeps the app running; the exception goes to the error log.
            e.Handled = true;
            HomeUtilities.App.Services.GlobalErrorHandlers.Log("Microsoft.UI.Xaml.Application.UnhandledException", e.Exception);
        };
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
