using CommunityToolkit.Mvvm.ComponentModel;

namespace HomeUtilities.ViewModels;

/// <summary>One quantity typed from the supplier bill (e.g. heat in MWh) for a quantity-from-bill tariff component.</summary>
public sealed partial class BillQuantityRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    [ObservableProperty]
    private string _amountText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAmountError))]
    private string _amountErrorText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    private string _warningText = string.Empty;

    public BillQuantityRowViewModel(Guid componentId, string name, string unit, string amountLabel = "", string amountHint = "")
    {
        ComponentId = componentId;
        Name = name;
        Unit = unit;
        AmountLabel = amountLabel;
        AmountHint = amountHint;
    }

    /// <summary>Label of the optional line amount field, e.g. "Amount on the bill (excl. VAT)".</summary>
    public string AmountLabel { get; }

    /// <summary>Hint under the amount field.</summary>
    public string AmountHint { get; }

    /// <summary>The tariff component the quantity belongs to.</summary>
    public Guid ComponentId { get; }

    /// <summary>Name of the component, e.g. "Heat for heating / circulation".</summary>
    public string Name { get; }

    /// <summary>Localized unit of the quantity, e.g. "MWh".</summary>
    public string Unit { get; }

    /// <summary>"Heat for heating / circulation, MWh".</summary>
    public string Label => $"{Name}, {Unit}";

    public bool HasError => ErrorText.Length > 0;

    public bool HasAmountError => AmountErrorText.Length > 0;

    /// <summary>True if the amount from the bill does not match quantity × price.</summary>
    public bool HasWarning => WarningText.Length > 0;

    /// <summary>Raised when the user changed the text.</summary>
    public event Action? Edited;

    partial void OnTextChanged(string value) => Edited?.Invoke();

    partial void OnAmountTextChanged(string value) => Edited?.Invoke();
}
