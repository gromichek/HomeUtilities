# T-052: Overview "Деньги" — sections per contract (to pay, overpayment, expected settlement), no netting across contracts
Phase: 9 · Depends on: T-051 (#322) · Size: M · Owner 2026-11-12 (layout agreed on the lead's mock-up)

## Why
`OverviewMoney.PerProperty` shows per property: overdue, prepayment (unallocated payments) and the **net balance of all
contracts** (debt / paid in advance / settled). Debts and overpayments of **different** contracts cancel each other out:
a 10 € debt to the electricity supplier and 10 € unallocated on gas show "Расчёты закрыты"; with no open bills the
"Предоплата X" and "Оплачено вперёд X" lines repeat the same amount. Different suppliers never offset each other.

## Layout (per property block, as today one block per property)
```
Деньги
Моя квартира
К оплате                         37,40 €      ← section header with the section total
  Газ                    12,25 €  ›           ← one row per contract
  просрочено с 05.11  (danger colour)
  Обслуживание дома      25,15 €  ›
  до 25.11
Переплата                        13,25 €
  Электричество          13,25 €  ›
Ожидаемый перерасчёт                          ← no total (refund and surcharge of different suppliers)
  Газ                    −9,32 €  ›
  возврат
  Электричество          +2,87 €  ›
  доплата
```
1. **Sections in this order: К оплате → Переплата → Ожидаемый перерасчёт.** An empty section is not shown (no "nothing to
   pay" line). All three empty → one line «Расчёты закрыты — долгов и переплат нет.» (replaces `OverviewMoneyAllPaid`; keep
   `OverviewMoneyEmpty` for "no bills and no payments at all").
2. **К оплате**: per contract the sum of the open (unpaid) remainders of its non-draft bills. Second line: «просрочено с
   {дата}» in the danger colour when its oldest open bill is past due (date = that bill's due date), otherwise «до {дата}» =
   the nearest due date, or nothing when no open bill has a due date. Overdue contracts first (oldest first), then by due
   date, then by name. Header total = sum of the rows.
3. **Переплата**: per contract the amount of its payments not allocated to bills (what today is "prepayment", per
   contract). A payment without a contract → row «Без договора». Sorted by amount. Header total = sum.
4. **No netting**: a contract with both an open bill and an unallocated payment appears in **both** sections (the user should
   allocate it — that is visible on Payments). No property-level net balance line any more (`ContractBalance…` texts on the
   contract editor stay as they are — this task is the Overview card only).
5. **Ожидаемый перерасчёт**: one row per equalized contract (today's `Settlements`), amount signed, second line «возврат» /
   «доплата» (word, not only colour). No header total.
6. **More than 5 rows in a section**: first 5 + a row «ещё {N}» that opens the list (К оплате → Bills filtered "unpaid"
   for the property; Переплата → Payments with unallocated; Перерасчёт → Contracts or the first settlement screen — choose
   the simplest existing target and say which).
7. Taps: К оплате row → Bills of this contract, filter unpaid; Переплата row → Payments of this contract (or the payment if
   only one); Перерасчёт row → the contract's settlement screen (as today).
8. The separate «Просрочено …» line disappears (overdue is in the row). Several currencies: amounts per currency as today
   (rows show their currency; a section total only when all rows share one currency, otherwise no total).
9. Phone (360 dp, Russian) and wide window: rows wrap (name left, amount right, second line under the name), touch targets
   ≥ 48 dp, both themes, colours via resources. Accessibility description lists the sections.
10. Guide EN + RU, Overview → Деньги: the three sections, "debts and overpayments of different contracts are not offset",
    the example from above with invented numbers.

## Not in this task
No change of the contract editor balance card, of Payments/Bills screens or of the allocation logic.

## Tests (Core: the model; ViewModels: texts and visibility)
- Gas overdue 12,25 (due 05.11), house 25,15 (due 25.11), electricity unallocated 13,25, equalized gas −9,32, electricity
  +2,87 → sections and rows exactly as in the layout, totals 37,40 / 13,25, order.
- Only overpayment 22,24 on gas → no "К оплате" section; Переплата 22,24; settlements shown.
- Debt 10 on electricity + unallocated 10 on gas → both sections (no "settled").
- Everything closed → the single "Расчёты закрыты…" line; nothing at all → `OverviewMoneyEmpty`.
- 7 contracts to pay → 5 rows + «ещё 2»; payment without contract → «Без договора»; two currencies → no section total.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist; guide EN + RU.

## Manual test steps for the owner (short)
1. Overview → Деньги: «Переплата 22,24 € · Газ» and the two settlement rows, no duplicate lines.
2. Enter the November house-maintenance bill without paying → «К оплате 35,10 €» with «Обслуживание дома … до …»; the gas
   overpayment stays as its own section.
3. Tap each row → the right screen.
