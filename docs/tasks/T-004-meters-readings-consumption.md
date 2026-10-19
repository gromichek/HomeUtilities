# T-004: Meters, registers, readings, contract–meter link, consumption calculator
Phase: 1 · Depends on: T-003 · Size: L

## Goal
Model physical meters (incl. multi-register and solar export), their readings, replacement history and
verification, link meters to contracts, and implement `ConsumptionCalculator` — the core logic that turns
readings into consumption per period, correctly handling rollover, meter replacement and anomalies.
Pure C# in Core, fully unit-tested. No persistence, no UI.

## Context
- `docs/DATA_MODEL.md` §3 (`ContractMeter`), §4 (Meters — all of it, especially "Consumption rules")
- Types from T-002/T-003: `SyncEntity`, `IVersioned`, `Versioned`, `ValidationResult`, `ErrorCodes`,
  `MoneyMath.RoundQuantity`, `YearMonth`, `IClock`, `Contract.GetReadingWindow`.

## Scope — do
Namespace `HomeUtilities.Core.Domain` (entities, enums), `HomeUtilities.Core.Consumption` (calculator).

### 1. Enums (explicit integer values)
`MeterResource { ColdWater, HotWater, Electricity, Gas, Heat, Other }`,
`MeterUnit { M3, KWh, MWh, Gcal }`, `RegisterKind { Total, Day, Night, Peak, Export }`,
`EnergyDirection { Import, Export }`,
`ReadingKind { Regular, Initial, Final, Control, Estimated }`.

### 2. Entities (derive from `SyncEntity`)
- `Meter` — fields per §4 + `List<MeterRegister> Registers`.
  - `DateOnly? GetVerificationDue()` — `NextVerificationDue` if set; else
    `(VerifiedOn ?? InstalledOn).AddMonths(VerificationIntervalMonths)` if interval set; else null.
  - `bool IsInstalledOn(DateOnly date)` — `InstalledOn <= date && (RemovedOn is null || date <= RemovedOn)`, not deleted.
- `MeterRegister` — fields per §4 + `List<MeterReading> Readings`.
  - `static MeterRegister CreateDefault(Guid meterId)` → `Total`/`Import`.
- `MeterReading` — fields per §4.
- `ContractMeter : SyncEntity, IVersioned` — `ContractId`, `MeterId`, `ValidFrom`, `ValidTo?`.
  - `bool IsActiveOn(DateOnly date)`.

### 3. Validation (static validators, codes in `ErrorCodes`, format `entity.rule`)
- `MeterValidator`: `PropertyId` not empty; `Digits` null or 1–12; `RemovedOn >= InstalledOn`;
  `VerificationIntervalMonths` null or 1–600; at least one non-deleted register; no two non-deleted
  registers with the same (`Kind`, `Direction`); `Export` kind requires `Direction = Export` and vice versa;
  `Unit` compatible with `Resource` (water/gas → M3; electricity → KWh; heat → MWh/Gcal/KWh; Other → any).
- `MeterReadingValidator` (validates a reading in the context of its register and meter):
  `Value >= 0`; `Date` within `InstalledOn..RemovedOn` of the meter; only one `Initial` per register and it
  must be the earliest; only one `Final` and it must be the latest and dated = `RemovedOn` (if set);
  no two non-deleted readings on the same date with the same `Kind`.
- `ContractMeterValidator`: ids not empty, `ValidTo >= ValidFrom`.

### 4. Replacement helper
`static class MeterReplacement` →
`MeterReplacementResult Replace(Meter old, DateOnly date, decimal finalValue, decimal initialValue, IClock clock)`
where `sealed record MeterReplacementResult(Meter NewMeter, IReadOnlyList<MeterReading> FinalReadings,
IReadOnlyList<MeterReading> InitialReadings)`:
- sets `old.RemovedOn = date`, adds a `Final` reading per old register (for multi-register meters the method
  takes `IReadOnlyDictionary<RegisterKind, (decimal Final, decimal Initial)>` — provide this overload and make
  the single-value one call it for `Total`);
- creates a new meter on the same property, same resource/unit/location, `InstalledOn = date`,
  `ReplacedMeterId = old.Id`, same register kinds, `Initial` readings;
- touches all new/changed entities with `clock`.
Linking the new meter to the old meter's contracts is done by the caller (later, in the service layer) —
**not** here; document this in the XML doc.

### 5. ConsumptionCalculator (`HomeUtilities.Core.Consumption`)
Input model (no dependency on contracts):
```csharp
public sealed record MeterChain(IReadOnlyList<Meter> Meters); // ordered old → new via ReplacedMeterId
```
`MeterChain.Build(IEnumerable<Meter> meters, Guid anyMeterId)` — walks `ReplacedMeterId` both directions,
returns the chain ordered by `InstalledOn`.

