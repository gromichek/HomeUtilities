namespace HomeUtilities.Core.Common;

/// <summary>Base class of every persisted entity: GUID id, UTC timestamps and soft delete.</summary>
public abstract class SyncEntity
{
    /// <summary>Initializes a new entity with a fresh version-7 GUID.</summary>
    protected SyncEntity()
    {
        Id = Guid.CreateVersion7();
    }

    /// <summary>Identifier generated on the device.</summary>
    public Guid Id { get; set; }

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Timestamp (UTC) of the last change.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Soft-delete flag; user data is never removed physically.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Sets <see cref="UpdatedAt"/> to the current time and <see cref="CreatedAt"/> too if it is not set yet.
    /// </summary>
    /// <exception cref="ArgumentException">The clock does not return a UTC time.</exception>
    public void Touch(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var now = clock.UtcNow;
        if (now.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The clock must return a UTC time (zero offset).", nameof(clock));
        }

        UpdatedAt = now;
        if (CreatedAt == default)
        {
            CreatedAt = now;
        }
    }

    /// <summary>Marks the entity as deleted and touches it.</summary>
    public void MarkDeleted(IClock clock)
    {
        Touch(clock);
        IsDeleted = true;
    }
}
