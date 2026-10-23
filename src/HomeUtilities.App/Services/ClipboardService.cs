using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class ClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Clipboard.Default.SetTextAsync(text);
}
