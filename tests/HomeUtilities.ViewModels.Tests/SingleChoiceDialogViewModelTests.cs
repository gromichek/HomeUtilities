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
        Assert.True(dialog.Saved);
    }

    [Fact]
    public void A_plain_row_selects_and_closes_the_dialog_at_once()
    {
        var dialog = Create(1);
        var closed = 0;
        dialog.CloseRequested += () => closed++;

        dialog.Items[1].ChooseCommand.Execute(null);

        Assert.True(dialog.Saved);
        Assert.Equal(1, closed);
        Assert.Equal(2, dialog.SelectedValue);
        Assert.False(dialog.ShowOk);
    }

    [Fact]
    public void Two_options_with_the_same_text_return_their_own_values()
    {
        // The app maps the index back to the record (two contracts "Gas" after a change of supplier).
        var dialog = new SingleChoiceDialogViewModel(
            _kit.Localizer,
            "Contract",
            [new SingleChoiceOption(0, "Gas"), new SingleChoiceOption(1, "Gas")],
            0);

        dialog.Items[1].ChooseCommand.Execute(null);

        Assert.True(dialog.Saved);
        Assert.Equal(1, dialog.SelectedValue);
        Assert.Equal([false, true], dialog.Items.Select(i => i.IsSelected));
    }

    [Fact]
    public void A_row_with_extra_inputs_stays_open_until_OK()
    {
        var dialog = new SingleChoiceDialogViewModel(
            _kit.Localizer,
            "Period",
            [new SingleChoiceOption(1, "This year"), new SingleChoiceOption(2, "Custom…", HasExtraInputs: true)],
            1);

        Assert.True(dialog.ShowOk);
        dialog.Items[1].ChooseCommand.Execute(null);
        Assert.False(dialog.Saved);
        Assert.Equal(2, dialog.SelectedValue);

        dialog.ConfirmCommand.Execute(null);
        Assert.True(dialog.Saved);

        // A plain row of the same dialog still answers at once.
        var other = new SingleChoiceDialogViewModel(
            _kit.Localizer,
            "Period",
            [new SingleChoiceOption(1, "This year"), new SingleChoiceOption(2, "Custom…", HasExtraInputs: true)],
            2);
        other.Items[0].ChooseCommand.Execute(null);
        Assert.True(other.Saved);
        Assert.Equal(1, other.SelectedValue);
    }

    [Fact]
    public void An_action_row_comes_last_is_never_preselected_and_acts_at_once()
    {
        var dialog = new SingleChoiceDialogViewModel(
            _kit.Localizer,
            "Supplier",
            [new SingleChoiceOption(-1, "＋ New supplier…", IsAction: true), new SingleChoiceOption(0, "A"), new SingleChoiceOption(1, "B")],
            1);

        Assert.Equal(["A", "B", "＋ New supplier…"], dialog.Items.Select(i => i.Text));
        Assert.Equal([false, true, false], dialog.Items.Select(i => i.IsSelected));
        Assert.Equal([false, false, true], dialog.Items.Select(i => i.IsAction));

        dialog.Items[2].ChooseCommand.Execute(null);

        Assert.True(dialog.Saved);
        Assert.Equal(-1, dialog.SelectedValue);
    }

    [Fact]
    public void Cancel_closes_without_an_answer()
    {
        var dialog = Create(1);

        dialog.CancelCommand.Execute(null);

        Assert.False(dialog.Saved);
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
