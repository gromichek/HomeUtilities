# T-020a: Heating as on the supplier's bill — "Quantity from the bill" basis, VAT on the total
Phase: 5 · Depends on: T-020 · Size: M

## Problem (owner, 2026-10-25, his heating/hot-water bill — not in the repo, numbers below are invented)
The heating supplier's bill has three lines and VAT on the total:

| Line | Quantity | Price excl. VAT | Net |
|---|---|---|---|
| Hot water by the flat's meters | 7,50 m³ | 3,20 €/m³ | 24,00 |
| Heat for heating / circulation (supplier's share from the building meter) | 0,210 MWh | 70,00 €/MWh | 14,70 |
| Hot water difference (share of the building's difference) | 0,150 m³ | 3,20 €/m³ | 0,48 |
| VAT 12 % **of the total 39,18** | | | 4,70 |
| **Total** | | | **43,88** |

Today the app models heating as "from bill", so it cannot compute the expected charge and the comparison is skipped.
The owner also confirmed that all his suppliers calculate **VAT on the total**, not per line.
Decisions are in `docs/DATA_MODEL.md` §5 (`QuantityFromBill`, `QuantityUnit`, charge calculation step 4,
`Contract.VatCalculation`), §6 (`Bill.BillQuantities`) and `docs/PAYMENT_TYPES.md` (catalogue).

## Scope — do
1. **Core**
   - `ChargeBasis.QuantityFromBill`; `TariffComponent.QuantityUnit` (MeterUnit?); validator: unit required, ≥ 1
     rate, no tiers, no meters/parameter.
   - `ChargeInput` gets the quantities from the bill (`IReadOnlyDictionary<Guid, decimal>? QuantitiesFromBill`);
     `ChargeCalculator`: quantity × price for such components; without a quantity → incomplete line + warning
     `ChargeWarningCodes.QuantityFromBill`.
   - **VAT on the total**: `Contract.VatCalculation` (`OnTotal` default, `PerLine`). Totals per VAT % group as in
     DATA_MODEL §5 step 4; `CalculatedCharge` keeps line VATs and adds `VatGroups` (`VatPercent`, `Net`, `Vat`,
     `Gross`) so the UI can show "VAT 12 % of 39,18 = 4,70". `BillComparison` uses the totals.
   - Tests with written-out numbers: the table above (24,00 + 14,70 + 0,48 = 39,18; VAT 12 % = 4,7016 → 4,70;
     total 43,88); a case where per-line and on-total differ: three lines of 0,02 € at 21 % → per line 0,0042 → 0,00 each,
     sum 0,00; on the total 0,06 × 21 % = 0,0126 → 0,01; a group priced incl. VAT; mixed 21 % + 12 % groups; `PerLine` keeps the old result; missing quantity warning.
2. **Data**: migration for `TariffComponents.QuantityUnit`, `Contracts.VatCalculation` (existing rows → `OnTotal`),
   `Bills.BillQuantities` (JSON column like the other owned lists).
3. **Tariff editor (T-017)**: basis "Quantity from the bill" with a one-line explanation ("The supplier tells the
   quantity on the bill (e.g. heat MWh); you type it when you enter the bill."), unit picker, prices; example
   "0,210 MWh → 14,70 + VAT …". New preset **Hot water + heating (distributed)**: "Hot water" PerUnit (hot meters only),
   "Heat for heating/circulation" QuantityFromBill MWh, "Hot water difference" QuantityFromBill m³; VAT LV-REDUCED.
   Keep the old preset, renamed "Heating as one sum" (water heating PerUnit + heating FromBill).
4. **Contract editor**: Billing card — switch "VAT is calculated on the bill total" (on = `OnTotal`), hint
   "Off only if the supplier rounds VAT per line."
5. **Bill editor (T-019)**: for a contract with `QuantityFromBill` components, a card **Quantities from the bill**
   (one `DecimalInput` per component, with unit) above Comparison; the comparison/calculation uses them live; stored
   in `BillQuantities` and in the snapshot. **Charges screen (T-018)**: such lines show "quantity from the bill"; if
   the month has a bill with quantities they are used (and marked "from bill 10.2026").
6. **Sample data**: switch the sample heating contract to the new preset with invented prices (as in the table),
   bills with quantities; extend tests (calculation complete, difference 0,00 for a bill equal to the expected total).
7. **User guide** EN + RU: Tariffs (new basis, new preset, when to use "one sum"), Bills (quantities card),
   Charges (VAT on the total line "VAT 12 % of 39,18"), Contracts (switch). Help anchors unchanged or updated with test.

## Scope — do NOT
- No distribution of building meters by the app (the supplier's formula is unknown); house meter values can be kept
  in custom fields.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner: heating contract with the new preset and his real prices; entering his bill with the two quantities gives
   the same total as the bill (difference 0,00).
3. Existing contracts calculate VAT on the total; water example still 10 m³ × 1,41 → 14,10 + 2,96 = 17,06.

## Manual test steps for the owner
1. Heating contract → Tariff → preset **Hot water + heating (distributed)** → prices from your bill (hot water €/m³,
   heat €/MWh incl. the price change from 01.10), VAT reduced 12 %.
2. Bills → new bill for that contract, month of your bill → **Quantities from the bill**: heat MWh, hot-water
   difference m³ → Comparison shows the calculated total; enter the bill total → difference 0,00 (or the cents
   explained).
3. Charges for that month: three lines, "VAT 12 % of …", total equals the bill.
4. Contract → Billing → "VAT is calculated on the bill total" is on.
