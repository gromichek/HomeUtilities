using HomeUtilities.Core.Common;
using HomeUtilities.Core.Tests.Fakes;

namespace HomeUtilities.Core.Tests.Common;

public class SyncEntityTests
{
    private sealed class TestEntity : SyncEntity;

    private static readonly DateTimeOffset T1 = new(2026, 10, 19, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void New_HasVersion7Id()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal(7, entity.Id.Version);
    }

    [Fact]
    public void New_TwoEntitiesHaveDifferentIds() =>
        Assert.NotEqual(new TestEntity().Id, new TestEntity().Id);

    [Fact]
    public void Touch_SetsCreatedAtOnce_AndUpdatedAtEveryTime()
    {
        var clock = new FakeClock(T1);
        var entity = new TestEntity();

        entity.Touch(clock);
        clock.UtcNow = T1.AddHours(2);
        entity.Touch(clock);

        Assert.Equal(T1, entity.CreatedAt);
        Assert.Equal(T1.AddHours(2), entity.UpdatedAt);
    }

    [Fact]
    public void MarkDeleted_SetsFlagAndTouches()
    {
        var entity = new TestEntity();

        entity.MarkDeleted(new FakeClock(T1));

        Assert.True(entity.IsDeleted);
        Assert.Equal(T1, entity.UpdatedAt);
        Assert.Equal(T1, entity.CreatedAt);
    }

    [Fact]
    public void Touch_NonUtcClock_Throws()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.FromHours(2)));
        var entity = new TestEntity();

        Assert.Throws<ArgumentException>(() => entity.Touch(clock));
        Assert.Throws<ArgumentException>(() => entity.MarkDeleted(clock));
        Assert.False(entity.IsDeleted);
    }
}
