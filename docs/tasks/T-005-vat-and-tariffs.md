# T-005: Reading-window fallback fix, VAT catalog, tariff components and rates
Phase: 1 · Depends on: T-004 · Size: M

## Goal
1. Fix the reading selection fallback in `ConsumptionCalculator.CalculateForPeriod` (open question from T-004).
2. Add the VAT catalog and the tariff model (components, time-versioned rates, tiers) with rate/price
   resolution logic. The actual monthly charge calculation is **T-006** — not here.

Pure C# in Core, unit-tested. No persistence, no UI.

## Context
- `docs/DATA_MODEL.md` §4 rule 5 (updated), §5 (Tariffs and VAT)
- Existing: `ConsumptionCalculator`, `ReadingWindow`, `Versioned`, `IVersioned`, `MoneyMath`,
  `ValidationResult`, `ErrorCodes`, `ServiceKind`, `RegisterKind`, `Currency`.

## Part 0 — reading-window fallback fix (`ConsumptionCalculator.CalculateForPeriod`)
Current behaviour: when there is no reading inside the window, the latest reading on/before the window end
is used, which may be the previous period's reading → consumption 0.

New rule for picking the reading of period `P` with window `[from, to]`:
1. Latest reading inside `[from, to]`.
2. Else the **earliest** reading with `to < date < nextFrom`, where `nextFrom` = window start of `P + 1`
   (a late reading). Warning `ReadingOutsideWindow`.
3. Else the latest reading with `date < from` that is **not** the reading already chosen for `P − 1`
   (an early reading). Warning `ReadingOutsideWindow`.
4. Else none → `MissingEndReading` / `MissingStartReading` as today.
Pick the reading of `P − 1` first using the same rule, then pick `P` so that the same reading is never used
for both (if rule 3 would return the start reading, treat as none).

Tests (add to `ConsumptionCalculatorTests`, keep existing ones; update the existing fallback test to the new rule):
- window 20–26, readings 2026-09-24 (437.097), 2026-10-28 (445.000) → period 2026-10 = 7.903, warning
  `ReadingOutsideWindow` (late reading used).
- readings 2026-09-24 (437.097), 2026-10-15 (441.000) → period 2026-10 = 3.903, warning (early reading).
- readings 2026-09-24 only → period 2026-10 incomplete, `MissingEndReading`, quantity 0.
- late reading not stolen from the next period: readings 2026-09-24, 2026-10-28, 2026-11-22 → period 2026-10
  ends at 2026-10-28; period 2026-11 runs 2026-10-28 → 2026-11-22.

## Part 1 — VAT catalog (`HomeUtilities.Core.Tariffs`)
- `VatRate : SyncEntity` — `Code` (string, unique, upper-case, `^[A-Z0-9-]{1,32}$`), `Name`,
  `List<VatRateVersion> Versions`.
- `VatRateVersion : SyncEntity, IVersioned` — `VatRateId`, `ValidFrom`, `Percent` (decimal, 0–100).
- `decimal? VatRate.PercentAt(DateOnly date)` via `Versioned.At`.
- `static class LatvianVatDefaults` → `IReadOnlyList<VatRate> Create()` returning **new instances** with:
  - `LV-STANDARD` "Standard rate": 1995-01-01 18%, 2009-01-01 21%, 2011-01-01 22%, 2012-07-01 21%
  - `LV-REDUCED` "Reduced rate": 2004-05-01 5%, 2009-01-01 10%, 2011-01-01 12%
  - `ZERO` "Zero / exempt": 1995-01-01 0%
  Mark in the XML doc that these are editable defaults, to be verified by the owner. Names are English
  identifiers here; localization of catalog names is a UI concern later.
- `VatRateValidator`: code format, name not blank, versions valid (0–100), no duplicate `ValidFrom`.

## Part 2 — tariff model (`HomeUtilities.Core.Tariffs`)
Enums (explicit int values): `ChargeBasis { PerUnit, PerArea, PerResident, FixedMonthly, FromBill, NetExport }`,
`TierPeriod { Month, Year }`.

