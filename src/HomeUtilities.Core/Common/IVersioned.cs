namespace HomeUtilities.Core.Common;

/// <summary>An item whose value applies from a given date onwards.</summary>
public interface IVersioned
{
    /// <summary>First date on which this item applies.</summary>
    DateOnly ValidFrom { get; }
}
