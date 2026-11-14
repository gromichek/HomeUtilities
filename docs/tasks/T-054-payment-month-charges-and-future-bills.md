# T-054: Charges by month of payment; Overview counts bills already entered for later months of this year
Phase: 9 · Depends on: T-053 (#333) · Size: M · Owner 2026-11-14 · **Regression-sensitive: the owner asked explicitly that
nothing that works now breaks — every entry point listed below gets a test.**

## Why
1. **Charges.** The screen shows one month for all contracts of a property and calculates each contract for **that** month;
   the default is the previous month until a reading window of the month starts (`ChargePeriodDefaults`). The owner opens
   Charges to see **what he pays this month**: in November that is October for contracts billed after the period
   (`InArrears`), November for `InServiceMonth`, December for `InAdvance`. Today he sees October for all of them, the house
   maintenance and internet bills he is paying now are on other months.
2. **Overview.** "Этот год", the monthly bars and the category card count bills only up to the **current month**
   (`OverviewMoney.YearToDate`: `Period.Month <= today.Month`, `OverviewCharts.MonthlyComparison` / `SpendingByCategory`).
   An internet bill for 12.2026 entered and paid in November is invisible until December.

## Scope — do

### A. Charges: the screen month is the **month of payment**
1. Each contract on the screen month `P` is calculated for its **service month** `S = P − ArrivalOffset(contract.BillingTiming)`
   (`BillingTimingExtensions.ArrivalOffset`: InArrears +1 → S = P − 1; InServiceMonth 0 → S = P; InAdvance −1 → S = P + 1).
   `LoadManyAsync` gets `(contractId, S)` per contract; the bill shown on a card is the contract's bill with `Period == S`
   (first entered, as today); `IsActive` checks use `S`.
2. **Default screen month = the current month** (remove `ChargePeriodDefaults` or keep it unused — say which and update its
   tests). Arrears contracts at the very start of the month may show "readings missing" until the window closes — expected.
3. **Texts**: the month header reads e.g. «Ноябрь 2026 · к оплате» / "November 2026 · to pay"; every card shows its service
   month in its subtitle («за октябрь 2026» / "for October 2026"); the totals label says it is the amount to pay in the
   screen month. Resx EN + RU.
4. **Everything on a card uses the card's service month `S`, never the screen month**: «Новый счёт» (`EnterBillAsync` →
   `PeriodParameter = S`), open bill, open readings, open tariff component, open property/contract links, the equalized
   lines, the "readings missing" / "no rate" warnings and their dates.
5. **Entry points** (convert service month → payment month when a month is passed):
   - main menu → current month (default);
   - contract editor «Начисления» (`ContractEditViewModel.ShowChargesAsync`) → current month, scrolled to the contract;
   - bill editor link to the charges (`BillEditViewModel.Parts` → `MonthParameter = Period`): the bill's period is the
     **service** month → open payment month `P = Period + ArrivalOffset(contract)` with the contract focused, so the same
     bill's card is shown;
   - property switch on the screen → current month (as today "the default");
   - ‹ › and the month picker move the payment month.
   List every other caller you find (`rg "Routes.Charges"`), and handle it the same way.
6. Contract timing changed later: past screen months use the **current** timing (documented limitation, guide one line).

### B. Overview: bills already entered for later months of this year
1. `YearToDate`: this year = the contract's bills of the whole current year (no current-month cap), per contract to its own
   last month (T-051 rule unchanged otherwise) — an internet bill for 12.2026 makes that contract's months Jan–Dec and last
   year's internet Jan–Dec is compared.
2. `MonthlyComparison`: a later month of this year that already has counted bills shows its bar (muted/approximate like the
   current month); later months without bills stay empty.
3. `SpendingByCategory`: all counted bills of the current year (subtitle «с начала года» unchanged).
4. Drafts still excluded; nothing beyond 31 December.

### C. Guide EN + RU
Charges section: "the screen shows what you pay in the chosen month: per contract the month given by «Время выставления
счёта»", the example table (after the period / in the service month / in advance), the card subtitle, the limitation of a
changed timing. Overview: bills for later months of this year already count.

## Not in this task
No change of the calculation itself, of reports, bills, payments, equalized settlement, reminders or the Overview money card.

## Tests (concrete dates, today 14 Nov 2026)
- Charges P = 11.2026: arrears contract → S = 10.2026, service-month contract → 11.2026, advance contract → 12.2026; each
  card's bill = the bill with that period; totals = sum of the three cards.
- Default month = 11.2026 (also on 1 Nov, when an arrears window is still open).
- «Новый счёт» on the advance card → period 12.2026; on the arrears card → 10.2026.
- Bill editor link from an advance bill of 12.2026 → Charges P = 11.2026 with that contract focused and its card showing
  the 12.2026 bill; from an arrears bill of 10.2026 → P = 11.2026.
- Contract editor «Начисления» → P = current month, contract focused.
- Readings / tariff / bill links from a card use S.
- ‹ › from 11.2026 → 10.2026 shows arrears 09, service 10, advance 11.
- Overview: internet bill 12.2026 entered → in the year total, in the December bar (approximate), in the category card;
  last year's internet compared Jan–Dec; other contracts unchanged; draft for 12.2026 → not counted.
- Existing Charges and Overview tests: adapt only where the month rule changed, say which in the PR.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist (Charges header and card subtitles at
360 dp in Russian, both themes); guide EN + RU. PR lists every entry point checked.

## Manual test steps for the owner (short)
1. «Начисления» (menu): «Ноябрь 2026 · к оплате»; water/heat/gas/electricity «за октябрь», house maintenance «за ноябрь»,
   internet «за декабрь» with the December bill; the total = what is paid in November.
2. On the internet card «Новый счёт» → the period is 12.2026 (cancel).
3. Open a bill (e.g. heating for October) → its link to the charges → November screen, the heating card shows that bill.
4. ‹ to October: arrears «за сентябрь», house «за октябрь», internet «за ноябрь».
5. Overview: «Этот год» and the categories include the internet bill for December; a December bar appears (muted).