API:
```csharp
ConsumptionResult Calculate(MeterChain chain, RegisterKind kind, DateOnly from, DateOnly to);
```
Meaning: consumption between the reading taken **on or before `from`** (the "start reading", latest such)
and the reading taken **on or before `to`** (the "end reading", latest such), over register `kind`
(Direction taken from the register).

`ConsumptionResult`: `decimal Quantity`, `StartReading`, `EndReading` (`MeterReading?`),
`IReadOnlyList<ConsumptionSegment> Segments` (one per meter involved: meter id, start value, end value,
quantity, `bool Rollover`), `IReadOnlyList<ConsumptionWarning> Warnings`, `bool IsComplete`.

Rules (from DATA_MODEL §4):
1. Single meter: `end − start`.
2. Rollover: if `end < start` and `Digits` set → `end + 10^Digits − start`, segment flagged `Rollover`,
   warning `RolloverAssumed`. If `Digits` null → `IsComplete = false`, warning `NegativeConsumption`,
   `Quantity = 0`.
3. Replacement inside the interval: sum segments — old meter `(Final − start)` + new meter
   `(end − Initial)`; also handles several replacements. Readings of different meters are never subtracted.
4. Missing start or end reading → `IsComplete = false`, warning `MissingStartReading`/`MissingEndReading`,
   `Quantity = 0`.
5. Ignore deleted readings. `Estimated` readings are used but add warning `EstimatedReadingUsed`.
   `Control` readings are used like `Regular`.
6. Quantities rounded with `MoneyMath.RoundQuantity` (3 decimals).

Also provide:
```csharp
ConsumptionResult CalculateForPeriod(MeterChain chain, RegisterKind kind, YearMonth period,
                                     (int From, int To)? readingWindowDays);
```
Picks the end reading for `period` and for `period − 1` and returns consumption between them:
- with a window: the latest reading whose date is inside `Contract`-style window of that period
  (reuse the same window logic as `Contract.GetReadingWindow` — extract it into a shared static helper
  `ReadingWindow.For(YearMonth, int from, int to)` in Core and make `Contract.GetReadingWindow` call it);
- without a window: the latest reading on or before `period.LastDay`.
If there's no reading in the window, fall back to the latest reading on or before the window end and add
warning `ReadingOutsideWindow`.

### 6. Anomaly detection
`static class ConsumptionAnomalies`:
`IReadOnlyList<ConsumptionWarning> Check(decimal current, IReadOnlyList<decimal> history, decimal factor = 3m)`
— warning `UnusuallyHigh` when `history.Count >= 3` and `current > factor × average(history)`;
`ZeroConsumption` when `current == 0` and the average of history > 0.

`ConsumptionWarning` = record with `Code` (string constant in `ConsumptionWarningCodes`) and optional `Args`.

## Scope — do NOT
- No tariffs, charges, money.
- No EF Core, no UI, no services/repositories.
- Do not attach meters to contracts automatically on replacement.

## Acceptance criteria
1. All of the above implemented with XML doc comments on public members.
2. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
3. `Contract.GetReadingWindow` behaviour unchanged (T-003 tests still pass) after extracting `ReadingWindow`.

## Tests required (invented data, concrete numbers in comments)
- `MeterTests`: verification due — installed 2015-04-30, interval 72 → 2021-04-30; VerifiedOn 2021-05-10
  overrides InstalledOn → 2027-05-10; explicit `NextVerificationDue` wins; no interval → null.
  `IsInstalledOn` boundaries.
- `MeterValidatorTests`: every rule, failing + passing.
- `MeterReadingValidatorTests`: every rule.
- `MeterReplacementTests`: single-register (old final 202, new initial 0 on 2015-04-30); dual-tariff
  (Day/Night); new meter has `ReplacedMeterId`, same location; old `RemovedOn` set; timestamps set.
- `ConsumptionCalculatorTests`:
  - simple: 413.816 → 425.900 = 12.084 m³
  - rollover with Digits 5: 99 950 → 12 = 62; without Digits → incomplete + `NegativeConsumption`
  - replacement: old 190 → Final 202 (12), new Initial 0 → 13 (13) = 25 m³, two segments
  - two replacements in one interval (3 segments)
  - dual tariff: Day and Night calculated independently
  - solar: Export register 1 200 → 1 450 = 250 kWh
  - missing start → incomplete; estimated reading → warning
  - `CalculateForPeriod` with window 20–26: readings 2026-09-24 (437.097) and 2026-10-25 (445.000)
    → 7.903; reading only on 2026-10-28 → fallback + `ReadingOutsideWindow`; no window → readings on
    or before month end
- `ConsumptionAnomaliesTests`: history [10, 11, 9] → 40 is `UnusuallyHigh`, 25 is not; history of 2 →
  no warning; 0 with history avg > 0 → `ZeroConsumption`.
- `MeterChainTests`: chain built from the middle meter returns all 3 in order; meters of other chains ignored.

## Manual test steps for the owner
n/a (no UI changes).
