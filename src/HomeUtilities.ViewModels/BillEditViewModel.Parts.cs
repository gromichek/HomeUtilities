using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using HomeUtilities.Core.Attachments;
using HomeUtilities.Core.Billing;
using HomeUtilities.Core.Charges;
using HomeUtilities.Core.Common;
using HomeUtilities.Core.Domain;
using HomeUtilities.Core.Equalized;
using HomeUtilities.Core.Tariffs;
using HomeUtilities.ViewModels.Common;

namespace HomeUtilities.ViewModels;

// Loading, the comparison card, lines, documents and the payment card of the bill editor.
public sealed partial class BillEditViewModel
{
    private readonly List<BillLine> _lines = [];
    private readonly List<Guid> _removedAttachments = [];
    private readonly List<PickedFile> _pendingFiles = [];
    private List<Attachment> _storedAttachments = [];
    private List<Payment> _paymentList = [];
    private decimal _prepaymentAvailable;
    private bool _calculationFailed;

    // ---- Loading --------------------------------------------------------------------------------------------------

    private async Task PrepareNewAsync()
    {
        _lines.Clear();
        _removedAttachments.Clear();
        _pendingFiles.Clear();
        _storedAttachments = [];
        _contract = null;
        _live = null;
        _snapshot = null;
        _bill.Currency = Currency.EUR;
        CurrencySymbol = _localizer["Currency_Symbol_" + _bill.Currency];
        IsDraft = false;
        _templateSource = null;
        _templateDatesEdited = false;
        CanUseTemplate = (await _templateStore.ListAsync()).Any(t => t.IsActive);

        var preset = PresetContractId is { } contractId ? await _contractStore.GetAsync(contractId) : null;
        Guid? propertyId = preset?.PropertyId ?? PresetPropertyId ?? (_properties.Count == 1 ? _properties[0].Id : null);

        IsOneOff = false;
        BillTitle = string.Empty;
        Number = string.Empty;
        HasIssuedOn = true;
        IssuedOn = _clock.Today;
        HasDueOn = false;
        HasServicePeriod = false;
        Notes = string.Empty;
        ConsumptionText = string.Empty;
        Amounts.Clear();
        Amounts.GrossPlaceholder = PresetGross is { } suggested ? AmountEditor.FormatMoney(suggested) : string.Empty;
        SelectedProperty = null;
        SelectedContract = null;
        SelectedCategory = null;
        SelectedSupplier = null;
        FieldRows = [];
        RebuildKinds();
        SelectedKind = Kinds[0];
        Period = PresetPeriod ?? YearMonth.From(_clock.Today).AddMonths(-1);
        _periodTouched = PresetPeriod is not null;

        if (propertyId is { } id)
        {
            await SelectPropertyCoreAsync(id);
        }

        if (preset is not null && preset.PropertyId == propertyId)
        {
            ApplyContract(preset);
            if (PresetKind is { } presetKind && Kinds.FirstOrDefault(k => k.Value.Equals(presetKind)) is { } kindOption)
            {
                SelectedKind = kindOption;
            }
        }

        RebuildAttachmentRows();
        RebuildLines();
    }

    private async Task ShowExistingAsync(Bill existing)
    {
        _lines.Clear();
        _lines.AddRange(existing.Lines.Where(l => !l.IsDeleted));
        _removedAttachments.Clear();
        _pendingFiles.Clear();
        _snapshot = existing.CalculatedSnapshot;
        _live = null;
        IsDraft = existing.IsDraft;
        _templateDatesEdited = true;
        _templateSource = existing.TemplateId is { } templateId ? await _templateStore.GetAsync(templateId) : null;
        OnPropertyChanged(nameof(DraftBannerText));
        IsOneOff = existing.Kind == BillKind.OneOff || existing.ContractId is null;
        _contract = existing.ContractId is { } contractId ? await _contractStore.GetAsync(contractId) : null;
        _contracts = existing.PropertyId == Guid.Empty ? [] : await _contractStore.ListByPropertyAsync(existing.PropertyId);
        CurrencySymbol = _localizer["Currency_Symbol_" + existing.Currency];

        SelectedProperty = _properties.Where(p => p.Id == existing.PropertyId).Select(PropertyOption).FirstOrDefault();
        SelectedContract = _contract is { } own ? new ChoiceOption(own.Id, own.Title, string.Empty) : null;
        RebuildKinds();
        SelectedKind = Kinds.FirstOrDefault(k => k.Value.Equals(existing.Kind)) ?? Kinds[0];
        Period = existing.Period;
        BillTitle = existing.Title ?? string.Empty;
        Number = existing.Number ?? string.Empty;
        HasIssuedOn = existing.IssuedOn is not null;
        IssuedOn = existing.IssuedOn ?? _clock.Today;
        HasDueOn = existing.DueOn is not null;
        DueOn = existing.DueOn ?? existing.IssuedOn ?? _clock.Today;
        HasServicePeriod = existing.ServiceFrom is not null && existing.ServiceTo is not null;
        ServiceFrom = existing.ServiceFrom ?? existing.Period.FirstDay;
        ServiceTo = existing.ServiceTo ?? existing.Period.LastDay;
        SelectedCategory = CategoryOptions().FirstOrDefault(c => c.Id == existing.CategoryId);
        SelectedSupplier = existing.SupplierId is { } supplierId ? SupplierOptions().FirstOrDefault(s => s.Id == supplierId) : null;
        Amounts.Load(new AmountSet(existing.Net, existing.VatPercent, existing.Vat, existing.Gross));
        Amounts.GrossPlaceholder = string.Empty;
        ConsumptionText = existing.ConsumptionOnBill is { } consumption ? DecimalInput.Format(consumption, CultureInfo.CurrentCulture, 4) : string.Empty;
        Notes = existing.Notes ?? string.Empty;
        FieldRows = BuildFieldRows(existing.CustomFields);
        await RefreshAttachmentRowsAsync();
        RebuildLines();
    }

