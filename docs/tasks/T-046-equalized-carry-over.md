# T-046: Equalized payments — carry-over from the previous period (Elektrum-style settlements)
Phase: 9 · Depends on: T-045 (#304) · Size: S–M · Owner 2026-11-11 (lead's analysis of 19 real supplier bills; no
personal data in this file — the numbers below are invented but follow the same rule)

## Why
Some suppliers never send a settlement bill. Their equalized year runs e.g. **July – June**; the difference of a year
(actual cost − forecast) is divided by 12 and **added to the monthly payment of the next year** as a printed line
"difference from the previous period" (positive = the customer still owes, negative = refund). The amount to pay
(*Apmaksai*) therefore already contains this carry-over.

The app computes the expected settlement as Σ (actual − billed) since the last `Settlement` bill
(`EqualizedSettlement.Calculate`). With such a supplier "billed" contains the carry-over, so the refund of last year's
overpayment is counted as a new underpayment: with a carry-over of −4.00 €/month the expected surcharge grows by 4.00 €
every month although nothing is owed. The owner checked: the supplier's yearly difference equals
Σ (bill total − amount to pay) = Σ (actual − (amount to pay − carry-over)) over the year, to the cent (rounding).

## Scope — do
1. **`EqualizedPlan.CarryOverMonthly`** (`decimal?`, gross, signed as printed by the supplier; null = none). EF migration,
   sync (the plan's sync payload and snapshot), backup restore of older backups (null). `SampleDataGenerator` gets one plan
   version with a carry-over.
2. **Plan editor** (the plan version dialog of the contract's "План равномерных платежей" card): optional field
   **«Перенос с прошлого периода, € в месяц»** with a one-line hint «Строка поставщика „разница с прошлого периода“, со
   знаком: минус — возврат переплаты, плюс — доплата. Уже входит в месячную сумму.» (EN + RU). The plan row shows it when
   set, e.g. «с 07.2026 — 30,00 € (в т. ч. перенос −4,00 €)».
3. **Calculation** (`EqualizedSettlement`): for a month covered by a plan version with a carry-over, the month's billed
   amount counts **without** the carry-over: `difference = actual − (billed − carryOver)`; the month row shows the
   carry-over (new column or a second line «перенос −4,00 €» — whatever fits §3/§4 on the phone). Totals and the expected
   settlement follow. Months without a plan or without a carry-over: unchanged.
4. **Period boundary without a settlement bill.** A bill of kind *Перерасчёт (годовой)* with **Итого 0,00 €** must be
   accepted and start the next period (check `BillValidator` and the bill editor; no "amount required" error, no
   reminder, balance unchanged). If something prevents it, fix that only.
5. **Overview / Charges lines** that show the expected settlement (`ChargesEqualizedLine`, Overview "Ожидаемый перерасчёт")
   use the same corrected value (they call the same service — verify).
6. **User guide EN + RU**, section 16 "Равномерные платежи": a short subsection «Поставщик без счёта-перерасчёта (разница
   переносится в платежи следующего года)» — set the plan with the amount to pay and the carry-over, enter a
   *Перерасчёт (годовой)* with 0,00 € for the last month of each equalized year (e.g. 06.2026), enter the monthly bills
   with the amount to pay as before. Invented example with numbers: forecast 34,00, carry-over −4,00 → amount to pay
   30,00; actual 36,50 → difference 36,50 − (30,00 − (−4,00)) = +2,50.

## Not in this task
No automatic detection of the equalized year, no import of the carry-over from PDFs, no change for suppliers that send a
settlement bill (they keep working as today).

## Tests (concrete numbers)
- Month: plan 30,00 incl. carry-over −4,00, billed 30,00, actual 36,50 → difference +2,50 (old rule: +6,50).
- Year: 12 months as above with actual 33,00 / 35,00 / … → expected settlement = Σ(actual) − Σ(billed + 4,00).
- Carry-over +1,50 (owing): billed 40,00, actual 37,00 → difference 37,00 − 38,50 = −1,50.
- Plan version change in the middle of the period with different carry-overs; month without carry-over unchanged.
- Settlement bill 0,00 € starts the next period; balance unchanged; no reminder.
- Migration / sync round trip of `CarryOverMonthly`; old backup restores with null.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist (plan dialog field, month rows on the
phone at 360 dp in Russian); guide EN + RU.

## Manual test steps for the owner (short)
The lead tells the owner what to enter in his contract (plans, carry-overs, 0 € settlements). Then:
1. Contract → «План равномерных платежей» → the rows show the carry-over.
2. «Показать расчёт» → the months since 07.2026 show the carry-over; the expected settlement equals the lead's figure.
3. Overview → «Ожидаемый перерасчёт» shows the same value.
