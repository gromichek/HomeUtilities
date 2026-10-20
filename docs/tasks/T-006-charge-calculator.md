# T-006: Charge calculator
Phase: 1 · Depends on: T-005 · Size: L

## Goal
Implement `ChargeCalculator`: for one contract and one billing period it produces the app's own expected
charge (lines + totals) from tariffs, VAT, meter consumption and property parameters. This is the number that
later gets compared with the supplier's bill. Pure C# in Core, no persistence, no UI.

## Context
- `docs/DATA_MODEL.md` §5 "Charge calculation", §4 consumption rules, §2 parameters
- Existing: `TariffComponent`, `TariffRate`, `TariffTier`, `TariffPricing.Slice/ResolveVatPercent`, `VatRate`,
  `ConsumptionCalculator.CalculateForPeriod/Calculate`, `MeterChain`, `ContractMeter`, `Contract`
  (`IsActiveOn`, `ReadingDayFrom/To`), `Property.GetParameter`, `MoneyMath.FromNet/FromGross/RoundQuantity`,
  `YearMonth`.

## Scope — do
Namespace `HomeUtilities.Core.Charges`.

### 1. Small model addition (Tariffs)
Add to `TariffComponent`: `PropertyParameterKind? QuantityParameter` and `string? QuantityParameterName`
(for `Custom`). Defaults when null: `PerArea` → `TotalArea`, `PerResident` → `DeclaredResidents`.
Validator: only allowed for `PerArea`/`PerResident`; `Custom` requires the name.

### 2. Input / output types
```csharp
public sealed record ChargeInput(
    Contract Contract,
    Property Property,
    IReadOnlyList<TariffComponent> Components,      // of this contract
    IReadOnlyList<ContractMeter> ContractMeters,    // of this contract
    IReadOnlyList<Meter> PropertyMeters,            // all meters of the property (for chains)
    IReadOnlyCollection<VatRate> VatCatalog,
    YearMonth Period);

public sealed record ChargeLine(
    Guid ComponentId, string ComponentName, ServiceKind ChargeKind, ChargeBasis Basis,
    decimal? Quantity, decimal? UnitPrice, decimal Net, decimal VatPercent, decimal Vat, decimal Gross,
    bool IsFromBill, bool IsComplete, IReadOnlyList<ConsumptionResult> Consumption);

public sealed record CalculatedCharge(
    Guid ContractId, YearMonth Period, Currency Currency,
    IReadOnlyList<ChargeLine> Lines, decimal TotalNet, decimal TotalVat, decimal TotalGross,
    bool IsComplete, IReadOnlyList<ChargeWarning> Warnings);

public sealed record ChargeWarning(string Code, Guid? ComponentId = null, object?[]? Args = null);
// codes in static class ChargeWarningCodes
```

### 3. `ChargeCalculator.Calculate(ChargeInput input)` rules
General:
- Contract not active on `Period.LastDay` (and not on `Period.FirstDay`) → empty lines, warning
  `ContractInactive`, `IsComplete = true`, totals 0.
- Components: non-deleted, ordered by `SortOrder`, then `Name`.
- Rate date: for `PerUnit`/`NetExport` = the **end reading date** of the consumption (latest end reading
  date over all involved meters), fallback `Period.LastDay` if no end reading; for other bases = `Period.FirstDay`.
- No rate on the rate date → line with zeros, `IsComplete = false`, warning `NoRateForDate`.
- Rate currency ≠ contract currency → line with zeros, `IsComplete = false`, warning `CurrencyMismatch`.
- VAT percent via `TariffPricing.ResolveVatPercent(component, catalog, rateDate)`; on
  `InvalidOperationException` → line with zeros, `IsComplete = false`, warning `UnknownVat`.
- Amount of a line: `amount = quantity × unitPrice` (unrounded); if `rate.PriceIncludesVat` →
  `MoneyMath.FromGross(amount, vat)`, else `MoneyMath.FromNet(amount, vat)`.
- Totals = sums of line Net / Vat / Gross. `IsComplete` = all lines complete.