    private async Task SelectPropertyCoreAsync(Guid propertyId)
    {
        SelectedProperty = _properties.Where(p => p.Id == propertyId).Select(PropertyOption).FirstOrDefault();
        _contracts =
        [
            .. (await _contractStore.ListByPropertyAsync(propertyId))
                .Where(c => !c.IsDeleted && !c.IsArchived)
                .OrderBy(c => c.Title, StringComparer.CurrentCultureIgnoreCase),
        ];
        _contract = null;
        SelectedContract = null;
        FieldRows = [];
    }

    /// <summary>Takes everything a new bill inherits from its contract: category, supplier, kind, currency, period, due date, custom fields.</summary>
    private void ApplyContract(Contract contract)
    {
        _applying = true;
        try
        {
            _contract = contract;
            SelectedContract = new ChoiceOption(contract.Id, contract.Title, string.Empty);
            _bill.Currency = contract.Currency;
            CurrencySymbol = _localizer["Currency_Symbol_" + contract.Currency];

            if (!_categoryTouched)
            {
                SelectedCategory = CategoryOptions().FirstOrDefault(c => c.Id == contract.CategoryId);
            }

            if (!_supplierTouched)
            {
                SelectedSupplier = SupplierOptions().FirstOrDefault(s => s.Id == contract.SupplierId);
            }

            var kind = Bill.CreateFor(contract, Period, _clock).Kind;
            SelectedKind = Kinds.FirstOrDefault(k => k.Value.Equals(kind)) ?? Kinds[0];
            if (!_periodTouched)
            {
                Period = contract.BillingTiming == BillingTiming.InAdvance
                    ? YearMonth.From(_clock.Today).AddMonths(1)
                    : ChargePeriodDefaults.For(_contracts, _clock.Today);
            }

            _snapshot = null;
            ServiceFrom = Period.FirstDay;
            ServiceTo = Period.LastDay;
            if (!_dueTouched)
            {
                var due = BillDefaults.DueDate(contract, Period);
                HasDueOn = due is not null;
                DueOn = due ?? DueOn;
            }

            FieldRows = BuildFieldRows([]);
        }
        finally
        {
            _applying = false;
        }
    }

    private IReadOnlyList<CustomFieldValueRowViewModel> BuildFieldRows(IEnumerable<CustomFieldValue> stored)
    {
        var rows = CustomFieldValueRowViewModel.Build(
            FieldDefinitions().Where(d => d.Target == CustomFieldTarget.Bill),
            stored,
            _clock.Today,
            _localizer["BillFieldDateSet"]);
        foreach (var row in rows)
        {
            row.Edited += MarkDirty;
        }

        return rows;
    }

    /// <summary>The custom fields the bill carries: those of its template, else those of its contract.</summary>
    private IEnumerable<CustomFieldDefinition> FieldDefinitions() =>
        _templateSource is { } template && template.CustomFields.Any(d => !d.IsDeleted)
            ? template.CustomFields
            : _contract?.CustomFields ?? [];

    private ChoiceOption PropertyOption(Property property) => new(property.Id, property.Name, string.Empty);

