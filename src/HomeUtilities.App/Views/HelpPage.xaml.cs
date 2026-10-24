using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>
/// The user guide. As a menu section it opens at the top; pushed with query parameter <c>topic</c> (a
/// <see cref="HelpTopic"/>) it opens at that section, and standard Back returns to the screen.
/// </summary>
public partial class HelpPage : PageBase, IQueryAttributable
{
    public HelpPage(HelpViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HelpViewModel.Html))
            {
                ShowHtml(viewModel.Html);
            }
        };
    }

    private void ShowHtml(string html) =>
        Browser.Source = html.Length == 0 ? null : new HtmlWebViewSource { Html = html };

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is HelpViewModel viewModel && query.TryGetValue(HelpNavigation.TopicParameter, out var value))
        {
            viewModel.Topic = value switch
            {
                HelpTopic topic => topic,
                string text when Enum.TryParse<HelpTopic>(text, out var parsed) => parsed,
                _ => null,
            };
        }
    }

    private async void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (BindingContext is HelpViewModel viewModel && e.Url is { } url && viewModel.ShouldIntercept(url))
        {
            e.Cancel = true;
            await viewModel.InterceptAsync(url);
        }
    }
}
