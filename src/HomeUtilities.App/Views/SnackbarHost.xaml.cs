using System.ComponentModel;
using HomeUtilities.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Accessibility;

namespace HomeUtilities.App.Views;

/// <summary>
/// The in-app snackbar shown at the bottom of every page (see <see cref="PageBase"/>). It binds to the singleton
/// <see cref="SnackbarService"/>; no system notification is used (they crash the unpackaged Windows app).
/// </summary>
public partial class SnackbarHost : ContentView
{
    private const double MaxWidth = 560;
    private const double Gutter = 32;

    private SnackbarService? _service;

    public SnackbarHost()
    {
        InitializeComponent();
    }

    /// <summary>Full width on phones, at most <see cref="MaxWidth"/> and centred on wide windows.</summary>
    public void FitTo(double containerWidth)
    {
        if (containerWidth > 0)
        {
            WidthRequest = Math.Max(0, Math.Min(MaxWidth, containerWidth - Gutter));
        }
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (_service is not null)
        {
            _service.PropertyChanged -= OnServicePropertyChanged;
            _service = null;
            IsVisible = false;
        }

        if (Handler?.MauiContext?.Services.GetService<SnackbarService>() is { } service)
        {
            _service = service;
            BindingContext = service;
            // The bar is hidden in XAML and then follows the service (set here, not bound: a binding resolved against the
            // page's view model before this point would show an empty bar).
            IsVisible = service.IsVisible;
            service.PropertyChanged += OnServicePropertyChanged;
        }
    }

    // Shows and hides the bar; screen readers do not notice a view that appears on its own, so the message is announced.
    private void OnServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SnackbarService.IsVisible) && _service is { } shown)
        {
            // Some callers (the reminder summary) show a message from a worker thread; views change on the UI thread only.
            var visible = shown.IsVisible;
            if (Dispatcher.IsDispatchRequired)
            {
                Dispatcher.Dispatch(() => IsVisible = visible);
            }
            else
            {
                IsVisible = visible;
            }
        }

        if (e.PropertyName == nameof(SnackbarService.Message) && _service is { IsVisible: true, Message.Length: > 0 } service)
        {
            try
            {
                SemanticScreenReader.Announce(service.Message);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Announcing the snackbar failed: {ex}");
            }
        }
    }
}
