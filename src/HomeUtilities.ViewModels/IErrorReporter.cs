namespace HomeUtilities.ViewModels;

/// <summary>Handles an unexpected exception caught at a command boundary: log it and tell the user.</summary>
public interface IErrorReporter
{
    Task ReportAsync(string source, Exception exception);
}
