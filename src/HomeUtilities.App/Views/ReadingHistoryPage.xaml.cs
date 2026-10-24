using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// Reading history page of the compact layout. Query parameter <c>editor</c> hands over a loaded
/// <see cref="ReadingHistoryViewModel"/>; otherwise <c>id</c> (the meter id) is loaded into the page's own view model.
/// Standard Back; there is nothing unsaved on this page.
/// </summary>
public partial class ReadingHistoryPage : PageBase, IQueryAttributable
{
    public ReadingHistoryPage(ReadingHistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(EditorNavigation.EditorParameter, out var editor) && editor is ReadingHistoryViewModel running)
        {
            BindingContext = running;
        }
        else if (BindingContext is ReadingHistoryViewModel fresh
            && query.TryGetValue(EditorNavigation.IdParameter, out var value))
        {
            var id = value switch
            {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => Guid.Empty,
            };
            _ = fresh.LoadAsync(id);
        }
    }
}
