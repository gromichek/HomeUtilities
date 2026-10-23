# T-015: Meters — list per property, meter editor, contract links, replacement wizard, verification
Phase: 3 · Depends on: T-014a · Size: L

## Goal
Let the owner register the meters of each property (also 2 cold + 2 hot water meters, dual-tariff and solar
electricity meters), link them to contracts, see when verification is due, and replace a meter with a guided
wizard that records the final and initial readings. Reading entry itself is T-016.

## Context
- `docs/UI_GUIDELINES.md` (all, incl. §4 "✓ only saves", §6a pitfalls, §6b robustness) — reference screens:
  Properties, Suppliers, Contracts.
- `docs/DATA_MODEL.md` §4 (Meter, MeterRegister, MeterReading, consumption rules 1–6), §3 (ContractMeter).
- Existing Core: `Meter`, `MeterRegister.CreateDefault`, `MeterValidator`, `MeterReadingValidator`,
  `MeterReplacement.Replace(...)` (single + multi-register overload), `Meter.GetVerificationDue()`,
  `Meter.IsInstalledOn`, `ContractMeter`, `ContractMeterValidator`; stores `IMeterStore`, `IContractStore`
  (`ListContractMetersAsync`, `SaveContractMeterAsync`).
- T-014a infrastructure: `RunSafeAsync`/safe commands, `InputLimits`, stay-after-Save, error log.

## Scope — do

### 1. Meters card in the property editor
- Card **Meters** below Contracts; header `IconAdd` (disabled for an unsaved property, same hint as Contracts).
- Rows grouped by resource (Cold water, Hot water, Electricity, Gas, Heat, Other — localized), each row: location
  (or "Meter" + serial), serial number, registers summary ("Day / Night / Export"), **verification badge**:
  - overdue (red) "Verification overdue since {date}",
  - due within 60 days (amber) "Verification due {date}",
  - nothing otherwise;
  - removed meters hidden unless "Show removed" is on (shown muted with "Removed {date}").
- Tap → meter editor page.

### 2. Meter editor page (route `MeterEdit`, params `propertyId`, `id` optional)
Cards:
1. **General**: Resource* (picker), Unit* (picker filtered to units valid for the resource, default: water/gas →
   m³, electricity → kWh, heat → MWh), Serial number, Location, Digits (integer dial digits, optional, 1–12, hint
   "needed for roll-over 99999 → 00000"), Notes.
2. **Registers**: list of registers (kind + direction, localized); `IconAdd` opens a dialog to add Day / Night /
   Peak / Export (Export forces direction Export); delete icon with confirm (not allowed when the register has
   readings → message). A new meter starts with one `Total/Import` register; a quick action
   **"Dual tariff (day/night)"** replaces an unused Total register with Day + Night.
3. **Installation & verification**: Installed on* (date), Verified on (optional), Interval (months, optional,
   hint "water meters usually 72"), Next verification due (optional explicit override); computed line
   "Next verification: {date}" (from `GetVerificationDue`) with the same badge colours.
4. **Contracts**: which contracts of this property use the meter — list of the property's contracts with a
   switch each (on = active `ContractMeter` link valid from the meter's install date or today, whichever is
   later); turning off sets `ValidTo` = yesterday (never delete links). Hint: "A hot water meter is usually linked
   to both the water and the heating contract".
5. **History** (read-only, only for existing meters): "Replaced meter {serial} on {date}" / "Replaced by {serial}
   on {date}" with tap to open that meter.
Footer/toolbar per guidelines; **Delete** only when the meter has no readings (otherwise message "Meters with
readings cannot be deleted — use Replace"). Validation via `MeterValidator`.

### 3. Replace meter wizard (action "Replace meter…" in the meter editor of an installed meter)
Full-screen modal on phones, dialog on tablet/desktop; steps:
1. Date of replacement (default today; must be ≥ install date and ≥ last reading date).
2. **Final reading** of the old meter per register (shows the last known reading as a hint).
3. New meter: serial number, digits (prefilled from the old meter — DATA_MODEL §4 rule 6), verified on,
   interval (prefilled), **initial reading** per register (default 0).
4. Contracts: the old meter's active contract links are preselected for the new meter (switches).
5. Summary → **Replace**. Calls `MeterReplacement.Replace`, saves old + new meter, closes old meter's contract
   links (`ValidTo` = replacement date − 1 day) and creates the new links from the replacement date — all in
   one store operation (add `IMeterStore.SaveReplacementAsync(old, new, linksToClose, linksToCreate)` running in a
   single DbContext transaction).
Cancel at any step → nothing changes. Validation errors per step block "Next".

### 4. Resx (EN + RU)
Enums `MeterResource`, `MeterUnit` (m³, kWh, MWh, Gcal — units shown as symbols in both languages),
`RegisterKind`, `EnergyDirection`; all labels, hints, wizard texts, badge texts, error messages for codes used
by `MeterValidator`, `ContractMeterValidator`, `MeterReadingValidator` (initial/final readings).

## Scope — do NOT
- No regular reading entry/history screen (T-016) — the wizard only creates Final/Initial readings.
- No meter photos yet (T-016).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner can register 2 cold + 2 hot water meters and a dual-tariff electricity meter for one property, link them
   to contracts (hot meters to water **and** heating), and see verification badges.
3. Replacement wizard creates the new meter, readings and contract links correctly; the old meter shows
   "Replaced by …" and disappears from the list unless "Show removed".
4. No input crashes (InputHardeningTests extended to the meter editor and the wizard).

## Tests required
- Data: `SaveReplacementAsync` is atomic (simulate failure on second save → nothing persisted); links closed/created.
- ViewModels: `MeterEditViewModelTests` — unit filtered by resource and defaulted; dual-tariff quick action only when
  Total has no readings; register delete blocked with readings; verification line/badge for overdue / due in 30 days /
  far future (fake clock 2026-10-23: installed 2020-10-01 + 72 months → due 2026-10-01 → overdue; verified
  2021-01-15 + 72 → 2027-01-15 → no badge; explicit due 2026-12-01 → amber); contract switches create/close links
  (never delete); delete blocked with readings.
- `ReplaceMeterWizardViewModelTests` — date before last reading rejected; final reading below last reading rejected
  (unless `Digits` set — roll-over, show warning instead); digits/interval prefilled; contracts preselected; Cancel
  changes nothing; Replace calls the store once with the expected graph: old meter 190 → final 202 on 2026-10-23,
  new meter initial 0, links moved.
- `PropertyEditViewModelTests` — meters card grouping by resource, removed meters hidden by default, badges.
- `InputHardeningTests` — extended to meter editor + wizard fields.

## Manual test steps for the owner
Checklist `docs/UI_GUIDELINES.md` §7, plus:
1. Apartment → **Meters** → ＋ Cold water "Kitchen", serial `CW-1`, installed 01.10.2020, interval 72 → badge
   "Verification overdue since 01.10.2026". Add a second cold meter "Bathroom", and two hot meters.
2. Link: cold meters → water contract; hot meters → water **and** heating contract.
3. Electricity meter → "Dual tariff (day/night)" → registers Day + Night.
4. Open "Kitchen" → **Replace meter…** → date today, final reading 202, new serial `CW-2`, initial 0, contracts
   preselected → Replace. List shows CW-2; "Show removed" shows CW-1 "Removed {date}"; CW-2 → History shows
   "Replaced meter CW-1".
5. Try nonsense in digits/interval/readings → messages, no crash. Phone + tablet short check.