- `TariffComponent : SyncEntity` — fields per §5: `ContractId`, `Name`, `ChargeKind` (`ServiceKind`),
  `Basis`, `List<RegisterKind> RegisterKinds` (empty = all import registers), `List<Guid> MeterIds`
  (empty = all contract meters), `VatCode`, `decimal? VatPercentOverride`, `List<TariffRate> Rates`, `SortOrder`.
  - `TariffRate? RateAt(DateOnly date)`.
- `TariffRate : SyncEntity, IVersioned` — `ComponentId`, `ValidFrom`, `Price`, `PriceIncludesVat`,
  `Currency`, `List<TariffTier> Tiers`, `TierPeriod? TierPeriod`.
- `TariffTier` — **owned value**, not a SyncEntity: `record TariffTier(decimal? UpToQuantity, decimal Price)`.
  Tiers are ordered by `UpToQuantity`; the last tier has `UpToQuantity = null` (unlimited).
  When `Tiers` is non-empty, `Price` on the rate is ignored.

### Price resolution — `static class TariffPricing`
```csharp
// Splits a quantity into tier slices. alreadyUsed = quantity already consumed earlier in the same tier period.
IReadOnlyList<PriceSlice> Slice(TariffRate rate, decimal quantity, decimal alreadyUsed = 0m);
record PriceSlice(decimal Quantity, decimal UnitPrice);

// VAT percent effective for a component on a date (override wins; otherwise catalog by code).
decimal ResolveVatPercent(TariffComponent component, IReadOnlyCollection<VatRate> catalog, DateOnly date);
```
- No tiers → one slice `(quantity, rate.Price)`.
- With tiers → fill tiers in order starting after `alreadyUsed`. Negative quantity (export) → no tiers
  allowed (`ArgumentException`).
- `ResolveVatPercent` throws `InvalidOperationException` if the code is unknown or has no version on `date`.

### Validation — `TariffComponentValidator`
`Name` not blank/≤100; `ContractId` not empty; `VatCode` non-empty unless `VatPercentOverride` set; override
0–100; at least one active rate; no duplicate `ValidFrom`; `Price >= 0`; tiers: ascending `UpToQuantity`,
only the last tier may be unlimited, last tier **must** be unlimited, all prices ≥ 0, `TierPeriod` required
when tiers exist; `RegisterKinds`/`MeterIds` only allowed for `PerUnit` and `NetExport`; `NetExport` requires
`RegisterKinds` = [`Export`] or empty (then means Export).

## Scope — do NOT
- No `ChargeCalculator`, no charge lines/money totals (T-006).
- No persistence, no UI.

## Acceptance criteria
1. Part 0 behaviour as specified; all previous tests still pass (one fallback test intentionally updated).
2. Parts 1–2 implemented with XML doc comments; `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.

## Tests required
- `VatRateTests`: LV-STANDARD at 2008-12-31 = 18, 2009-01-01 = 21, 2011-06-01 = 22, 2012-07-01 = 21,
  2026-10-19 = 21; LV-REDUCED at 2008-06-01 = 5, 2011-01-01 = 12; `Create()` returns new instances each call.
- `VatRateValidatorTests`: bad code (`lv standard`), percent 101, duplicate ValidFrom.
- `TariffPricingTests`:
  - no tiers: 12.084 m³ × 1.41 → one slice.
  - tiers {≤100 @ 0.1065, unlimited @ 0.0818}, quantity 150, alreadyUsed 0 → (100, 0.1065), (50, 0.0818).
  - same, alreadyUsed 80 → (20, 0.1065), (130, 0.0818).
  - alreadyUsed 120 → (150, 0.0818).
  - quantity 0 → no slices.
  - negative quantity with tiers → throws; without tiers → one negative slice.
  - `ResolveVatPercent`: override 12 wins; code LV-STANDARD on 2011-06-01 = 22; unknown code throws.
- `TariffComponentTests`: `RateAt` across two rates (2025-07-01 heating 3.50, 2025-11-01 4.56):
  2025-10-31 → 3.50, 2025-11-01 → 4.56.
- `TariffComponentValidatorTests`: each rule failing + passing.

## Manual test steps for the owner
n/a (no UI changes).
