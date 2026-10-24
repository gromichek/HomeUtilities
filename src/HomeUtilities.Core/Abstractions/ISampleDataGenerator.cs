namespace HomeUtilities.Core.Abstractions;

/// <summary>Fills an empty database with invented data for testing the app (debug builds only).</summary>
public interface ISampleDataGenerator
{
    /// <summary>Creates the sample data; returns false (and changes nothing) if properties already exist.</summary>
    Task<bool> FillAsync(CancellationToken ct = default);
}
