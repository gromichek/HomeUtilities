using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Services;

public sealed class ToastService : IToastService
{
    public Task ShowAsync(string message) => Toast.Make(message, ToastDuration.Short).Show();
}
