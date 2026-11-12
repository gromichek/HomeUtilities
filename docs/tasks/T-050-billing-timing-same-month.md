# T-050: Billing timing "in the service month" (the bill for November arrives in November)
Phase: 9 · Depends on: T-049 (#316) · Size: S · Owner 2026-11-12

## Why
`BillingTiming` has `InArrears` (bill for October arrives in November) and `InAdvance` (bill for December arrives in
November). The owner's house-maintenance supplier sends the bill **for the current month around the 10th of that month**.
With neither value the new-bill default period, the template drafts and the "bill expected" to-do/reminder are one month off.
Existing bills are already on the right periods (checked by the lead) — only future defaults change.

## Scope — do
1. **`BillingTiming.InServiceMonth = 2`** (new enum value; stored as int — no migration; check sync and backup round-trip
   the value, and that an older app version ignoring it is not a concern: all devices update together).
2. Every place that maps timing to a month offset uses one helper (e.g. `BillingTimingExtensions.ArrivalOffset`:
   InArrears +1, InAdvance −1, InServiceMonth 0) — `Bill.cs` (default period: current month), `BillDefaults` (default
   issue/due dates), `BillTemplate` / `BillTemplateScheduler` (draft for the current month, created on the template's day in
   that month), `TodoBuilder` ("Ожидается счёт" for the current month from `BillDay`), reminders that use it. No other
   behaviour change for the two existing values.
3. **UI**: the timing choice in the contract editor and in the bill template editor gets the third value, resx EN + RU:
   «В месяце услуги» / "In the service month" (keep «После периода», «Вперёд»), with the existing one-line hints extended:
   «счёт за ноябрь приходит в ноябре». Choice dialog returns the value (§3a).
4. `SampleDataGenerator`: one contract (e.g. house maintenance) with `InServiceMonth` and `BillDay` 10.
5. Guide EN + RU: contract field "Время выставления счёта" — three values with one example each; templates section if it
   explains timing.

## Not in this task
No arbitrary month offset, no change of existing bills, no change of charge calculation (it works on the bill period).

## Tests (concrete dates)
- Today 12 Nov 2026, `InServiceMonth` → new bill period 11.2026; `InArrears` → 10.2026; `InAdvance` → 12.2026 (unchanged).
- Template day 10, `InServiceMonth`: on 10 Nov a draft for 11.2026; on 9 Nov none.
- To-do "Ожидается счёт" with `BillDay` 10, `InServiceMonth`: from 10 Nov for 11.2026, gone when the bill exists.
- Sync / backup round trip of value 2.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist (choice field, Russian at 360 dp); guide
EN + RU.

## Manual test steps for the owner (short)
1. Contract «Обслуживание дома» → «Время выставления счёта» → «В месяце услуги», «День выставления счёта» 10 → ✓.
2. «Счета» → ＋ for this contract → the period is the current month.
3. Overview: «Ожидается счёт … за ноябрь» appears from the 10th until the bill is entered.
