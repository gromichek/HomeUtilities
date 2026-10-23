namespace HomeUtilities.ViewModels;

/// <summary>The localized static texts of the replace-meter wizard.</summary>
public sealed class ReplaceMeterWizardTexts
{
    public ReplaceMeterWizardTexts(ILocalizer localizer)
    {
        DateStepTitle = localizer["WizardDateStep"];
        FinalStepTitle = localizer["WizardFinalStep"];
        NewMeterStepTitle = localizer["WizardNewMeterStep"];
        ContractsStepTitle = localizer["WizardContractsStep"];
        SummaryStepTitle = localizer["WizardSummaryStep"];
        DateLabel = localizer["WizardDateLabel"];
        FinalReadingLabel = localizer["WizardFinalReadingLabel"];
        InitialReadingLabel = localizer["WizardInitialReadingLabel"];
        SerialNumberLabel = localizer["MeterSerialNumberLabel"];
        DigitsLabel = localizer["MeterDigitsLabel"];
        DigitsHint = localizer["MeterDigitsHint"];
        HasVerifiedOnLabel = localizer["MeterHasVerifiedOnLabel"];
        VerifiedOnLabel = localizer["MeterVerifiedOnLabel"];
        IntervalLabel = localizer["MeterIntervalLabel"];
        IntervalHint = localizer["MeterIntervalHint"];
        ContractsHint = localizer["WizardContractsHint"];
        ContractsEmpty = localizer["MeterContractsEmpty"];
        NextText = localizer["WizardNext"];
        BackText = localizer["WizardBack"];
        ReplaceText = localizer["WizardReplace"];
    }

    public string DateStepTitle { get; }

    public string FinalStepTitle { get; }

    public string NewMeterStepTitle { get; }

    public string ContractsStepTitle { get; }

    public string SummaryStepTitle { get; }

    public string DateLabel { get; }

    public string FinalReadingLabel { get; }

    public string InitialReadingLabel { get; }

    public string SerialNumberLabel { get; }

    public string DigitsLabel { get; }

    public string DigitsHint { get; }

    public string HasVerifiedOnLabel { get; }

    public string VerifiedOnLabel { get; }

    public string IntervalLabel { get; }

    public string IntervalHint { get; }

    public string ContractsHint { get; }

    public string ContractsEmpty { get; }

    public string NextText { get; }

    public string BackText { get; }

    public string ReplaceText { get; }
}
