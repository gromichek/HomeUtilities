using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Domain;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

/// <summary>One row of the meters card in the property editor.</summary>
public sealed class MeterRowViewModel
{
    public MeterRowViewModel(Meter meter, DateOnly today, ILocalizer localizer, Action<Guid> onSelect)
    {
        Id = meter.Id;
        Resource = meter.Resource;
        Title = MeterNames.Title(localizer, meter);
        SerialText = string.IsNullOrWhiteSpace(meter.SerialNumber) || string.IsNullOrWhiteSpace(meter.Location)
            ? string.Empty
            : meter.SerialNumber;
        var registers = meter.Registers.Where(r => !r.IsDeleted).ToList();
        RegistersText = registers.Count == 1 && registers[0].Kind == RegisterKind.Total
            ? string.Empty
            : string.Join(" / ", registers.Select(r => EnumText.Get(localizer, r.Kind)));

        IsRemoved = meter.RemovedOn is not null;
        RemovedText = meter.RemovedOn is { } removedOn ? localizer.Get("MeterRemoved", removedOn) : string.Empty;

        Status = MeterVerification.GetStatus(meter, today);
        BadgeText = MeterNames.BadgeText(localizer, meter, Status);
        SelectCommand = new RelayCommand(() => onSelect(Id));
    }

    public Guid Id { get; }

    public MeterResource Resource { get; }

    /// <summary>Location, or "Meter" plus the serial number when there is no location.</summary>
    public string Title { get; }

    /// <summary>Serial number on its own line; empty when it already is the title.</summary>
    public string SerialText { get; }

    public bool HasSerial => SerialText.Length > 0;

    /// <summary>"Day / Night / Export"; empty for a simple single-register meter.</summary>
    public string RegistersText { get; }

    public bool HasRegisters => RegistersText.Length > 0;

    public bool IsRemoved { get; }

    public string RemovedText { get; }

    public VerificationStatus Status { get; }

    public string BadgeText { get; }

    public bool IsOverdue => Status == VerificationStatus.Overdue;

    public bool IsDueSoon => Status == VerificationStatus.DueSoon;

    public bool HasBadge => Status != VerificationStatus.None;

    public IRelayCommand SelectCommand { get; }
}

/// <summary>The meters of one resource in the meters card.</summary>
public sealed class MeterGroupViewModel(string header, IReadOnlyList<MeterRowViewModel> rows)
{
    public string Header { get; } = header;

    public IReadOnlyList<MeterRowViewModel> Rows { get; } = rows;
}

/// <summary>Display names shared by the meter list rows and the meter editor.</summary>
public static class MeterNames
{
    /// <summary>The location; without one "Meter" plus the serial number; without either just "Meter".</summary>
    public static string Title(ILocalizer localizer, Meter meter)
    {
        if (!string.IsNullOrWhiteSpace(meter.Location))
        {
            return meter.Location;
        }

        return string.IsNullOrWhiteSpace(meter.SerialNumber)
            ? localizer["MeterUnnamed"]
            : localizer.Get("MeterNumbered", meter.SerialNumber);
    }

    /// <summary>The serial number, or the localized "no serial number" when it is empty.</summary>
    public static string Serial(ILocalizer localizer, Meter meter) =>
        string.IsNullOrWhiteSpace(meter.SerialNumber) ? localizer["MeterNoSerial"] : meter.SerialNumber;

    /// <summary>"Verification overdue since …" / "Verification due …", or empty when there is nothing to warn about.</summary>
    public static string BadgeText(ILocalizer localizer, Meter meter, VerificationStatus status) =>
        meter.GetVerificationDue() is { } due
            ? status switch
            {
                VerificationStatus.Overdue => localizer.Get("MeterVerificationOverdue", due),
                VerificationStatus.DueSoon => localizer.Get("MeterVerificationDue", due),
                _ => string.Empty,
            }
            : string.Empty;
}
