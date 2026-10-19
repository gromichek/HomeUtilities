namespace HomeUtilities.Core.Tariffs;

/// <summary>One step of a stepped price. Owned by a <see cref="TariffRate"/>, not an entity of its own.</summary>
/// <param name="UpToQuantity">Upper bound of the step (cumulative per tier period); null means unlimited.</param>
/// <param name="Price">Unit price inside the step.</param>
public sealed record TariffTier(decimal? UpToQuantity, decimal Price);
