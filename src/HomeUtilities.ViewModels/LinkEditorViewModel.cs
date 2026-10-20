using CommunityToolkit.Mvvm.ComponentModel;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>Dialog that adds or edits one web link of a supplier.</summary>
public sealed partial class LinkEditorViewModel : EditorDialogViewModel
{
    private readonly ILocalizer _localizer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private IReadOnlyList<EnumOption> _kinds = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedKindIndex))]
    private EnumOption? _selectedKind;

    [ObservableProperty]
    private string _linkTitle = string.Empty;

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UrlError), nameof(LinkTitleError))]
    private IReadOnlyDictionary<string, string> _errors = new Dictionary<string, string>();

    [ObservableProperty]
    private string _kindLabel = string.Empty;

    [ObservableProperty]
    private string _linkTitleLabel = string.Empty;

    [ObservableProperty]
    private string _urlLabel = string.Empty;

    /// <param name="existing">The link to edit, or null to add a new one.</param>
    public LinkEditorViewModel(ILocalizer localizer, Link? existing)
        : base(localizer)
    {
        _localizer = localizer;
        IsNew = existing is null;

        var kind = existing?.Kind ?? LinkKind.Website;
        Kinds = EnumText.Options<LinkKind>(localizer);
        SelectedKind = Kinds.First(k => k.Value.Equals(kind));
        LinkTitle = existing?.Title ?? string.Empty;
        Url = existing?.Url ?? string.Empty;
        RefreshTexts();
    }

    public bool IsNew { get; }

    /// <summary>Index into <see cref="Kinds"/> for the picker; out-of-range values (the picker clearing itself) are ignored.</summary>
    public int SelectedKindIndex
    {
        get => SelectedKind is null ? -1 : Kinds.ToList().IndexOf(SelectedKind);
        set
        {
            if (value >= 0 && value < Kinds.Count)
            {
                SelectedKind = Kinds[value];
            }
        }
    }

    public string UrlError => Lookup(nameof(Link.Url));

    public string LinkTitleError => Lookup(nameof(Link.Title));

    /// <summary>The validated link (kind, title, URL only); meaningful only after <see cref="EditorDialogViewModel.Saved"/>.</summary>
    public Link? Result { get; private set; }

    protected override bool TryApply()
    {
        var candidate = new Link
        {
            Kind = SelectedKind?.As<LinkKind>() ?? LinkKind.Website,
            Title = string.IsNullOrWhiteSpace(LinkTitle) ? null : LinkTitle.Trim(),
            Url = Url.Trim(),
        };

        var errors = LinkValidator.Validate(candidate).Errors;
        Errors = errors
            .GroupBy(e => e.Field ?? string.Empty)
            .ToDictionary(g => g.Key, g => string.Join("\n", g.Select(e => ValidationText.For(_localizer, e))));
        if (errors.Count > 0)
        {
            return false;
        }

        Result = candidate;
        return true;
    }

    protected override void RefreshTexts()
    {
        base.RefreshTexts();
        var kind = SelectedKind?.Value;
        Kinds = EnumText.Options<LinkKind>(_localizer);
        if (kind is not null)
        {
            SelectedKind = Kinds.First(k => k.Value.Equals(kind));
        }

        Title = _localizer[IsNew ? "LinkNew" : "LinkEdit"];
        KindLabel = _localizer["LinkKindLabel"];
        LinkTitleLabel = _localizer["LinkTitleLabel"];
        UrlLabel = _localizer["LinkUrlLabel"];
    }

    private string Lookup(string key) => Errors.TryGetValue(key, out var message) ? message : string.Empty;
}