    private IReadOnlyList<ChoiceOption> PropertyOptions() => [.. _properties.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).Select(PropertyOption)];

    /// <summary>Active categories by sort order, plus the selected one if it is archived.</summary>
    private IReadOnlyList<ChoiceOption> CategoryOptions() =>
    [
        .. _categories
            .Where(c => !c.IsArchived || c.Id == SelectedCategory?.Id || c.Id == _bill.CategoryId)
            .OrderBy(c => c.SortOrder)
            .Select(c => new ChoiceOption(c.Id, CategoryNames.DisplayName(c, _localizer), c.Color)),
    ];

    private IReadOnlyList<ChoiceOption> SupplierOptions() =>
    [
        .. _suppliers
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new ChoiceOption(s.Id, s.Name, string.Empty)),
    ];

    // ---- Choosers -------------------------------------------------------------------------------------------------

    [RelayCommand]
    private Task ChoosePropertyAsync() => RunSafeAsync(async () =>
    {
        if (!IsNew)
        {
            return;
        }

        var options = PropertyOptions();
        var picked = await Dialogs.ChooseAsync(_localizer["BillChooseProperty"], [.. options.Select(o => o.Name)]);
        if (picked is null || options.FirstOrDefault(o => o.Name == picked) is not { } option || option.Id == SelectedProperty?.Id)
        {
            return;
        }

        await SelectPropertyCoreAsync(option.Id);
        PendingRefresh = RefreshCalculationAsync();
        await PendingRefresh;
    });

    [RelayCommand]
    private Task ChooseContractAsync() => RunSafeAsync(async () =>
    {
        if (!IsNew || IsOneOff)
        {
            return;
        }

        if (_contracts.Count == 0)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["BillNoContractsForProperty"]);
            return;
        }

        var picked = await Dialogs.ChooseAsync(_localizer["BillChooseContract"], [.. _contracts.Select(c => c.Title)]);
        if (picked is null || _contracts.FirstOrDefault(c => c.Title == picked) is not { } contract)
        {
            return;
        }

        ApplyContract(contract);
        PendingRefresh = RefreshCalculationAsync();
        await PendingRefresh;
    });

    [RelayCommand]
    private Task ChooseCategoryAsync() => RunSafeAsync(async () =>
    {
        var options = CategoryOptions();
        var picked = await Dialogs.ChooseAsync(_localizer["BillChooseCategory"], [.. options.Select(o => o.Name)]);
        if (picked is not null && options.FirstOrDefault(o => o.Name == picked) is { } option)
        {
            SelectedCategory = option;
        }
    });

    [RelayCommand]
    private Task ChooseSupplierAsync() => RunSafeAsync(async () =>
    {
        var none = _localizer["BillNoSupplier"];
        var options = SupplierOptions();
        var picked = await Dialogs.ChooseAsync(_localizer["BillChooseSupplier"], [none, .. options.Select(o => o.Name)]);
        if (picked is null)
        {
            return;
        }

        SelectedSupplier = picked == none ? null : options.FirstOrDefault(o => o.Name == picked);
    });

    [RelayCommand]
    private void PreviousPeriod() => Period = Period.AddMonths(-1);

    [RelayCommand]
    private void NextPeriod() => Period = Period.AddMonths(1);

    // ---- Comparison card ------------------------------------------------------------------------------------------

    /// <summary>Calculates the contract's charge for the period (the "live" calculation) and the VAT % a new bill starts with.</summary>
    private async Task RefreshCalculationAsync()
    {
        var version = ++_calculationVersion;
        var period = Period;
        ChargeInput? input = null;
        CalculatedCharge? live = null;
        IReadOnlyList<EqualizedPlan> plans = [];
        IReadOnlyList<BillQuantityRowViewModel> rows = [];
        var failed = false;
        if (!IsOneOff && _contract is { } contract)
        {
            try
            {
                input = await _chargeLoader.LoadAsync(contract.Id, period);
                if (contract.BillingMode == BillingMode.Equalized)
                {
                    plans = await _planStore.ListByContractAsync(contract.Id);
                }

                if (input is not null && input.Components.Any(c => !c.IsDeleted))
                {
                    rows = BuildQuantityRows(input);
                    live = ChargeCalculator.Calculate(WithTyped(input, rows));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _errorLog.Append($"{nameof(BillEditViewModel)}.{nameof(RefreshCalculationAsync)} (contract {contract.Id}, {period})", ex);
                failed = true;
            }
        }

        if (version != _calculationVersion)
        {
            return;
        }

        _input = input;
        _live = live;
        _plans = plans;
        QuantityRows = rows;
        ApplyAmountWarnings(live);
        _calculationFailed = failed;
        HasTariff = input is not null && input.Components.Any(c => !c.IsDeleted);
        if (IsNew && string.IsNullOrWhiteSpace(Amounts.NetText) && string.IsNullOrWhiteSpace(Amounts.GrossText))
        {
            Amounts.SetDefaultVatPercent(DefaultVatPercent(input));
        }

        UpdatePlanAmount();
        if (IsNew)
        {
            ApplyPlanDefault();
        }

        UpdateComparison();
    }

    /// <summary>The plan amount of the bill's period, when the bill is an equalized one of a contract with a plan.</summary>
    private void UpdatePlanAmount()
    {
        _planAmount = !IsOneOff && SelectedKind?.Value is BillKind.Equalized ? EqualizedPlans.AmountFor(_plans, Period) : null;
        OnPropertyChanged(nameof(PlanMode));
        OnPropertyChanged(nameof(ShowComparison));
        OnPropertyChanged(nameof(BasisLabel));
    }

    /// <summary>
    /// A new equalized bill starts with the plan amount of its period as the Total. A Total the user typed is kept; one that
    /// is still the earlier default follows the period.
    /// </summary>
    private void ApplyPlanDefault()
    {
        var current = Amounts.GrossText;
        var isDefault = string.IsNullOrWhiteSpace(current) || current == _planDefaultText;
        if (_planAmount is not { } amount)
        {
            if (current == _planDefaultText && _planDefaultText.Length > 0)
            {
                Amounts.SetDefaultGross(null);
            }

            _planDefaultText = string.Empty;
            return;
        }

        if (isDefault)
        {
            Amounts.SetDefaultGross(amount);
            _planDefaultText = Amounts.GrossText;
        }
    }

    /// <summary>One row per quantity-from-bill component; the text is kept from the rows shown before, else from the stored bill.</summary>
    private IReadOnlyList<BillQuantityRowViewModel> BuildQuantityRows(ChargeInput input)
    {
        var rows = new List<BillQuantityRowViewModel>();
        foreach (var component in input.Components.Where(c => c is { IsDeleted: false, Basis: ChargeBasis.QuantityFromBill }).OrderBy(c => c.SortOrder))
        {
            var unit = component.QuantityUnit is { } u ? EnumText.Get(_localizer, u) : string.Empty;
            var row = new BillQuantityRowViewModel(component.Id, component.Name, unit, _localizer["BillQuantityAmount"], _localizer["BillQuantityAmountHint"]);
            var kept = QuantityRows.FirstOrDefault(r => r.ComponentId == component.Id);
            if (kept is not null)
            {
                row.Text = kept.Text;
                row.AmountText = kept.AmountText;
            }
            else if (!IsNew && _bill.BillQuantities.FirstOrDefault(q => q.ComponentId == component.Id) is { } stored)
            {
                row.Text = DecimalInput.Format(stored.Quantity, CultureInfo.CurrentCulture, 4);
                row.AmountText = stored.Amount is { } amount ? DecimalInput.Format(amount, CultureInfo.CurrentCulture, 2) : string.Empty;
            }

            row.Edited += OnQuantityEdited;
            rows.Add(row);
        }

        return rows;
    }

    /// <summary>The input with the quantities typed so far; unreadable or empty rows count as not entered.</summary>
    private static ChargeInput WithTyped(ChargeInput input, IReadOnlyList<BillQuantityRowViewModel> rows)
    {
        var typed = new Dictionary<Guid, decimal>();
        var amounts = new Dictionary<Guid, decimal>();
        foreach (var row in rows)
        {
            if (DecimalInput.TryParse(row.Text, CultureInfo.CurrentCulture, out var value))
            {
                typed[row.ComponentId] = value;

                // An amount without a quantity is ignored by the calculation (and rejected on save).
                if (DecimalInput.TryParse(row.AmountText, CultureInfo.CurrentCulture, out var amount))
                {
                    amounts[row.ComponentId] = amount;
                }
            }
        }

        return input with { QuantitiesFromBill = typed, AmountsFromBill = amounts };
    }

    /// <summary>Shows the "amount does not match quantity × price" warning of the calculation under the row it belongs to.</summary>
    private void ApplyAmountWarnings(CalculatedCharge? calculation)
    {
        foreach (var row in QuantityRows)
        {
            row.WarningText = calculation is not null
                && calculation.Warnings.Any(w => w.Code == ChargeWarningCodes.AmountMismatch && w.ComponentId == row.ComponentId)
                    ? _localizer["BillAmountMismatch"]
                    : string.Empty;
        }
    }

    private static string QuantityField(int index) => $"{nameof(Bill.BillQuantities)}[{index}]";

    private static string QuantityAmountField(int index) => $"{nameof(Bill.BillQuantities)}[{index}].Amount";

    /// <summary>A quantity changed: the calculation (and the snapshot, if the bill has one) follows live.</summary>
    private void OnQuantityEdited()
    {
        if (_input is null || _loading)
        {
            return;
        }

        _live = ChargeCalculator.Calculate(WithTyped(_input, QuantityRows));
        ApplyAmountWarnings(_live);
        if (_snapshot is not null)
        {
            _snapshot = _live;
        }

        MarkDirty();
        UpdateComparison();
    }

    private static decimal? DefaultVatPercent(ChargeInput? input)
    {
        if (input?.Components.Where(c => !c.IsDeleted).OrderBy(c => c.SortOrder).FirstOrDefault() is not { } first)
        {
            return null;
        }

        try
        {
            return TariffPricing.ResolveVatPercent(first, input.VatCatalog, input.Period.FirstDay);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Recalculates the difference to the calculation; the stored snapshot counts, the live calculation only for a bill without one.</summary>
    private void UpdateComparison()
    {
        var calculation = _snapshot ?? _live;
        var gross = ReadGrossQuietly();
        var consumption = AmountEditor.ReadOptional(ConsumptionText, nameof(Bill.ConsumptionOnBill), []);
        var metered = BillComparison.MeteredQuantity(calculation);
        var result = BillComparison.Compare(new Bill { Gross = gross ?? 0m, ConsumptionOnBill = consumption }, calculation, metered);
        var hasLines = calculation is { Lines.Count: > 0 };

        decimal? difference;
        decimal? percent;
        if (_planAmount is { } plan)
        {
            // An equalized bill is compared with the plan; the calculation by readings is shown as information.
            CalculatedText = Money(plan);
            difference = gross is null ? null : gross.Value - plan;
            percent = difference is { } d0 && plan != 0 ? Math.Round(d0 / plan * 100, 1, MidpointRounding.AwayFromZero) : null;
            ActualInfoText = calculation is { IsComplete: true, Lines.Count: > 0 }
                ? _localizer.Get("BillActualByReadings", Money(calculation.TotalGross))
                : string.Empty;
        }
        else
        {
            CalculatedText = hasLines ? Money(calculation!.TotalGross) : string.Empty;
            difference = gross is null ? null : result.Difference;
            percent = gross is null ? null : result.DifferencePercent;
            ActualInfoText = string.Empty;
        }

        OnPropertyChanged(nameof(BasisLabel));
        DifferenceText = difference is { } d ? BillText.Difference(d, percent, CurrencySymbol) : string.Empty;
        var tone = BillComparison.ToneOf(percent);
        DifferenceHigher = tone == DifferenceTone.Higher;
        DifferenceLower = tone == DifferenceTone.Lower;
        MeteredText = metered is { } m ? QuantityText(m) : string.Empty;
        QuantityDifferenceText = result.QuantityDifference is { } q ? QuantityText(q) : string.Empty;

        ComparisonNote = _calculationFailed
            ? _localizer["BillCalculationFailed"]
            : calculation is { IsComplete: false }
                ? _localizer["BillCalculationIncomplete"]
                : string.Empty;
        SnapshotChangedText = _snapshot is { } snapshot && _live is { IsComplete: true } live && live.TotalGross != snapshot.TotalGross
            ? _localizer.Get("BillSnapshotChanged", Money(snapshot.TotalGross), Money(live.TotalGross))
            : string.Empty;
        UpdateLinesWarning();
    }

    private decimal? ReadGrossQuietly() =>
        AmountEditor.ReadOptional(Amounts.GrossText, "Gross", []) ?? null;

    private string QuantityText(decimal quantity)
    {
        var ids = _input?.ContractMeters.Select(l => l.MeterId).ToHashSet();
        var meter = ids is null ? null : _input!.PropertyMeters.FirstOrDefault(m => ids.Contains(m.Id));
        var unit = meter is null ? string.Empty : " " + EnumText.Get(_localizer, meter.Unit);
        return quantity.ToString("0.000", CultureInfo.CurrentCulture) + unit;
    }

    /// <summary>Takes the current calculation as the bill's snapshot (saved with the bill).</summary>
    [RelayCommand]
    private void UpdateSnapshot()
    {
        if (_live is null)
        {
            return;
        }

        _snapshot = _live;
        MarkDirty();
        UpdateComparison();
    }

    /// <summary>Opens the charges screen on the bill's property and month with the contract expanded.</summary>
    [RelayCommand]
    private Task ShowChargesAsync() => RunSafeAsync(() =>
    {
        if (_contract is not { } contract)
        {
            return Task.CompletedTask;
        }

        return _navigation.GoToAsync(
            Routes.ChargesFor,
            new Dictionary<string, object>
            {
                [EditorNavigation.PropertyIdParameter] = contract.PropertyId,
                [EditorNavigation.ContractIdParameter] = contract.Id,
                [ChargesViewModel.MonthParameter] = Period.ToString(),
            });
    });

    // ---- Lines ----------------------------------------------------------------------------------------------------

    [RelayCommand]
    private void ToggleLines() => LinesExpanded = !LinesExpanded;

    [RelayCommand]
    private Task AddLineAsync() => RunSafeAsync(() => EditLineAsync(null));

    private async Task EditLineAsync(BillLine? line)
    {
        var definitions = FieldDefinitions().Where(d => d.Target == CustomFieldTarget.BillLine);
        var dialog = new BillLineEditorViewModel(_localizer, line, _categories, definitions, _clock.Today);
        if (!await Dialogs.ShowEditorAsync(dialog) || dialog.Result is not { } result)
        {
            return;
        }

        var index = line is null ? -1 : _lines.FindIndex(l => l.Id == line.Id);
        if (index >= 0)
        {
            _lines[index] = result;
        }
        else
        {
            _lines.Add(result);
        }

        LinesExpanded = true;
        RebuildLines();
        MarkDirty();
    }

    private Task RemoveLineAsync(BillLine line) => RunSafeAsync(async () =>
    {
        var confirmed = await Dialogs.ConfirmAsync(
            _localizer["Dialog_DeleteRow_Title"],
            _localizer.Get("Dialog_DeleteRow_Message", line.Description),
            _localizer["Delete"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        _lines.RemoveAll(l => l.Id == line.Id);
        RebuildLines();
        MarkDirty();
    });

    private void RebuildLines()
    {
        LineRows =
        [
            .. _lines.Select(line => new BillLineRowViewModel(
                line,
                Money(line.Gross),
                LineDetail(line),
                Texts.DeleteLineText,
                l => _ = RunSafeAsync(() => EditLineAsync(l)),
                l => _ = RemoveLineAsync(l))),
        ];
        UpdateLinesWarning();
    }

    private string LineDetail(BillLine line)
    {
        if (line.Quantity is not { } quantity || line.UnitPrice is not { } price)
        {
            return string.Empty;
        }

        var quantityText = DecimalInput.Format(quantity, CultureInfo.CurrentCulture, 4) + (string.IsNullOrWhiteSpace(line.Unit) ? string.Empty : " " + line.Unit);
        return _localizer.Get("BillLineSummary", quantityText, DecimalInput.Format(price, CultureInfo.CurrentCulture, 6));
    }

    /// <summary>A warning, not an error: suppliers round differently.</summary>
    private void UpdateLinesWarning()
    {
        if (ReadGrossQuietly() is not { } gross)
        {
            LinesWarning = string.Empty;
            return;
        }

        var warning = BillChecks.Warnings(new Bill { Gross = gross, Lines = [.. _lines] })
            .FirstOrDefault(w => w.Code == BillWarningCodes.LinesTotalMismatch);
        LinesWarning = warning is null
            ? string.Empty
            : _localizer.Get("BillLinesMismatch", Money(_lines.Sum(l => l.Gross)), Money(gross));
    }

    // ---- Documents ------------------------------------------------------------------------------------------------

    private async Task RefreshAttachmentRowsAsync()
    {
        var stored = new List<Attachment>();
        foreach (var id in _bill.AttachmentIds.Where(id => !_removedAttachments.Contains(id)))
        {
            if (await _attachmentStore.GetAsync(id) is { } attachment)
            {
                stored.Add(attachment);
            }
        }

        _storedAttachments = stored;
        RebuildAttachmentRows();
    }

    private void RebuildAttachmentRows()
    {
        AttachmentRows =
        [
            .. _storedAttachments.Select(a => new BillAttachmentRowViewModel(
                a.FileName, false, Texts.OpenAttachmentText, Texts.RemoveAttachmentText,
                () => _ = RunSafeAsync(() => OpenAttachmentAsync(a)),
                () => RemoveStoredAttachment(a))),
            .. _pendingFiles.Select(f => new BillAttachmentRowViewModel(
                _localizer.Get("BillAttachmentPending", f.FileName), true, Texts.OpenAttachmentText, Texts.RemoveAttachmentText,
                null,
                () => RemovePendingFile(f))),
        ];
    }

    private void RemoveStoredAttachment(Attachment attachment)
    {
        _removedAttachments.Add(attachment.Id);
        _storedAttachments.RemoveAll(a => a.Id == attachment.Id);
        RebuildAttachmentRows();
        MarkDirty();
    }

    private void RemovePendingFile(PickedFile file)
    {
        _pendingFiles.Remove(file);
        RebuildAttachmentRows();
        MarkDirty();
    }

    private async Task OpenAttachmentAsync(Attachment attachment)
    {
        var path = await _attachmentStore.GetFilePathAsync(attachment.Id);
        if (path is null)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["BillAttachmentMissing"]);
            return;
        }

        await _viewer.OpenAsync(path, attachment.ContentType);
    }

    [RelayCommand]
    private Task AddAttachmentAsync() => RunSafeAsync(async () =>
    {
        AttachmentError = string.Empty;
        var take = _localizer["PhotoTake"];
        var image = _localizer["BillAttachmentImage"];
        var pdf = _localizer["BillAttachmentPdf"];
        var options = _media.IsCaptureSupported ? new List<string> { take, image, pdf } : [image, pdf];
        var choice = await Dialogs.ChooseAsync(_localizer["BillAttachmentChoose"], options);
        if (choice is null)
        {
            return;
        }

        var (file, error) = await PhotoPicking.TryPickAsync(
            () => choice == take
                ? _media.CapturePhotoAsync()
                : choice == pdf ? _media.PickFileAsync([AttachmentContentTypes.Pdf]) : _media.PickPhotoAsync(),
            _localizer);
        if (error.Length > 0)
        {
            AttachmentError = error;
            return;
        }

        if (file is null)
        {
            return;
        }

        _pendingFiles.Add(file);
        RebuildAttachmentRows();
        MarkDirty();
    });

    /// <summary>Stores the chosen files and returns the ids of all documents of the bill; a rejected file is reported and skipped.</summary>
    private async Task<List<Guid>> StoreAttachmentsAsync(Bill bill)
    {
        var ids = bill.AttachmentIds.ToList();
        foreach (var file in _pendingFiles.ToList())
        {
            try
            {
                await using var content = await file.OpenAsync();
                var attachment = await _attachmentStore.AddAsync(PhotoPicking.ToAttachment(file, AttachmentOwnerKind.Bill, bill.Id), content);
                ids.Add(attachment.Id);
            }
            catch (AttachmentRejectedException ex)
            {
                await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], PhotoPicking.Describe(ex.Result.Errors, _localizer));
            }
        }

        return ids;
    }

    // ---- Payment card ---------------------------------------------------------------------------------------------

    private async Task RefreshPaymentsAsync()
    {
        _paymentList = IsNew ? [] : [.. await _paymentStore.ListByBillAsync(_bill.Id)];
        _prepaymentAvailable = !IsNew && _bill.ContractId is { } contractId ? await _prepayments.AvailableAsync(contractId) : 0m;
        UpdatePaymentCard();
    }

    /// <summary>Re-reads the payments (back from the payment editor, which may have changed them).</summary>
    public Task RefreshPaymentCardAsync() => RunSafeAsync(RefreshPaymentsAsync);

    /// <summary>Back from the payment editor: the status, the amounts and the payment rows follow the payments.</summary>
    public override async Task OnAppearingAsync()
    {
        await base.OnAppearingAsync();
        if (!IsNew)
        {
            await RefreshPaymentCardAsync();
        }
    }

    private void UpdatePaymentCard()
    {
        if (IsNew)
        {
            PaymentRows = [];
            CanMarkPaid = false;
            CanUsePrepayment = false;
            UsePrepaymentText = string.Empty;
            StatusText = string.Empty;
            PaidAmountText = string.Empty;
            OpenAmountText = string.Empty;
            return;
        }

        var state = BillStatus.Of(_bill, _paymentList, _clock.Today);
        StatusKind = BillBadge.KindOf(state);
        StatusText = BillBadge.TextOf(_localizer, state, CurrencySymbol);
        PaidAmountText = Money(state.Paid);
        OpenAmountText = Money(state.Open);
        CanMarkPaid = state.Open != 0 && Math.Sign(state.Open) == Math.Sign(_bill.Gross);
        CanUsePrepayment = state.Open > 0 && _prepaymentAvailable > 0;
        UsePrepaymentText = _localizer.Get("BillUsePrepayment", Money(_prepaymentAvailable));
        PaymentRows =
        [
            .. _paymentList.Select(p =>
            {
                var share = p.Allocations.Where(a => !a.IsDeleted && a.BillId == _bill.Id).Sum(a => a.Amount);
                var text = _localizer.Get(
                    "BillPaymentRow",
                    BillText.Date(p.PaidOn),
                    Money(share),
                    EnumText.Get(_localizer, p.Method) + (p.CreatedByQuickAction ? " · " + _localizer["BillPaymentQuick"] : string.Empty));
                var detail = share != p.Amount ? _localizer.Get("BillPaymentOfBill", Money(share), Money(p.Amount)) : string.Empty;
                return new BillPaymentRowViewModel(p.Id, text, detail, Texts.RemovePaymentText, () => _ = OpenPaymentAsync(p.Id), () => _ = RemovePaymentAsync(p));
            }),
        ];
    }

    /// <summary>The "Paid" quick action: a payment for the open amount dated today, then "Bill marked as paid · Undo".</summary>
    [RelayCommand]
    private Task MarkPaidAsync() => RunSafeAsync(async () =>
    {
        if (IsNew)
        {
            return;
        }

        if (IsDirty)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["BillSaveFirst"]);
            return;
        }

        var payment = await _quickPay.MarkPaidAsync(_bill.Id);
        await PaymentsChangedAsync();
        if (payment is null)
        {
            return;
        }

        if (await _snackbar.ShowAsync(_localizer["BillMarkedPaid"], _localizer["BillUndo"]))
        {
            await _quickPay.UndoAsync(payment.Id);
            await PaymentsChangedAsync();
        }
    });

    /// <summary>"Add payment": the payment editor with this bill's contract, today, the open amount and the bill allocated.</summary>
    [RelayCommand]
    private Task AddPaymentAsync() => RunSafeAsync(async () =>
    {
        if (IsNew)
        {
            return;
        }

        if (IsDirty)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["BillSaveFirst"]);
            return;
        }

        await _navigation.GoToAsync(
            Routes.PaymentEdit,
            new Dictionary<string, object> { [EditorNavigation.BillIdParameter] = _bill.Id });
    });

    private Task OpenPaymentAsync(Guid paymentId) => RunSafeAsync(() => _navigation.GoToAsync(
        Routes.PaymentEdit,
        new Dictionary<string, object> { [EditorNavigation.IdParameter] = paymentId }));

    /// <summary>"Use prepayment": allocates the contract's unallocated money to this bill, then "Prepayment used · Undo".</summary>
    [RelayCommand]
    private Task UsePrepaymentAsync() => RunSafeAsync(async () =>
    {
        if (IsNew)
        {
            return;
        }

        if (IsDirty)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["BillSaveFirst"]);
            return;
        }

        var result = await _prepayments.UseForBillAsync(_bill.Id);
        await PaymentsChangedAsync();
        if (result is null)
        {
            return;
        }

        if (await _snackbar.ShowAsync(_localizer["PrepaymentUsed"], _localizer["BillUndo"]))
        {
            await _prepayments.UndoAsync(result);
            await PaymentsChangedAsync();
        }
    });

    private Task RemovePaymentAsync(Payment payment) => RunSafeAsync(async () =>
    {
        var outcome = QuickPayService.OutcomeFor(payment, _bill.Id);
        var confirmed = await Dialogs.ConfirmAsync(
            _localizer.Get("BillRemovePaymentTitle", Money(payment.Amount), BillText.Date(payment.PaidOn)),
            _localizer[outcome == RemovePaymentOutcome.AllocationRemoved ? "BillRemovePaymentRest" : "BillRemovePaymentHidden"],
            _localizer["BillRemovePayment"],
            _localizer["Cancel"]);
        if (!confirmed)
        {
            return;
        }

        await _quickPay.RemovePaymentAsync(_bill.Id, payment.Id);
        await PaymentsChangedAsync();
        _snackbar.Notify(_localizer["BillPaymentRemoved"]);
    });

    /// <summary>Re-reads the payments and tells the list screen, which shows the status too.</summary>
    private async Task PaymentsChangedAsync()
    {
        await RefreshPaymentsAsync();
        if (Finished is { } finished)
        {
            await finished(EditOutcome.Saved);
        }
    }

    // ---- Drafts and templates ---------------------------------------------------------------------------------------

    /// <summary>Confirm button of a draft: saves the form as a normal bill (an amount is required).</summary>
    [RelayCommand]
    private Task ConfirmDraftAsync() => RunSafeAsync(async () =>
    {
        if (!IsDraft || IsNew)
        {
            return;
        }

        _confirming = true;
        try
        {
            await SaveCommand.ExecuteAsync(null);
        }
        finally
        {
            _confirming = false;
        }

        if (!IsDraft)
        {
            _snackbar.Notify(_localizer["BillConfirmed"]);
        }
    });

    /// <summary>"Save as template": opens the template editor with this bill's content. The bill has to be saved first.</summary>
    [RelayCommand]
    private Task SaveAsTemplateAsync() => RunSafeAsync(async () =>
    {
        if (IsNew || IsDirty)
        {
            await Dialogs.AlertAsync(_localizer["Dialog_Error_Title"], _localizer["BillSaveBeforeTemplate"]);
            return;
        }

        await _templateLauncher.FromBillAsync(_bill.Id);
    });

    /// <summary>"From template…": picks one of the active templates and fills the new bill from it.</summary>
    [RelayCommand]
    private Task FromTemplateAsync() => RunSafeAsync(async () =>
    {
        if (!IsNew)
        {
            return;
        }

        var templates = (await _templateStore.ListAsync()).Where(t => t.IsActive).OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        if (templates.Count == 0)
        {
            CanUseTemplate = false;
            return;
        }

        var names = _properties.ToDictionary(p => p.Id, p => p.Name);
        string Label(BillTemplate t) => _properties.Count > 1 && names.TryGetValue(t.PropertyId, out var property)
            ? _localizer.Get("BillTemplateOption", t.Name, property)
            : t.Name;
        var labels = templates.Select(Label).ToList();
        var picked = await Dialogs.ChooseAsync(_localizer["BillChooseTemplate"], labels);
        if (picked is null || labels.IndexOf(picked) is var index && index < 0)
        {
            return;
        }

        await ApplyTemplateAsync(templates[index]);
    });

    /// <summary>Fills the new bill from a template: property, contract, category, supplier, title, amounts, lines, fields, dates.</summary>
    private async Task ApplyTemplateAsync(BillTemplate template)
    {
        _templateSource = template;
        _templateDatesEdited = false;
        _bill.TemplateId = template.Id;
        _categoryTouched = true;
        _supplierTouched = true;
        _dueTouched = true;

        IsOneOff = template.ContractId is null;
        if (_properties.All(p => p.Id != template.PropertyId) && await _propertyStore.GetAsync(template.PropertyId) is { } own)
        {
            _properties = [.. _properties, own];
        }

        await SelectPropertyCoreAsync(template.PropertyId);
        if (template.ContractId is { } contractId
            && (_contracts.FirstOrDefault(c => c.Id == contractId) ?? await _contractStore.GetAsync(contractId)) is { } contract)
        {
            ApplyContract(contract);
        }

        // Prepaid templates default to next month, the others to the month that just ended; a one-time purchase is of this month.
        _applying = true;
        try
        {
            var offset = template.ContractId is null && template.Schedule.Kind == ScheduleKind.Once
                ? 0
                : template.Timing == BillingTiming.InAdvance ? 1 : -1;
            Period = YearMonth.From(_clock.Today).AddMonths(offset);
            _periodTouched = true;
            SelectedCategory = CategoryOptions().FirstOrDefault(c => c.Id == template.CategoryId);
            SelectedSupplier = template.SupplierId is { } supplierId ? SupplierOptions().FirstOrDefault(s => s.Id == supplierId) : null;
            BillTitle = string.IsNullOrWhiteSpace(template.Title) ? (IsOneOff ? template.Name : string.Empty) : template.Title;
            Notes = template.Notes ?? string.Empty;
        }
        finally
        {
            _applying = false;
        }

        if (template.Net is null && template.VatPercent is null && template.Gross is null)
        {
            Amounts.Clear();
        }
        else if (Core.Billing.Amounts.Complete(template.Net, template.VatPercent, template.Gross).Result is { } amounts)
        {
            Amounts.Load(amounts);
        }

        _lines.Clear();
        _lines.AddRange(template.Lines.Select((l, i) => l.ToBillLine(_bill.Id, i)));
        RebuildLines();
        FieldRows = BuildFieldRows([]);
        ApplyTemplateDates();
        OnPropertyChanged(nameof(DraftBannerText));
        MarkDirty();
        PendingRefresh = RefreshCalculationAsync();
        await PendingRefresh;
    }

    /// <summary>Issue and due date of the template for the bill's period (none when the template has no issue day).</summary>
    private void ApplyTemplateDates()
    {
        if (_templateSource is not { } template)
        {
            return;
        }

        var issued = BillTemplateScheduler.IssueDate(template, Period);
        var due = BillTemplateScheduler.DueDate(template, Period);
        var wasApplying = _applying;
        _applying = true;
        try
        {
            HasIssuedOn = issued is not null;
            if (issued is { } issuedOn)
            {
                IssuedOn = issuedOn;
            }

            HasDueOn = due is not null;
            if (due is { } dueOn)
            {
                DueOn = dueOn;
            }
        }
        finally
        {
            _applying = wasApplying;
        }
    }
}
