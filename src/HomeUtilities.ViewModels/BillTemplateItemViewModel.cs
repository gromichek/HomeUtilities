using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Billing;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One row of the bill templates list: name, property and a short description of the schedule.</summary>
public sealed partial class BillTemplateItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public BillTemplateItemViewModel(BillTemplate template, string propertyName, string color, ILocalizer localizer, Action<Guid> onSelect)
    {
        Id = template.Id;
        Name = template.Name;
        PropertyName = propertyName;
        Color = color;
        IsActive = template.IsActive;
        ScheduleText = Describe(template.Schedule, template.Timing, localizer);
        InactiveText = localizer["BillTemplateInactiveBadge"];
        SelectCommand = new RelayCommand(() => onSelect(Id));
    }

    public Guid Id { get; }

    public string Name { get; }

    public string PropertyName { get; }

    /// <summary>Colour of the category as <c>#RRGGBB</c>.</summary>
    public string Color { get; }

    public bool IsActive { get; }

    public bool IsInactive => !IsActive;

    public string InactiveText { get; }

    /// <summary>"Monthly from 01.2026 · in advance".</summary>
    public string ScheduleText { get; }

    public IRelayCommand SelectCommand { get; }

    /// <summary>A one-line description of a schedule for lists.</summary>
    public static string Describe(Schedule schedule, BillingTiming timing, ILocalizer localizer)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(localizer);

        if (schedule.Kind == ScheduleKind.Once || schedule.StartMonth is not { } start)
        {
            return localizer["BillTemplateScheduleOnce"];
        }

        var from = BillText.Period(start);
        var kind = schedule.Kind switch
        {
            ScheduleKind.Monthly => localizer.Get("BillTemplateScheduleMonthly", from),
            ScheduleKind.EveryNMonths => localizer.Get("BillTemplateScheduleEvery", schedule.EveryMonths ?? 0, from),
            _ => localizer.Get("BillTemplateScheduleYearly", from),
        };
        return $"{kind} · {EnumText.Get(localizer, timing)}";
    }
}
