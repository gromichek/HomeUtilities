using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>
/// The snackbar: holds the message that is shown and the timing. The window's snackbar host (App project) binds to
/// this object; no system notification is involved (they crash the unpackaged Windows app).
/// </summary>
public sealed partial class SnackbarService : ObservableObject, ISnackbarService
{
    /// <summary>How long a plain message stays.</summary>
    public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(3);

    /// <summary>How long a message with an action stays.</summary>
    public static readonly TimeSpan ActionDuration = TimeSpan.FromSeconds(6);

    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private Request? _current;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _actionText = string.Empty;

    [ObservableProperty]
    private bool _hasAction;

    public SnackbarService()
        : this(Task.Delay)
    {
    }

    /// <param name="delay">Waits for the given time; tests pass a controllable one.</param>
    public SnackbarService(Func<TimeSpan, CancellationToken, Task> delay)
    {
        _delay = delay;
    }

    /// <inheritdoc />
    public Task<bool> ShowAsync(string message, string? actionText = null, TimeSpan? duration = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Task.FromResult(false);
        }

        var action = string.IsNullOrWhiteSpace(actionText) ? null : actionText;
        var request = new Request(action is null ? duration ?? DefaultDuration : duration ?? ActionDuration);
        var previous = _current;
        _current = request;
        previous?.Finish(false);

        Message = message;
        ActionText = action ?? string.Empty;
        HasAction = action is not null;
        IsVisible = true;
        _ = RunTimerAsync(request);
        return request.Result.Task;
    }

    /// <summary>The action button was tapped: the pending call completes with true and the bar disappears.</summary>
    [RelayCommand]
    private void InvokeAction()
    {
        if (HasAction && _current is { } request && request.Finish(true))
        {
            Hide(request);
        }
    }

    /// <summary>Closes the message early (the pending call completes with false).</summary>
    [RelayCommand]
    private void Dismiss()
    {
        if (_current is { } request && request.Finish(false))
        {
            Hide(request);
        }
    }

    private async Task RunTimerAsync(Request request)
    {
        try
        {
            await _delay(request.Duration, request.Timer.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (request.Finish(false))
        {
            Hide(request);
        }
    }

    private void Hide(Request request)
    {
        if (ReferenceEquals(_current, request))
        {
            _current = null;
            IsVisible = false;
        }
    }

    private sealed class Request(TimeSpan duration)
    {
        public TimeSpan Duration { get; } = duration;

        public CancellationTokenSource Timer { get; } = new();

        public TaskCompletionSource<bool> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Completes the call once; false when it was already completed.</summary>
        public bool Finish(bool actionTapped)
        {
            if (!Result.TrySetResult(actionTapped))
            {
                return false;
            }

            Timer.Cancel();
            return true;
        }
    }
}
