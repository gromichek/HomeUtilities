namespace HomeUtilities.Core.Abstractions;

/// <summary>Creates or migrates the database and seeds the reference data.</summary>
public interface IDatabaseInitializer
{
    /// <summary>Runs the migrations and the seeding; safe to call on every start.</summary>
    Task InitializeAsync(CancellationToken ct = default);
}
