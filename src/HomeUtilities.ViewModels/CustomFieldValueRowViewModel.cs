using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Common;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One custom field of a bill or bill line in an editor: the name, and the value typed for its kind.</summary>
public sealed partial class CustomFieldValueRowViewModel : ObservableObject
{
    private static readonly System.Text.RegularExpressions.Regex IndexInField = new(@"^CustomFields\[(\d+)\]\.", System.Text.RegularExpressions.RegexOptions.Compiled);

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _hasDate;

    [ObservableProperty]
    private DateOnly _date;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorText = string.Empty;

    private bool _loading;

    private CustomFieldValueRowViewModel(Guid? definitionId, string name, CustomFieldKind kind)
    {
        DefinitionId = definitionId;
        Name = name;
        Kind = kind;
    }

    /// <summary>The definition the value belongs to; null for a value whose definition was removed.</summary>
    public Guid? DefinitionId { get; }

    public string Name { get; }

    public CustomFieldKind Kind { get; }

    public bool IsText => Kind == CustomFieldKind.Text;

    public bool IsNumber => Kind is CustomFieldKind.Number or CustomFieldKind.Money;

    public bool IsDate => Kind == CustomFieldKind.Date;

    public bool HasError => ErrorText.Length > 0;

    /// <summary>The label of the "set a date" switch of a date field.</summary>
    public string DateSetText { get; set; } = string.Empty;

    /// <summary>Raised when the user changed the value.</summary>
    public event Action? Edited;

    partial void OnTextChanged(string value) => RaiseEdited();

    partial void OnHasDateChanged(bool value) => RaiseEdited();

    partial void OnDateChanged(DateOnly value) => RaiseEdited();

    private void RaiseEdited()
    {
        if (!_loading)
        {
            Edited?.Invoke();
        }
    }

    /// <summary>
    /// The rows of an editor: one per definition (sorted), filled with the stored value, followed by stored values
    /// whose definition no longer exists (removing a definition keeps old values).
    /// </summary>
    public static IReadOnlyList<CustomFieldValueRowViewModel> Build(
        IEnumerable<CustomFieldDefinition> definitions, IEnumerable<CustomFieldValue> stored, DateOnly today, string dateSetText)
    {
        var values = stored.ToList();
        var rows = new List<CustomFieldValueRowViewModel>();
        var used = new HashSet<CustomFieldValue>();
        foreach (var definition in definitions.Where(d => !d.IsDeleted).OrderBy(d => d.SortOrder).ThenBy(d => d.CreatedAt))
        {
            var value = values.FirstOrDefault(v => v.DefinitionId == definition.Id);
            if (value is not null)
            {
                used.Add(value);
            }

            rows.Add(Create(definition.Id, definition.Name, definition.Kind, value, today, dateSetText));
        }

        foreach (var orphan in values.Where(v => !used.Contains(v)))
        {
            rows.Add(Create(orphan.DefinitionId, orphan.Name, orphan.Kind, orphan, today, dateSetText));
        }

        return rows;
    }

    /// <summary>
    /// Reads the rows into stored values (empty rows are skipped). A number that cannot be read is an error under
    /// <c>{fieldPrefix}[row].Value</c>. <paramref name="rowOfValue"/> tells which row each returned value came from, to
    /// map validator errors (<c>CustomFields[n].Value</c>, n counting the values) back to rows.
    /// </summary>
    public static List<CustomFieldValue> Read(
        IReadOnlyList<CustomFieldValueRowViewModel> rows, List<ValidationError> errors, string fieldPrefix, out List<int> rowOfValue)
    {
        var values = new List<CustomFieldValue>();
        rowOfValue = [];
        for (var i = 0; i < rows.Count; i++)
        {
            var (value, error) = rows[i].ToValue();
            if (error is not null)
            {
                errors.Add(new ValidationError(error, $"{fieldPrefix}[{i}].Value"));
            }
            else if (value is not null)
            {
                values.Add(value);
                rowOfValue.Add(i);
            }
        }

        return values;
    }

    /// <summary>The index of the row in an error field like <c>CustomFields[2].Value</c>; null for other fields.</summary>
    public static int? RowIndexOf(string? field, string prefix = "CustomFields")
    {
        if (field is null)
        {
            return null;
        }

        var match = prefix == "CustomFields"
            ? IndexInField.Match(field)
            : System.Text.RegularExpressions.Regex.Match(field, $@"^{System.Text.RegularExpressions.Regex.Escape(prefix)}\[(\d+)\]\.");
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private (CustomFieldValue? Value, string? Error) ToValue()
    {
        ErrorText = string.Empty;
        switch (Kind)
        {
            case CustomFieldKind.Date:
                return HasDate
                    ? (new CustomFieldValue(DefinitionId, Name, Kind, Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), null)
                    : (null, null);

            case CustomFieldKind.Number or CustomFieldKind.Money:
                if (string.IsNullOrWhiteSpace(Text))
                {
                    return (null, null);
                }

                if (!DecimalInput.TryParse(Text, CultureInfo.CurrentCulture, out var number))
                {
                    return (null, CommonErrorCodes.NumberInvalid);
                }

                return Math.Abs(number) > AmountEditor.MaxAmount
                    ? (null, CommonErrorCodes.NumberTooLarge)
                    : (new CustomFieldValue(DefinitionId, Name, Kind, number.ToString(CultureInfo.InvariantCulture)), null);

            default:
                return string.IsNullOrWhiteSpace(Text)
                    ? (null, null)
                    : (new CustomFieldValue(DefinitionId, Name, Kind, Text.Trim()), null);
        }
    }

    private static CustomFieldValueRowViewModel Create(
        Guid? definitionId, string name, CustomFieldKind kind, CustomFieldValue? value, DateOnly today, string dateSetText)
    {
        var row = new CustomFieldValueRowViewModel(definitionId, name, kind) { DateSetText = dateSetText, _loading = true };
        row.Date = today;
        switch (kind)
        {
            case CustomFieldKind.Date:
                if (value?.AsDate() is { } date)
                {
                    row.HasDate = true;
                    row.Date = date;
                }

                break;
            case CustomFieldKind.Number or CustomFieldKind.Money:
                row.Text = value?.AsDecimal() is { } number ? DecimalInput.Format(number, CultureInfo.CurrentCulture, 4) : value?.Value ?? string.Empty;
                break;
            default:
                row.Text = value?.Value ?? string.Empty;
                break;
        }

        row._loading = false;
        return row;
    }
}
