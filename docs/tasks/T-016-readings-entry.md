# T-016: Readings entry (phone-first), reading history, submit-readings link
Phase: 3 · Depends on: T-015 · Size: L

## Goal
The screen the owner will use every month standing at the meters: pick a property, see every active meter and
register with its last reading, type the new values, get instant consumption and warnings, save all at once,
open the supplier's "submit readings" page and mark the readings as submitted. Plus a reading history per
meter with edit/delete. (Meter photos come in T-016a — attachments infrastructure.)

## Context
- `docs/UI_GUIDELINES.md` (all), `docs/DATA_MODEL.md` §4 (MeterReading, consumption rules), §3 (Contract
  reading window, links `SubmitReadings`), `docs/PAYMENT_TYPES.md` (reading window 25–28, bill month = readings month).
- Existing Core: `MeterReading`, `ReadingKind`, `MeterReadingValidator`, `ConsumptionCalculator`,
  `ConsumptionAnomalies`, `MeterChain`, `ReadingWindow`, `Contract.GetReadingWindow`, `Contract.GetEffectiveLinks`,
  `LinkKind.SubmitReadings`; stores `IMeterStore`, `IContractStore`, `IPropertyStore`, `ISupplierStore`;
  `ILauncherService`, `IToastService`, T-014a safe commands / hardening.

## Scope — do

### 1. Readings screen (flyout item "Readings", replaces the placeholder)
- **Property selector** at the top (picker of non-archived properties; remembers the last choice in
  `Preferences` via a small `ISettingsStore` abstraction in ViewModels; if only one property — no picker).
- **Window banner** per contract of the property that has a reading window and meters:
  "Water utility: submit 25–28 Oct — open now" (green) / "opens in 3 days" (neutral) / "closed, next 25 Nov"
  (muted), computed with `ReadingWindow.For` and `IClock.Today`; if the contract (or its supplier) has a
  `SubmitReadings` link → button **Submit online** (`IconOpenLink`) opening it in the browser.
- **One card per installed meter**, grouped by resource, ordered by location; inside one row per register
  (Day/Night/Export labelled; a single Total register shows no label):
  - last reading: value + date ("last: 437,097 · 26.09.2026");
  - **new value** `Entry` (numeric keyboard, `DecimalInput`), date (default today, shared date picker at the
    top "Reading date" that applies to all; per-row override not needed);
  - live preview: "+ 8,917 m³" (consumption vs last reading via `ConsumptionCalculator.Calculate` on the chain,
    rounded with `MoneyMath.RoundQuantity`), and warnings from `ConsumptionAnomalies.Check` with the last 12
    monthly consumptions (localized: "Unusually high (avg 9,1)", "Zero consumption"); value lower than last:
    error if `Digits` not set, amber "Roll-over assumed (99999 → 0)" if set.
  - optional note (expander "Note").
- **Save all** (primary): validates every filled row (`MeterReadingValidator`), saves all new readings
  (`Kind = Regular`) in one store call (`IMeterStore.AddReadingsAsync(IEnumerable<(Guid registerId, MeterReading)>)`,
  single transaction); rows left empty are ignored; after save the rows clear and "last" updates; toast
  "{n} readings saved". Stay on the page (guidelines §4).
- After saving, a banner "Mark as submitted to {supplier}?" → **Mark submitted** sets `SubmittedOn = today` on the
  readings just saved for meters linked to that contract.
- Back/leave with typed but unsaved values → standard unsaved-changes dialog.
- Layout: phone — single column of cards; tablet/desktop — cards in two columns (or list + history pane: on
  Medium/Expanded, tapping a meter card shows its **history** on the right).

### 2. Reading history (per meter)
- Reachable from the readings screen (tap card header / right pane) and from the meter editor (new card
  "Readings" with the last 5 + "All readings").
- List per register: date, value, consumption since previous, kind badge (Initial / Final / Control / Estimated),
  "submitted {date}" marker, note icon.
- Tap row → edit dialog (date, value, kind: Regular/Control/Estimated — Initial/Final are read-only, submitted on,
  note); trash icon → confirm → soft delete. Validation via `MeterReadingValidator` (no duplicates on same date +
  kind, date within install/removal, Initial/Final rules).

### 3. Small Core/Data additions
- `IMeterStore.AddReadingsAsync` (transactional) and `UpdateReadingAsync`/`DeleteReadingAsync` if the aggregate
  save is not suitable — prefer reusing `SaveAsync(meter)` where simple.
- Close contract links when a meter without readings is deleted (T-015 suggestion): `ValidTo = today`.
- Monthly consumption history for anomalies: helper in Core
  `ConsumptionHistory.Monthly(MeterChain chain, RegisterKind kind, YearMonth upTo, int months, (int,int)? window)`
  returning the consumptions of the previous `months` periods (skips incomplete ones).

### 4. Resx (EN + RU) for all new texts, warnings, banners.

## Scope — do NOT
- No photos/attachments (T-016a). No charges/bills.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. On a phone: enter readings for 2 cold + 2 hot + day/night electricity meters in one go, see consumption
   previews, save all, open the supplier's submit page, mark submitted.
3. History shows readings with consumption; edit and delete work; nothing crashes on bad input.

## Tests required
- Core: `ConsumptionHistory.Monthly` (12 months, gaps skipped, replacement inside history).
- Data: `AddReadingsAsync` atomic; meter delete closes links.
- ViewModels `ReadingsViewModelTests`: property selection remembered; window banner states (fake today 2026-10-23,
  window 25–28 → "opens in 2 days"; 2026-10-26 → "open now"; 2026-10-29 → "closed, next 25.11"); preview
  437.097 → 445.000 = 7.903; lower value without digits → error, with digits 5 → roll-over warning; anomaly with
  history [9,10,8] and +40 → warning; Save all saves only filled rows in one call and clears them; Mark submitted
  sets `SubmittedOn` only on the right meters; unsaved-values dialog on leave; submit link opens the effective
  `SubmitReadings` URL (contract overrides supplier).
- `ReadingHistoryViewModelTests`: consumption column, edit validation (duplicate date+kind), delete confirm,
  Initial/Final read-only.
- `InputHardeningTests` extended to the readings screen.

## Manual test steps for the owner
Checklist `docs/UI_GUIDELINES.md` §7, plus:
1. Give the water contract (or its supplier) a link of type **Submit readings** (any URL).
2. Menu → **Readings** → your apartment. Banner for the water contract with the window state and **Submit online**.
3. Enter new values for all meters (also lower than last on a meter without digits → error; on electricity
   Day/Night both). Previews "+ x m³ / kWh" appear while typing.
4. **Save all** → toast "n readings saved", fields cleared, "last" updated. **Mark submitted** → history shows
   "submitted today".
5. Open a meter's history → edit a value, delete one (confirm). Nonsense input → messages only.
6. Phone (main use), tablet landscape (history pane), Windows.
