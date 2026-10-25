using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>
/// Dialog that adds or edits one detail line of a bill: description, quantity, unit, unit price, the amounts (any two of
/// Net / VAT % / Total), an optional category of its own, a note and the line-level custom fields. The result is a
/// <see cref="BillLine"/>; nothing is stored here.
/// </summary>
public sealed partial class BillLineEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;
    private readonly BillLine _original;
    private readonly IReadOnlyList<ChoiceOption> _categories;
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private string _quantityText;

    [ObservableProperty]
    private string _unit;

    [ObservableProperty]
    private string _unitPriceText;

    [ObservableProperty]
    private string _note;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCategoryIndex))]
    private IReadOnlyList<ChoiceOption> _categoryChoices = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCategoryIndex))]
    private ChoiceOption? _selectedCategory;

    [ObservableProperty]
    private BillLineEditorTexts _texts;

    [ObservableProperty]
    private BillEditTexts _common;

    /// <param name="localizer">Texts.</param>
    /// <param name="existing">The line to edit, or null for a new one.</param>
    /// <param name="categories">The categories offered for the line's own category.</param>
    /// <param name="definitions">The contract's custom field definitions that apply to bill lines.</param>
    /// <param name="today">Today, the start value of a date field.</param>
    public BillLineEditorViewModel(
        ILocalizer localizer,
        BillLine? existing,
        IReadOnlyList<Category> categories,
        IEnumerable<CustomFieldDefinition> definitions,
        DateOnly today)
        : base(localizer)
    {
        _localizer = localizer;
        _original = existing ?? new BillLine();
        IsNew = existing is null;
        _categories = [.. categories.Where(c => !c.IsArchived || c.Id == existing?.CategoryId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ChoiceOption(c.Id, CategoryNames.DisplayName(c, localizer), c.Color))];

        _texts = new BillLineEditorTexts(localizer);
        _common = new BillEditTexts(localizer);
        _description = _original.Description;
        _quantityText = _original.Quantity is { } q ? DecimalInput.Format(q, CultureInfo.CurrentCulture, 4) : string.Empty;
        _unit = _original.Unit ?? string.Empty;
        _unitPriceText = _original.UnitPrice is { } p ? DecimalInput.Format(p, CultureInfo.CurrentCulture, 6) : string.Empty;
        _note = _original.Note ?? string.Empty;
        if (existing is not null)
        {
            Amounts.Load(new AmountSet(existing.Net, existing.VatPercent, existing.Vat, existing.Gross));
        }

        FieldRows = CustomFieldValueRowViewModel.Build(definitions, _original.CustomFields, today, localizer["BillFieldDateSet"]);
        BuildCategoryChoices();
        _selectedCategory = _original.CategoryId is { } id ? CategoryChoices.FirstOrDefault(c => c.Id == id) : CategoryChoices[0];
        _selectedCategory ??= CategoryChoices[0];
        Title = localizer[IsNew ? "BillLineNew" : "BillLineEdit"];
    }

    public bool IsNew { get; }

    /// <summary>The amount fields of the line.</summary>
    public AmountEditor Amounts { get; } = new();

    /// <summary>The line-level custom fields.</summary>
    public IReadOnlyList<CustomFieldValueRowViewModel> FieldRows { get; }

    public bool HasFields => FieldRows.Count > 0;

    /// <summary>The validated line; meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public BillLine? Result { get; private set; }

    public int SelectedCategoryIndex
    {
        get => SelectedCategory is null ? -1 : CategoryChoices.ToList().IndexOf(SelectedCategory);
        set
        {
            if (value >= 0 && value < CategoryChoices.Count)
            {
                SelectedCategory = CategoryChoices[value];
            }
        }
    }

    public string DescriptionError => ErrorOf("Description");

    public string QuantityError => ErrorOf("Quantity");

    public string UnitPriceError => ErrorOf("UnitPrice");

    public string NetError => ErrorOf("Net");

    public string VatPercentError => ErrorOf("VatPercent");

    public string GrossError => ErrorOf("Gross");

    public string NoteError => ErrorOf(nameof(BillLine.Note));

    protected override bool TryApply()
    {
        var errors = new List<ValidationError>();
        var quantity = AmountEditor.ReadOptional(QuantityText, "Quantity", errors);
        var unitPrice = AmountEditor.ReadOptional(UnitPriceText, "UnitPrice", errors);
        errors.AddRange(Amounts.TryRead(out var amounts));
        var values = CustomFieldValueRowViewModel.Read(FieldRows, errors, "CustomFields", out var rowOfValue);

        var line = new BillLine
        {
            Id = _original.Id,
            CreatedAt = _original.CreatedAt,
            BillId = _original.BillId,
            SortOrder = _original.SortOrder,
            Description = Description.Trim(),
            Quantity = quantity,
            Unit = string.IsNullOrWhiteSpace(Unit) ? null : Unit.Trim(),
            UnitPrice = unitPrice,
            Net = amounts?.Net,
            VatPercent = amounts?.VatPercent,
            Vat = amounts?.Vat,
            Gross = amounts?.Gross ?? 0m,
            CategoryId = SelectedCategory is { } c && c.Id != Guid.Empty ? c.Id : null,
            CustomFields = values,
            Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
        };

        if (string.IsNullOrWhiteSpace(line.Description))
        {
            errors.Add(new ValidationError(ErrorCodes.BillLineDescriptionRequired, "Description"));
        }
        else if (line.Description.Length > InputLimits.Text)
        {
            errors.Add(new ValidationError(CommonErrorCodes.TextTooLong, "Description", [InputLimits.Text]));
        }

        if (line.Note is { Length: > InputLimits.Notes })
        {
            errors.Add(new ValidationError(ErrorCodes.NotesTooLong, nameof(BillLine.Note), [InputLimits.Notes]));
        }

        for (var i = 0; i < values.Count; i++)
        {
            foreach (var error in CustomFieldValidator.Validate(values[i]).Errors)
            {
                errors.Add(error with { Field = $"CustomFields[{rowOfValue[i]}].Value" });
            }
        }

        _errors = errors
            .GroupBy(e => e.Field ?? string.Empty)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => ValidationText.For(_localizer, e))));
        RaiseErrorProperties();
        foreach (var (row, index) in FieldRows.Select((r, i) => (r, i)))
        {
            row.ErrorText = _errors.GetValueOrDefault($"CustomFields[{index}].Value", string.Empty);
        }

        if (errors.Count > 0)
        {
            return false;
        }

        Result = line;
        return true;
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        Texts = new BillLineEditorTexts(_localizer);
        Common = new BillEditTexts(_localizer);
        Title = _localizer[IsNew ? "BillLineNew" : "BillLineEdit"];
        var selected = SelectedCategory?.Id;
        BuildCategoryChoices();
        SelectedCategory = CategoryChoices.FirstOrDefault(c => c.Id == selected) ?? CategoryChoices[0];
    }

    private void BuildCategoryChoices() =>
        CategoryChoices = [new ChoiceOption(Guid.Empty, _localizer["BillLineSameCategory"], string.Empty), .. _categories];

    private string ErrorOf(string field) => _errors.GetValueOrDefault(field, string.Empty);

    private void RaiseErrorProperties()
    {
        OnPropertyChanged(nameof(DescriptionError));
        OnPropertyChanged(nameof(QuantityError));
        OnPropertyChanged(nameof(UnitPriceError));
        OnPropertyChanged(nameof(NetError));
        OnPropertyChanged(nameof(VatPercentError));
        OnPropertyChanged(nameof(GrossError));
        OnPropertyChanged(nameof(NoteError));
    }
}
