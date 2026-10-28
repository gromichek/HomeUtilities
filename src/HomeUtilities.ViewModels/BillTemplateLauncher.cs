using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Opens the bill template editor with a new template that starts from a contract or from a bill.</summary>
public sealed class BillTemplateLauncher(Func<BillTemplateEditViewModel> editorFactory, INavigationService navigation)
{
    /// <summary>"Template from this contract": property, category, supplier, timing, custom fields and the newest bill's amount.</summary>
    public Task FromContractAsync(Guid contractId) => OpenAsync(editor => editor.PresetContractId = contractId);

    /// <summary>"Save as template": everything the bill carries, as the default of the template.</summary>
    public Task FromBillAsync(Guid billId) => OpenAsync(editor => editor.PresetBillId = billId);

    private async Task OpenAsync(Action<BillTemplateEditViewModel> preset)
    {
        var editor = editorFactory();
        editor.IsHostedInPage = true;
        preset(editor);
        await editor.LoadAsync(null);
        await navigation.GoToAsync(Routes.BillTemplateEdit, new Dictionary<string, object> { [EditorNavigation.EditorParameter] = editor });
    }
}
