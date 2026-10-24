using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One row of the VAT catalog list: name, code and the percent valid today.</summary>
public sealed partial class VatRateItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public VatRateItemViewModel(VatRate rate, DateOnly today, ILocalizer localizer, Action<Guid> onSelect)
    {
        Id = rate.Id;
        Name = rate.Name;
        Code = rate.Code;
        PercentText = rate.PercentAt(today) is { } percent
            ? localizer.Get("VatPercentValue", DecimalInput.Format(percent, System.Globalization.CultureInfo.CurrentCulture, 2))
            : localizer["VatNoPercent"];
        SelectCommand = new RelayCommand(() => onSelect(Id));
    }

    public Guid Id { get; }

    public string Name { get; }

    public string Code { get; }

    /// <summary>"21 %" — the percent valid today.</summary>
    public string PercentText { get; }

    public IRelayCommand SelectCommand { get; }
}
