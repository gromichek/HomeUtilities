using System.ComponentModel;
using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.App.Views;

/// <summary>
/// The charges screen. As a menu section it opens on the remembered property; as a pushed page ("Show charges" in the
/// contract editor) the query parameters <c>propertyId</c> and <c>contractId</c> choose the property and the card to scroll to.
/// </summary>
public partial class ChargesPage : PageBase, IQueryAttributable
{
    public ChargesPage(ChargesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        MonthPicker.MinimumDate = ChargesViewModel.EarliestMonth.FirstDay.ToDateTime(TimeOnly.MinValue);
        MonthPicker.MaximumDate = ChargesViewModel.LatestMonth.LastDay.ToDateTime(TimeOnly.MinValue);
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (BindingContext is not ChargesViewModel viewModel || !TryGuid(query, EditorNavigation.PropertyIdParameter, out var propertyId))
        {
            return;
        }

        Guid? contractId = TryGuid(query, EditorNavigation.ContractIdParameter, out var contract) ? contract : null;
        YearMonth? month = query.TryGetValue(ChargesViewModel.MonthParameter, out var value) && value is string text && YearMonth.TryParse(text, out var parsed)
            ? parsed
            : null;
        _ = viewModel.OpenForAsync(propertyId, contractId, month);
    }

    private static bool TryGuid(IDictionary<string, object> query, string key, out Guid id)
    {
        id = Guid.Empty;
        return query.TryGetValue(key, out var value)
            && value switch
            {
                Guid guid => (id = guid) != Guid.Empty,
                string text => Guid.TryParse(text, out id),
                _ => false,
            };
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChargesViewModel.FocusedCard) && sender is ChargesViewModel { FocusedCard: { } card })
        {
            // Wait for the cards to be laid out, then bring the requested contract to the top.
            Dispatcher.Dispatch(async () =>
            {
                await Task.Delay(200);
                var target = CardList.Children.OfType<VisualElement>().FirstOrDefault(c => ReferenceEquals(c.BindingContext, card));
                if (target is not null)
                {
                    await Scroller.ScrollToAsync(target, ScrollToPosition.Start, true);
                }
            });
        }
    }
}
