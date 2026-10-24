namespace HomeUtilities.ViewModels.Tests;

public class NavigationGuardTests
{
    private readonly FakeActiveEditorProvider _provider = new();

    [Fact]
    public async Task NoActiveEditor_AllowsNavigation()
    {
        Assert.True(await new NavigationGuard(_provider).CanLeaveAsync());
    }

    [Fact]
    public async Task CleanEditor_AllowsNavigationWithoutAsking()
    {
        var editor = new FakeGuarded { HasUnsavedChanges = false };
        _provider.Current = editor;

        Assert.True(await new NavigationGuard(_provider).CanLeaveAsync());
        Assert.Equal(0, editor.Asked);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DirtyEditor_ReturnsItsAnswer(bool editorAnswer)
    {
        var editor = new FakeGuarded { HasUnsavedChanges = true, Answer = editorAnswer };
        _provider.Current = editor;

        Assert.Equal(editorAnswer, await new NavigationGuard(_provider).CanLeaveAsync());
        Assert.Equal(1, editor.Asked);
    }

    [Fact]
    public async Task OnlyTheActiveEditorIsAsked()
    {
        var other = new FakeGuarded { HasUnsavedChanges = true };
        var active = new FakeGuarded { HasUnsavedChanges = true };
        _provider.Current = active;

        await new NavigationGuard(_provider).CanLeaveAsync();

        Assert.Equal(1, active.Asked);
        Assert.Equal(0, other.Asked);
    }

    [Fact]
    public async Task SecondRequestWhileTheDialogIsOpen_IsRefused()
    {
        var editor = new FakeGuarded { HasUnsavedChanges = true, Gate = new TaskCompletionSource<bool>() };
        _provider.Current = editor;
        var guard = new NavigationGuard(_provider);

        var first = guard.CanLeaveAsync();
        var second = await guard.CanLeaveAsync();
        editor.Gate.SetResult(true);

        Assert.False(second);
        Assert.True(await first);
        Assert.Equal(1, editor.Asked);

        // The guard is free again afterwards.
        editor.Gate = null;
        Assert.True(await guard.CanLeaveAsync());
    }

    private sealed class FakeActiveEditorProvider : IActiveEditorProvider
    {
        public IUnsavedChangesGuarded? Current { get; set; }
    }

    private sealed class FakeGuarded : IUnsavedChangesGuarded
    {
        public bool HasUnsavedChanges { get; set; }

        public bool Answer { get; set; } = true;

        public TaskCompletionSource<bool>? Gate { get; set; }

        public int Asked { get; private set; }

        public async Task<bool> CanLeaveAsync()
        {
            Asked++;
            return Gate is null ? Answer : await Gate.Task;
        }
    }
}
