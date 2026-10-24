namespace HomeUtilities.ViewModels;

/// <summary>Controls the running app; implemented by the platform layer.</summary>
public interface IAppLifecycle
{
    /// <summary>Closes the app.</summary>
    void Quit();
}
