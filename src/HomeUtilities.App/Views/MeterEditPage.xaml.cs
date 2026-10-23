using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// Meter editor page (all layouts), see <see cref="EditorPageBase"/>. Without a running editor it loads the
/// meter <c>id</c> (absent = new) of the property <c>propertyId</c>.
/// </summary>
public partial class MeterEditPage : EditorPageBase
{
    public MeterEditPage(MeterEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override void PrepareFresh(PaneEditorViewModel editor, IDictionary<string, object> query)
    {
        if (editor is MeterEditViewModel meter
            && query.TryGetValue(EditorNavigation.PropertyIdParameter, out var value))
        {
            meter.PropertyId = value switch
            {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => Guid.Empty,
            };
        }
    }
}
