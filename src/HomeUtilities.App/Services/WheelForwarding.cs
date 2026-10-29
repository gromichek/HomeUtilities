namespace HomeUtilities.App.Services;

/// <summary>
/// Makes the vertical mouse wheel over a horizontally scrolling view scroll the page (the parent scroll view). Windows only:
/// WinUI lets the inner scroll viewer swallow the wheel; touch and the Android scroll chaining already behave.
/// </summary>
public static class WheelForwarding
{
    /// <summary>Starts forwarding vertical wheel input of <paramref name="scroll"/> to the page; call after its handler exists.</summary>
    public static void Attach(ScrollView scroll)
    {
#if WINDOWS
        HomeUtilities.App.WinUI.WheelForwardingWindows.Attach(scroll);
#endif
    }
}
