using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Billing;

namespace HomeUtilities.ViewModels.Tests;

/// <summary>The hint under "Applies to" in the custom field editor says where the field will appear (T-036c).</summary>
public sealed class CustomFieldTargetHintTests
{
    [Fact]
    public void Hint_FollowsTheChosenTarget_InRussian()
    {
        var editor = new CustomFieldEditorViewModel(ResxLocalizer.Russian, null, _ => false);

        Assert.Equal("Поле счёта появляется в карточке «Дополнительные поля» счёта.", editor.TargetHint);

        editor.SelectedTargetIndex = editor.Targets.ToList().FindIndex(t => t.Value.Equals(CustomFieldTarget.BillLine));

        Assert.Equal("Поле строки счёта появляется внутри каждой строки счёта.", editor.TargetHint);
    }

    [Fact]
    public void Hint_English()
    {
        var editor = new CustomFieldEditorViewModel(ResxLocalizer.English, null, _ => false);

        Assert.Equal("A field of a bill appears in the card “Custom fields” of the bill.", editor.TargetHint);
    }
}
