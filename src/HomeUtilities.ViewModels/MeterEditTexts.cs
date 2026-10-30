namespace HomeUtilities.ViewModels;

/// <summary>The localized labels of the meter editor page; the editor builds a new instance whenever the language changes.</summary>
public sealed class MeterEditTexts
{
    public MeterEditTexts(ILocalizer localizer)
    {
        GeneralHeader = localizer["MeterGeneral"];
        ResourceLabel = localizer["MeterResourceLabel"];
        UnitLabel = localizer["MeterUnitLabel"];
        SerialNumberLabel = localizer["MeterSerialNumberLabel"];
        LocationLabel = localizer["MeterLocationLabel"];
        DigitsLabel = localizer["MeterDigitsLabel"];
        DigitsHint = localizer["MeterDigitsHint"];
        NotesLabel = localizer["MeterNotesLabel"];
        RegistersHeader = localizer["MeterRegistersHeader"];
        AddRegisterText = localizer["MeterRegisterAdd"];
        DualTariffText = localizer["MeterDualTariff"];
        InstallationHeader = localizer["MeterInstallationHeader"];
        InstalledOnLabel = localizer["MeterInstalledOnLabel"];
        HasVerifiedOnLabel = localizer["MeterHasVerifiedOnLabel"];
        VerifiedOnLabel = localizer["MeterVerifiedOnLabel"];
        IntervalLabel = localizer["MeterIntervalLabel"];
        IntervalHint = localizer["MeterIntervalHint"];
        HasDueOverrideLabel = localizer["MeterHasDueOverrideLabel"];
        DueOverrideLabel = localizer["MeterDueOverrideLabel"];
        ReplaceText = localizer["MeterReplace"];
        ContractsHeader = localizer["MeterContractsHeader"];
        ContractsHint = localizer["MeterContractsHint"];
        ContractsEmpty = localizer["MeterContractsEmpty"];
        LinkHint = localizer["MeterLinkHint"];
        HistoryHeader = localizer["MeterHistoryHeader"];
        ReadingsHeader = localizer["MeterReadingsHeader"];
        AllReadingsText = localizer["MeterReadingsAll"];
        ReadingsEmpty = localizer["MeterReadingsEmpty"];
        DeleteBlockedText = localizer["MeterDeleteBlocked"];
        DeleteMeterText = localizer["MeterDelete"];
    }

    public string GeneralHeader { get; }

    public string ResourceLabel { get; }

    public string UnitLabel { get; }

    public string SerialNumberLabel { get; }

    public string LocationLabel { get; }

    public string DigitsLabel { get; }

    public string DigitsHint { get; }

    public string NotesLabel { get; }

    public string RegistersHeader { get; }

    public string AddRegisterText { get; }

    public string DualTariffText { get; }

    public string InstallationHeader { get; }

    public string InstalledOnLabel { get; }

    public string HasVerifiedOnLabel { get; }

    public string VerifiedOnLabel { get; }

    public string IntervalLabel { get; }

    public string IntervalHint { get; }

    public string HasDueOverrideLabel { get; }

    public string DueOverrideLabel { get; }

    public string ReplaceText { get; }

    public string ContractsHeader { get; }

    public string ContractsHint { get; }

    public string ContractsEmpty { get; }

    public string LinkHint { get; }

    public string HistoryHeader { get; }

    public string ReadingsHeader { get; }

    public string AllReadingsText { get; }

    public string ReadingsEmpty { get; }

    public string DeleteBlockedText { get; }

    public string DeleteMeterText { get; }
}
