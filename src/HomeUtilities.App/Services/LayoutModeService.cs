using CommunityToolkit.Mvvm.Messaging;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

/// <summary>Derives the layout mode from the window width (in dp) and publishes a message only when the mode changes.</summary>
public sealed class LayoutModeService : ILayoutModeService
{
    public LayoutMode Current { get; private set; } = LayoutMode.Compact;

    public void Attach(Window window)
    {
        window.SizeChanged += (_, _) => Update(window.Width, window.Height);
        window.Created += (_, _) => Update(window.Width, window.Height);
    }

    private void Update(double widthDp, double heightDp)
    {
        if (widthDp <= 0)
        {
            return;
        }

        var mode = LayoutModes.FromSize(widthDp, heightDp);
        if (mode == Current)
        {
            return;
        }

        Current = mode;
        WeakReferenceMessenger.Default.Send(new LayoutModeChangedMessage(mode));
    }
}
