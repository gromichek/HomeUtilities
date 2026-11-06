using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// The phone frame around a dialog view: top bar with ←, title and ✓ (see <c>DialogFrame.xaml</c>). The dialog views keep their own
/// title and Cancel / Save row because the desktop popup shows those; here the frame replaces them, so
/// <see cref="HideOwnChrome"/> hides them.
/// </summary>
public partial class DialogFrame : ContentView
{
    public DialogFrame(View body, EditorDialogViewModel editor)
    {
        InitializeComponent();
        BindingContext = editor;
        HideOwnChrome(body, editor);
        Body.Content = body;
    }

    /// <summary>
    /// Hides what the frame's top bar replaces: the dialog's title label (the one showing the view model's title) and the buttons
    /// bound to its Cancel or primary command, together with a row that holds nothing else. Other buttons (a wizard's Back / Next)
    /// stay.
    /// </summary>
    internal static void HideOwnChrome(View root, EditorDialogViewModel editor)
    {
        var titleHidden = false;
        Visit(root);

        void Visit(IView? node)
        {
            switch (node)
            {
                case Label label when !titleHidden && editor.Title.Length > 0 && label.Text == editor.Title:
                    label.IsVisible = false;
                    titleHidden = true;
                    break;
                case Button button when IsFrameCommand(button):
                    button.RemoveBinding(VisualElement.IsVisibleProperty);
                    button.IsVisible = false;
                    break;
                case ScrollView scroll:
                    Visit(scroll.Content);
                    break;
                case ContentView content:
                    Visit(content.Content);
                    break;
                case Border border:
                    Visit(border.Content);
                    break;
                case Layout layout:
                    foreach (var child in layout.Children.ToList())
                    {
                        Visit(child);
                    }

                    // A row that only held the hidden buttons takes no space either.
                    if (layout.Children.Count > 0 && layout.Children.All(c => c is Button { IsVisible: false }))
                    {
                        layout.IsVisible = false;
                    }

                    break;
            }
        }

        bool IsFrameCommand(Button button) =>
            button.Command is { } command && (ReferenceEquals(command, editor.CancelCommand) || ReferenceEquals(command, editor.PrimaryCommand));
    }
}
