# T-018: Charges screen — what each contract should cost for a month
Phase: 4 · Depends on: T-017 · Size: M–L

## Goal
With readings (T-016) and tariffs (T-017) in place, show the owner the **expected charge** of every contract of a
property for a month, line by line, with the readings behind each quantity, so that he can check the supplier's
bill (the comparison itself comes with bill entry in T-019).

## Context
- `docs/DATA_MODEL.md` §4 "Consumption rules" (reading selection, replacement, rollover), §5 "Charge calculation".
- Core: `ChargeCalculator.Calculate(ChargeInput)` → `CalculatedCharge` (`Lines`, totals, `IsComplete`,
  `Warnings`), `ChargeLine` (quantity, unit price, Net / VatPercent / Vat / Gross, `IsFromBill`, `Consumption`),
  `ConsumptionResult` (start/end reading, segments across a replacement, warnings), `ChargeWarningCodes`.
  Stores for properties, contracts, tariffs, meters, readings, VAT. `docs/UI_GUIDELINES.md` (all).
- Nothing is persisted by this task: charges are calculated on demand (the snapshot is stored with the bill, T-019).

## Scope — do

### 1. Menu item **Charges / Начисления** (between Readings and Bills), route `Charges`
- Header: **Property** picker (same behaviour as Readings: remembers the last property), **Month** with ‹ › buttons
  and a tap to pick month/year. Default month: the **current** month if the reading window of any contract of the
  property has already started this month (`today ≥ window start` for the current month), otherwise the
  **previous** month (e.g. readings 25–28 October → from 25 October the screen shows October). No contract with a
  window → previous month. Put this rule in a small testable class (`ChargePeriodDefaults`).
- Property total for the month: Net · VAT · Total, and "incomplete" when any contract is incomplete.

### 2. One card per contract of the property active in the month (`IsActiveOn` any day of the month)
- Card header: contract title, supplier, category colour dot, **Total** (gross) of the month, status:
  complete / incomplete (warning icon) / "from bill" only. Collapsed by default on phones, expanded on wide layouts;
  `IconExpand`/`IconCollapse`. Contracts without tariff components: one muted line "No tariff — add it in the
  contract" with a link that opens the contract editor (Tariff card).
- Lines (one per `ChargeLine`; tiered components show each slice): component name, quantity with unit
  ("5,500 m³", "52,30 m²", "2 residents", "1 month"), unit price ("× 1,41 € excl. VAT" / "incl. VAT"), Net,
  VAT % and VAT, Total. `IsFromBill` lines: "Amount from the supplier's bill" and no amounts.
- **Readings behind a quantity** (tap the line → expandable detail, or a dialog on phones): per meter/register:
  start reading (value, date), end reading (value, date), consumption; replacement shown as two segments
  ("old meter 123,000 → final 125,500; new meter initial 0,000 → 2,000"); roll-over flagged; reading taken
  outside the window flagged.
- **Warnings** under the card, localized, each with what to do:
  `NoRateForDate` → "No price valid on {date} for «{component}» — add a price in the tariff." (opens the component
  editor); `MissingParameter` → "«{parameter}» has no value on {date} — add it in the property." (opens the property);
  `ConsumptionIncomplete` / `NoMeters` → "Readings missing for {meter} — enter them in Readings." (opens Readings with
  property + date); `UnknownVat`, `CurrencyMismatch`, `TierUsageUnknown`, `InvalidTariff`, `ContractInactive` → plain
  messages. Consumption warnings (high, zero, roll-over, date before last) are shown on their detail rows.
- Every link uses normal push navigation (Back returns to Charges, same month and property).

### 3. Entry points
- Contract editor → Tariff card: secondary button **Show charges** → Charges with the contract's property and the
  default month, scrolled to/expanded on that contract.
- `IconHelp` "?" on the Charges screen → new `HelpTopic.Charges` (anchor test for both languages).

### 4. ViewModels
- `ChargesViewModel` loads everything for the property once per (property, month) change, builds `ChargeInput`s and
  calls `ChargeCalculator`; no calculation logic in the VM beyond mapping. Calculation runs off the UI thread only
  if needed — keep it simple (it is fast). Recalculate when the page appears again (tariffs/readings may have changed).
- Robustness (§6b): an exception in one contract's calculation becomes an error line on that card (logged), the rest
  of the screen works.

### 5. User guide
New section "Charges" in `docs/user-guide/en.md` and `ru.md`: what the expected charge is and where its numbers come
from (readings → consumption → tariff → VAT), how the month is chosen, how to read a line and its readings,
what each warning means and how to fix it, that nothing is stored yet (bills come later). Update the relations
diagram (`img/relations.*.mmd` → re-render with `scripts/render-guide-diagrams.sh`): add Tariff (contract has
tariff components) and VAT rate; Charge = readings × tariff. Remove "calculated monthly charges" from "Coming soon".

### 6. Sample data
No new entities. Make sure the generated data gives at least: one complete contract, one with a replacement inside
a recent month, one stepped price, one `FromBill`, one contract with a missing price (warning) — adjust the generator
if needed and add a test that runs `ChargeCalculator` over all sample contracts for the last 3 months without
exceptions.

## Scope — do NOT
- No storing of charges, no bill comparison, no reading override (T-019).
- No all-properties summary screen (dashboard, T-022), no charts (T-024).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Sample data: Charges for Flat in town shows every contract with lines and totals; the replaced meter shows two
   segments; the warning links open the right editor.
3. Numbers on screen equal `ChargeCalculator` results (VM tests with written-out numbers, e.g.
   `5.500 m³ × 1.41 = 7.76 net; VAT 21 % = 1.63; total 9.39`).
4. Nothing crashes for a property without contracts, a contract without tariff, a month without readings.

## Tests required
- `ChargePeriodDefaultsTests`: window 25–28 on 24 Oct → September, on 25 Oct → October; window 28–3 on 27 Oct →
  September, on 28 Oct → October; no windows → previous month; January → December of the previous year.
- `ChargesViewModelTests`: property/month switching, totals, line texts (units, incl./excl. VAT), from-bill line,
  tiered slices, replacement segments, each warning → message + navigation target, contract without tariff,
  inactive contract hidden, error in one contract isolated, recalculation on appearing, help command.
- Sample data test as in §6. `InputHardeningTests`: month picker extremes.

## Manual test steps for the owner
1. Debug build: Settings → Test data → Delete all data → reopen → Fill with sample data.
2. Menu **Charges** → Flat in town → current/previous month as described → each contract card with lines and totals;
   ‹ › to change the month.
3. Tap the water line → readings behind it; find the month with the replaced meter → two segments.
4. A warning "No price valid…" → link opens the tariff component; add a price → Back → warning gone.
5. Contract editor → Tariff → **Show charges** → Charges opens on that contract.
6. Phone, tablet, wide window; dark theme; Russian texts fit; "?" opens the Charges section.
