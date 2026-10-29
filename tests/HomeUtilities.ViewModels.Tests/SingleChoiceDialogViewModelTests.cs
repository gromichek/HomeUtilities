namespace HomeUtilities.ViewModels.Tests;

/// <summary>The reusable radio-row dialog.</summary>
public class SingleChoiceDialogViewModelTests
{
    private readonly ContractTestKit _kit = new(new DateOnly(2026, 10, 29));

    private SingleChoiceDialogViewModel Create(object? selected) => new(
        _kit.Localizer,
        "Report",
        [
            new SingleChoiceOption(1, "Costs", "What you paid"),
            new SingleChoiceOption(2, "Consumption", "How much you used"),
            new SingleChoiceOption(3, "Unit cost"),
        ],
        selected);

    [Fact]
    public void The_given_value_starts_marked()
    {
        var dialog = Create(2);

        Assert.Equal([false, true, false], dialog.Items.Select(i => i.IsSelected));
        Assert.Equal(2, dialog.SelectedValue);
        Assert.Equal(["What you paid", "How much you used", string.Empty], dialog.Items.Select(i => i.Description));
        Assert.Equal([true, true, false], dialog.Items.Select(i => i.HasDescription));
    }

    [Fact]
    public void Choosing_a_row_unmarks_the_others()
    {
        var dialog = Create(1);

        dialog.Items[2].ChooseCommand.Execute(null);

        Assert.Equal([false, false, true], dialog.Items.Select(i => i.IsSelected));
        Assert.Equal(3, dialog.SelectedValue);
    }

    [Fact]
    public void OK_closes_the_dialog_as_saved_and_Cancel_does_not()
    {
        var ok = Create(1);
        ok.ConfirmCommand.Execute(null);
        Assert.True(ok.Saved);

        var cancel = Create(1);
        cancel.CancelCommand.Execute(null);
        Assert.False(cancel.Saved);
    }

    [Fact]
    public void Without_a_marked_row_OK_does_nothing()
    {
        var dialog = Create(null);

        dialog.ConfirmCommand.Execute(null);

        Assert.False(dialog.Saved);
    }

    [Fact]
    public void The_row_count_hint_counts_the_explanation_lines()
    {
        Assert.Equal(6, Create(1).RowCountHint);
    }
}
