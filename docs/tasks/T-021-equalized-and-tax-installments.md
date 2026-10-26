# T-021: Equalized payments with annual settlement; property tax installments
Phase: 5 · Depends on: T-020a · Size: L

## Goal
Two kinds of contracts are not "bill = consumption": (1) **equalized** electricity/gas — the same amount every month,
the difference to the real consumption is settled once a year; the owner wants to know during the year how big the
settlement will be. (2) **Property tax** — one yearly amount paid in installments with fixed due dates.

## Context
- `docs/DATA_MODEL.md` §6 "Equalized billing", §7 "Property taxes"; `docs/PAYMENT_TYPES.md` catalogue rows
  "Electricity, equalized", "Gas (propane), equalized", "Property tax".
- Existing: `BillingMode.Equalized` / `Assessed`, `BillKind.Equalized` / `Settlement` / `TaxInstallment` (bill defaults
  already map mode → kind), bills, payments, `ChargeCalculator`, `ChargeInputLoader`, Charges screen, contract editor.
- `EqualizedPlan` does **not** exist yet.

## Scope — do

### A. Equalized plans
1. **Core**: `EqualizedPlan : SyncEntity { ContractId, ValidFrom, ValidTo?, MonthlyAmount (gross, decimal), Notes }`,
   validator (amount > 0, no overlapping periods per contract, ValidTo ≥ ValidFrom), `EqualizedPlans.AmountFor(plans,
   YearMonth)`. **Data**: entity configuration, store (`IEqualizedPlanStore.ListByContractAsync`), migration (generated
   with `dotnet ef`), backup/restore covers it automatically (check), round-trip test.
2. **Settlement calculation** (Core, `EqualizedSettlement.Calculate`): for a contract and a range of months returns per
   month `{ Period, PlanAmount, Billed (gross of Equalized bills of the month), Actual (ChargeCalculator total gross,
   IsComplete), Difference = Actual − Billed }` and totals; **expected settlement = Σ Actual − Σ Billed**
   (positive = surcharge, negative = refund). Range: from the month after the last `Settlement` bill's `Period`
   (or the first plan's `ValidFrom`) to the current month. Months without readings → `Actual` incomplete → totals
   marked "incomplete, n months missing". Tests with written-out numbers, e.g. plan 40,00 × 6 = 240,00 billed; actual
   35,20 + 31,10 + 38,40 + 44,90 + 52,30 + 47,60 = 249,50 → expected surcharge 9,50.
3. **Contract editor** (BillingMode Equalized only): card **Equalized plan** — rows "from 01.06.2026 — 40,00 €/month"
   (newest first, child dialog: Valid from*, Valid to, Monthly amount*, Notes; trash with confirm), and a summary line
   "Expected settlement: +9,50 € (surcharge) for 06.2026–11.2026" with **Show settlement**.
4. **Settlement page** (pushed, route `EqualizedSettlement`, param `contractId`): month table (period, plan, billed,
   actual, difference, running total), incomplete months marked with a link to Readings, totals and the expected result
   in words ("You will probably pay about 9,50 € extra" / "You will probably get about 12,00 € back"); button
   **Enter settlement bill** → new bill, Kind Settlement, period = current month, the expected amount as a placeholder
   (not a value). Help topic + "?".
5. **Bills**: for an Equalized contract a new bill defaults to Kind Equalized and Total = plan amount of its period
   (editable). The comparison card for Equalized bills compares with the **plan**, not the calculation, and shows the
   calculation as information ("actual by readings 35,20 €").
6. **Charges screen**: equalized contracts show a line "Plan 40,00 € · actual 35,20 € · since last settlement +9,50 €".

### B. Property tax installments
1. Contract with BillingMode Assessed: card **Tax by year** — per year: total, installments (due date, amount, status
   badge), paid, remaining; **＋ Enter yearly tax** opens a dialog: Year* (default current), Yearly amount*, Number of
   installments (1 / 2 / 4; default 4), due dates pre-filled with the Latvian defaults **31.03, 15.05, 15.08, 15.11**
   (2 installments: 31.03 and 15.08; 1: 31.03) — all editable — and amounts split evenly with the remainder cents on
   the first installment (e.g. 101,00 / 4 → 25,25 × 4; 100,03 / 4 → 25,03 + 25,00 × 3). Creates one `TaxInstallment`
   bill per installment: `Period` = month of its due date, `DueOn` = due date, `ServiceFrom/ServiceTo` = 01.01–31.12 of
   the year (reports spread it), title "Property tax 2026, installment 2 of 4". Logic in Core (`TaxInstallmentPlanner`),
   unit tested (splits, dates per count, leap years irrelevant, validation of dates inside the year and ascending).
2. Paying the whole year at once works with the T-020 payment editor (one payment allocated to all installments) —
   mention it in the guide; the card offers **Pay whole year** → payment editor pre-filled with all open installments.
3. A second "Enter yearly tax" for a year that already has installments asks "Installments for 2026 exist. Replace the
   unpaid ones?" (Replace / Cancel) — paid ones are kept, unpaid ones soft-deleted and recreated.

### C. Sample data, guide
- `SampleDataGenerator`: make the cottage electricity contract Equalized with two plan versions, 12 monthly Equalized
  bills (paid), a Settlement bill last year; the garage property tax and the cottage land tax get this year's
  installments (some paid, one overdue). Extend tests.
- User guide EN + RU: sections for **Equalized payments** (plan, why actual differs, expected settlement, settlement
  bill) and **Property tax** (yearly amount, installments, due dates, pay whole year), help topics + anchor test.
  Remove these items from "Coming soon".

## Scope — do NOT
- No dashboard (T-022), no reminders (T-028), no automatic plan suggestions.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Sample data: cottage electricity shows the plan, the settlement page with months and the expected settlement;
   taxes show installments with statuses.
3. Owner can enter a yearly property tax with 4 installments and pay them separately or all at once.

## Manual test steps for the owner
1. Settings → Test data → Delete all data → reopen → Fill with sample data.
2. Cottage → electricity contract → **Equalized plan** card → **Show settlement** → month table, expected result.
3. Add a new plan version (amount change from next month) → the settlement and the next bill default follow it.
4. Bills → new bill for that contract → Total = plan; comparison with the plan.
5. Garage → property tax contract → **Enter yearly tax** 2027: 101,00, 4 installments → 4 bills with the due dates;
   **Pay whole year** → all paid. Enter 2027 again → asks to replace unpaid ones.
6. Phone / wide / dark theme / Russian texts; "?" opens the new sections.
