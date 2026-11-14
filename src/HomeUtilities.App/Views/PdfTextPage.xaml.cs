using HomeUtilities.ViewModels;

namespace HomeUtilities.App.Views;

/// <summary>The text extracted from a PDF, pushed from the Imports screen with query parameter <c>text</c>; standard Back.</summary>
public partial class PdfTextPage : PageBase, IQueryAttributable
{
    public PdfTextPage(PdfTextViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is PdfTextViewModel viewModel && query.TryGetValue("text", out var value) && value is string text)
        {
            viewModel.Show(text);
        }
    }
}
