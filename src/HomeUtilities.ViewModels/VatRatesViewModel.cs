using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The VAT catalog (Settings → VAT rates); two-pane like Categories.</summary>
public sealed partial class VatRatesViewModel : MasterDetailViewModel<VatRateEditViewModel>
{
    protected override HelpTopic GuideTopic => HelpTopic.VatRates;

    private readonly IVatRateStore _store;
    private readonly ILocalizer _localizer;
    private readonly IClock _clock;

    private IReadOnlyList<VatRate> _rates = [];

    [ObservableProperty]
    private IReadOnlyList<VatRateItemViewModel> _items = [];

    [ObservableProperty]
    private string _bannerText = string.Empty;

    public VatRatesViewModel(
        IVatRateStore store,
        IClock clock,
        ILocalizer localizer,
        INavigationService navigation,
        ILayoutModeService layoutModeService,
        ISectionSelectionMemory selectionMemory,
        Func<VatRateEditViewModel> editorFactory)
        : base(navigation, layoutModeService, editorFactory, Routes.VatRateEdit, selectionMemory)
    {
        _store = store;
        _clock = clock;
        _localizer = localizer;
        RefreshTexts();
    }

    protected override int TotalCount => Items.Count;

    public override async Task ReloadAsync()
    {
        _rates = await _store.ListAsync();
        RebuildItems();
        IsLoaded = true;
    }

    partial void OnItemsChanged(IReadOnlyList<VatRateItemViewModel> value) => RaiseItemsChanged();

    protected override void RefreshTexts()
    {
        Title = _localizer["SectionVatRates"];
        EmptyText = _localizer["VatRatesEmpty"];
        AddText = _localizer["VatRateAdd"];
        HelpText = _localizer["SectionHelp"];
        SelectHintText = _localizer["VatRateSelectHint"];
        BannerText = _localizer["VatRatesBanner"];
        RebuildItems();
    }

    protected override bool ContainsItem(Guid id) => _rates.Any(x => x.Id == id);

    protected override void MarkSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsSelected = SelectedEditor is { IsNew: false } editor && editor.EntityId == item.Id;
        }
    }

    private void RebuildItems()
    {
        Items =
        [
            .. _rates
                .OrderBy(v => v.Code, StringComparer.Ordinal)
                .Select(v => new VatRateItemViewModel(v, _clock.Today, _localizer, id => SelectCommand.Execute(id))),
        ];
        MarkSelectedItem();
    }
}
