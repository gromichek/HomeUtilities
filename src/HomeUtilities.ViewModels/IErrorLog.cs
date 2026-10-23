namespace HomeUtilities.ViewModels;

/// <summary>Persistent log of unexpected errors, so the owner can send it for diagnosis. Never throws.</summary>
public interface IErrorLog
{
    /// <summary>Appends the exception with a timestamp and the place where it was caught.</summary>
    void Append(string source, Exception exception);

    /// <summary>The whole log; empty when nothing was logged.</summary>
    string ReadAll();

    void Clear();
}