Per basis:
- **PerUnit**: meters = `ContractMeters` active on `Period.LastDay` (non-deleted), filtered by `MeterIds` if
  non-empty. For each such meter build `MeterChain.Build(PropertyMeters, meterId)`; de-duplicate chains (two
  links into the same chain count once). Registers = `RegisterKinds` if non-empty, else every register kind
  with `Direction = Import` present on the chain. For each (chain, register kind):
  `ConsumptionCalculator.CalculateForPeriod(chain, kind, Period, window)` where window =
  `(Contract.ReadingDayFrom, Contract.ReadingDayTo)` if both set, else null. Quantity = sum of results;
  any incomplete result → line incomplete + warning `ConsumptionIncomplete`. Consumption warnings are kept in
  `ChargeLine.Consumption`.
  - **Tiers**: when the rate has tiers, one `ChargeLine` **per price slice** (same component id/name).
    `alreadyUsed` = consumption from the start of the tier period (`Month` → `Period.FirstDay`, so 0;
    `Year` → January 1 of `Period.Year`) up to the start reading date of this period, computed with
    `ConsumptionCalculator.Calculate(chain, kind, tierStart, startReadingDate)` summed over chains/kinds;
    0 if not computable (+ warning `TierUsageUnknown`).
- **NetExport**: like PerUnit but register kind `Export` only; quantity is **negated** (credit line).
  No tiers (validator already forbids? if a rate has tiers → `ArgumentException` from `Slice` → treat as
  incomplete + warning `InvalidTariff`).
- **PerArea / PerResident**: quantity = `Property.GetParameter(kind or custom name, Period.FirstDay)`;
  missing → incomplete + warning `MissingParameter`.
- **FixedMonthly**: quantity 1, unit price = rate price.
- **FromBill**: line with `IsFromBill = true`, `Quantity`/`UnitPrice` null, amounts 0, complete. (The bill
  provides the amount later; T-008 combines them.)

Rounding: quantities in lines via `MoneyMath.RoundQuantity`; unit price unrounded as given by the rate.

## Scope — do NOT
- No bills, payments, balances (T-008). No persistence, no UI.
- No splitting of a period on a mid-month price change (v1 limitation, document in XML doc).
- No reading overrides (later).

## Acceptance criteria
1. Implemented as specified, XML doc comments on public members.
2. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.

## Tests required (`Charges/ChargeCalculatorTests.cs`; build fixtures with a small test builder; invented ids/names)
Numbers come from the owner's real pattern; write the arithmetic in comments.
1. **Water + sewerage**, contract window 20–26, cold meter 425.900 (2026-08-26) → 437.097 (2026-09-26),
   hot meter 371.927 → 380.844, both linked to the contract, period 2026-09:
   - quantity 11.197 + 8.917 = 20.114 m³
   - "Water supply" 1.41 net, VAT LV-STANDARD 21%: 28.36074 → net 28.36, VAT 5.96, gross 34.32
   - "Sewerage" 0.86 net, 21%: 17.29804 → net 17.30, VAT 3.63, gross 20.93
   - total gross 55.25 (the owner's spreadsheet shows 55.247 for this month)
2. **Hot water heating**, heat contract linked only to the hot meter, 4.56 net, VAT override 12%:
   8.917 × 4.56 = 40.66152 → net 40.66, VAT 4.88, gross 45.54; plus "Heating" `FromBill` line (0, IsFromBill).
3. **Maintenance PerArea**: TotalArea 52.30 m², 0.6711 €/m², ZERO VAT → 35.09853 → 35.10 / 0 / 35.10.
4. **Waste PerResident**: residents 3 until 2026-05-31, 2 from 2026-06-01; 7.50 €, 21%: period 2026-05 →
   22.50 / 4.73 / 27.23; period 2026-06 → 15.00 / 3.15 / 18.15.
5. **Internet FixedMonthly** 22.95 incl. VAT 21% → net 18.97, VAT 3.98, gross 22.95.
6. **Solar NetExport** export 1 200 → 1 450 kWh, 0.05 €, 21% → quantity −250, net −12.50, VAT −2.63, gross −15.13.
7. **Dual tariff** component Day only (RegisterKinds = [Day]) and another Night only, different prices.
8. **Tiers (Year)**: tiers {≤1 200 @ 0.10, unlimited @ 0.15}; Jan 1 reading 10 000; start of period reading
   11 150; end reading 11 350 → alreadyUsed 1 150 → slices (50 @ 0.10 = 5.00), (150 @ 0.15 = 22.50).
9. **Rate change**: water rate 1.30 from 2026-01-01, 1.41 from 2026-09-15; end reading 2026-09-26 → 1.41 used.
10. **Meter replacement inside period** is handled (quantity across replacement), new meter not linked to
    contract but in the same chain.
11. Missing end reading → line quantity 0, line and charge `IsComplete = false`, warning `ConsumptionIncomplete`.
12. No rate for date, unknown VAT code, currency mismatch, missing parameter, inactive contract — one test each.
13. Two contract links to meters of the same chain → counted once.

## Manual test steps for the owner
n/a (no UI changes).
