using CommunityToolkit.Mvvm.Input;

namespace HomeUtilities.ViewModels;

/// <summary>State of a contract's reading submission window relative to today.</summary>
public enum ReadingWindowState
{
    /// <summary>Today is inside the window.</summary>
    Open,

    /// <summary>The window of this month starts later.</summary>
    Upcoming,

    /// <summary>This month's window is over; the next one is in the following month.</summary>
    Closed,
}

/// <summary>The banner of a contract's reading window with the "Submit online" button.</summary>
public sealed class ReadingWindowBannerViewModel
{
    public ReadingWindowBannerViewModel(Guid contractId, ReadingWindowState state, string text, string submitText, IAsyncRelayCommand? submitCommand)
    {
        ContractId = contractId;
        State = state;
        Text = text;
        SubmitText = submitText;
        SubmitCommand = submitCommand;
    }

    public Guid ContractId { get; }

    public ReadingWindowState State { get; }

    /// <summary>"Water utility: submit 25–28 Oct — open now".</summary>
    public string Text { get; }

    public bool IsOpen => State == ReadingWindowState.Open;

    public bool IsUpcoming => State == ReadingWindowState.Upcoming;

    public bool IsClosed => State == ReadingWindowState.Closed;

    /// <summary>"Submit online"; the button exists only when the contract (or its supplier) has a SubmitReadings link.</summary>
    public string SubmitText { get; }

    public IAsyncRelayCommand? SubmitCommand { get; }

    public bool HasSubmitLink => SubmitCommand is not null;
}

/// <summary>"Mark as submitted to {supplier}?" shown after the readings were saved.</summary>
public sealed class SubmitPromptViewModel
{
    public SubmitPromptViewModel(
        Guid contractId,
        IReadOnlySet<Guid> meterIds,
        string text,
        string markText,
        string dismissText,
        IAsyncRelayCommand mark,
        IRelayCommand dismiss)
    {
        ContractId = contractId;
        MeterIds = meterIds;
        Text = text;
        MarkText = markText;
        DismissText = dismissText;
        MarkCommand = mark;
        DismissCommand = dismiss;
    }

    public Guid ContractId { get; }

    /// <summary>Meters of the contract that were saved just now.</summary>
    public IReadOnlySet<Guid> MeterIds { get; }

    public string Text { get; }

    public string MarkText { get; }

    public string DismissText { get; }

    public IAsyncRelayCommand MarkCommand { get; }

    public IRelayCommand DismissCommand { get; }
}
