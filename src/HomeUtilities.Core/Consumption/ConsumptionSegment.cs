namespace HomeUtilities.Core.Consumption;

/// <summary>Consumption of one meter within a calculated interval.</summary>
/// <param name="MeterId">The meter.</param>
/// <param name="StartValue">Dial value at the start of the segment.</param>
/// <param name="EndValue">Dial value at the end of the segment.</param>
/// <param name="Quantity">Consumption of the segment (rounded to 3 decimals).</param>
/// <param name="Rollover">True if a dial rollover was assumed.</param>
public sealed record ConsumptionSegment(Guid MeterId, decimal StartValue, decimal EndValue, decimal Quantity, bool Rollover);
