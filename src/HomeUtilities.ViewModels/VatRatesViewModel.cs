using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core;
using HomeUtilities.Core.Abstractions;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>The VAT catalog (Settings → VAT rates).</summary>
public sealed partial class VatRatesViewModel : MasterDetailViewModel<VatRateEditViewModel>
{
    protected override HelpTopic GuideTopic => HelpTopic.VatRates;

    private readonly IVatRateStore _store;
    private readonly ILocalizer _localizer;
    private readonly IClock _clock;

    private IReadOnlyList<VatRate> _rates = [];

    /// <summary>The cards; updated in place so the list keeps its scroll position.</summary>
    public ObservableCollection<VatRateItemViewModel> Items { get; } = [];

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

    public override async Task ReloadAsync(CancellationToken ct = default)
    {
        var loaded = await OffUiAsync(() => _store.ListAsync(ct), ct);
        ct.ThrowIfCancellationRequested();
        _rates = loaded;
        RebuildItems();
        IsLoaded = true;
    }


    protected override void RefreshTexts()
    {
        Title = _localizer["SectionVatRates"];
        EmptyText = _localizer["VatRatesEmpty"];
        AddText = _localizer["VatRateAdd"];
        HelpText = _localizer["SectionHelp"];
        BannerText = _localizer["VatRatesBanner"];
        RebuildItems();
    }

    protected override bool ContainsItem(Guid id) => _rates.Any(x => x.Id == id);

    protected override void MarkSelectedItem()
    {
        foreach (var item in Items)
        {
            item.IsSelected = HighlightedId == item.Id;
        }
    }

    private void SetItems(IReadOnlyList<VatRateItemViewModel> items)
    {
        ItemsSync.Update(Items, items, item => item.Id);
        RaiseItemsChanged();
    }

    private void RebuildItems()
    {
        SetItems(
        [
            .. _rates
                .OrderBy(v => v.Code, StringComparer.Ordinal)
                .Select(v => new VatRateItemViewModel(v, _clock.Today, _localizer, id => SelectCommand.Execute(id))),
        ]);
        MarkSelectedItem();
    }
}
