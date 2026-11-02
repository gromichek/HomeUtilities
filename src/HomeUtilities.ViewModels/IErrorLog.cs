namespace HomeUtilities.ViewModels;

/// <summary>Persistent log of unexpected errors, so the owner can send it for diagnosis. Never throws.</summary>
public interface IErrorLog
{
    /// <summary>Appends the exception with a timestamp and the place where it was caught.</summary>
    void Append(string source, Exception exception);

    /// <summary>The whole log; empty when nothing was logged.</summary>
    string ReadAll();

    void Clear();

    /// <summary>Appends a plain message (no exception), e.g. a "Performance: …" line. Default: logged as an exception-less entry.</summary>
    void Note(string source, string message) => Append(source, new NoteException(message));
}

/// <summary>Carries a plain message through <see cref="IErrorLog.Append"/> for logs that only implement the exception overload.</summary>
public sealed class NoteException(string message) : Exception(message)
{
    public override string ToString() => Message;
}
