using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>A supplier link shown muted in the contract editor; it can only be opened.</summary>
public sealed class InheritedLinkRowViewModel
{
    public InheritedLinkRowViewModel(Link link, ILocalizer localizer, Func<Link, Task> open)
    {
        Url = link.Url;
        DisplayText = string.IsNullOrWhiteSpace(link.Title) ? EnumText.Get(localizer, link.Kind) : link.Title;
        FromSupplierText = localizer["LinkFromSupplier"];
        OpenText = localizer["LinkOpen"];
        OpenCommand = new AsyncRelayCommand(() => open(link));
    }

    public string DisplayText { get; }

    public string Url { get; }

    public string FromSupplierText { get; }

    public string OpenText { get; }

    public IRelayCommand OpenCommand { get; }
}
