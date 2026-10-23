namespace HomeUtilities.ViewModels;

/// <summary>Shows a short, non-blocking message.</summary>
public interface IToastService
{
    Task ShowAsync(string message);
}
