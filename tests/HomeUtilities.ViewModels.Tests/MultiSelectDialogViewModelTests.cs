namespace HomeUtilities.ViewModels.Tests;

/// <summary>The reusable check-box dialog.</summary>
public class MultiSelectDialogViewModelTests
{
    private readonly ContractTestKit _kit = new(new DateOnly(2026, 10, 27));
    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();
    private readonly Guid _c = Guid.NewGuid();

    private MultiSelectDialogViewModel Create(params Guid[] selected) => new(
        _kit.Localizer,
        "Title",
        [
            new MultiSelectItemViewModel(_a, "A", "#FF0000", false),
            new MultiSelectItemViewModel(_b, "B", string.Empty, false),
            new MultiSelectItemViewModel(_c, "C", string.Empty, false),
        ],
        selected);

    [Fact]
    public void The_given_items_start_checked()
    {
        var dialog = Create(_a, _c);

        Assert.Equal([true, false, true], dialog.Items.Select(i => i.IsSelected));
        Assert.Equal(2, dialog.SelectedCount);
        Assert.True(dialog.Items[0].HasColor);
        Assert.False(dialog.Items[1].HasColor);
    }

    [Fact]
    public void All_and_None_set_every_box()
    {
        var dialog = Create(_a);

        dialog.SelectAllCommand.Execute(null);
        Assert.Equal(3, dialog.SelectedCount);

        dialog.SelectNoneCommand.Execute(null);
        Assert.Equal(0, dialog.SelectedCount);
    }

    [Fact]
    public void OK_is_disabled_while_nothing_is_checked()
    {
        var dialog = Create(_a);
        Assert.True(dialog.ConfirmCommand.CanExecute(null));

        dialog.Items[0].IsSelected = false;

        Assert.False(dialog.HasSelection);
        Assert.False(dialog.ConfirmCommand.CanExecute(null));

        dialog.Items[1].IsSelected = true;

        Assert.True(dialog.ConfirmCommand.CanExecute(null));
    }

    [Fact]
    public void OK_closes_the_dialog_with_the_checked_ids()
    {
        var dialog = Create(_b);
        var closed = false;
        dialog.CloseRequested += () => closed = true;
        dialog.Items[2].IsSelected = true;

        dialog.ConfirmCommand.Execute(null);

        Assert.True(dialog.Saved);
        Assert.True(closed);
        Assert.Equal(new HashSet<Guid> { _b, _c }, dialog.SelectedIds);
    }

    [Fact]
    public void Cancel_closes_the_dialog_without_saving()
    {
        var dialog = Create(_b);
        var closed = false;
        dialog.CloseRequested += () => closed = true;

        dialog.CancelCommand.Execute(null);

        Assert.False(dialog.Saved);
        Assert.True(closed);
    }

    [Fact]
    public void OK_does_nothing_with_nothing_checked()
    {
        var dialog = Create();

        dialog.ConfirmCommand.Execute(null);

        Assert.False(dialog.Saved);
    }
}
