namespace HomeUtilities.ViewModels;

/// <summary>Puts text on the system clipboard.</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
}
