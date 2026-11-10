# T-044: Changing a contract's category also changes its existing bills (on request) + deterministic row-command tests
Phase: 9 · Depends on: T-043 (#293) · Size: S–M

Owner (2026-11-10): the Overview card "Расходы … по категориям" and the reports group by the **bill's** category, which is
copied from the contract when a bill is created. The owner has entered almost all his data and now wants own categories
(e.g. "Отопление и горячая вода" for a contract with service kind *Heating*). Changing the contract's category today leaves
all existing bills (and templates) in the old category — he would have to open every bill.

## Scope — do

### A. Offer to change the existing records
1. Contract editor, **Save** of an existing contract whose category changed (old → new): before saving, one question
   (standard dialog, UI_GUIDELINES §3 — not a delete, so a question is fine), only if there is something to change:
   «Категория договора меняется с «Вода» на «Отопление и горячая вода». Поменять её и у существующих записей этого договора:
   счета: 42, шаблоны: 1?» — buttons **«Поменять»** (primary) / **«Только договор»**; Back / close = cancel the save (nothing
   saved, editor stays). Counts by kind, only kinds with a count, wording EN + RU in resx.
2. "Existing records of this contract" = not deleted **bills** (incl. drafts) with `ContractId` = this contract **and**
   `CategoryId` = the old category, and **bill templates** of this contract with the old category. A bill or template the
   user deliberately put into another category is left alone (say so in the dialog text only if it is short; otherwise in the
   guide). **Bill lines** and **tariff components** with their own category are not changed (their category is explicit;
   empty = follows the bill / contract anyway).
3. The contract and the changed records are saved **in one transaction** (store / service method in Data, `UpdatedAt` set so
   sync carries the change); afterwards the usual saved message, e.g. «Сохранено · категория изменена у 42 счетов и 1 шаблона».
   No Undo needed (the user can change the category back the same way) — say this in the guide.
4. Works the same on Windows and Android; the counting and the change off the UI thread like the other store calls of the
   contract editor today (no change of how the editor loads — owner decision 2026-11-09).
5. User guide EN + RU: section 10 "Категории" — category vs. service kind (one line: the Overview and reports group by the
   bill's category; the service kind does not change that) and how to regroup a contract; section 7 "Договоры" — the question
   on save. Use the exact resx wording.

### B. Tech debt: row-command tests without `Task.Delay`
T-043 listed tests that `Execute` a row command and wait with `Task.Delay(50)` because the rows wrap `_ = …Async()`
(Overview to-do / Money rows ×8, Overview chart category / consumption rows ×2). Give these row view models an awaitable
action (`Func<Task>` / `IAsyncRelayCommand`) so the tests can await it; the UI keeps executing the command as today (no
behaviour change). Other `Delay(50)` tests (Charges, editors, Help, Reminder, Sync) only if the same small change fixes them.

## Not in this task
No bulk category editor for arbitrary bills, no change of `ServiceKind`, no category on payments beyond what exists.

## Tests
- Data: contract category change with "change records" → contract, 3 of 3 matching bills (incl. a draft) and 1 template
  changed in one save, a bill of the same contract with a third category unchanged, a deleted bill unchanged, bill line
  categories unchanged, other contracts unchanged; "only contract" → only the contract.
- ViewModels: no question when the category did not change or nothing matches; counts text "счета: 42, шаблоны: 1" EN/RU
  (only kinds with a count); cancel → nothing saved.
- B: the listed tests await the row action; no `Task.Delay` left in them; run 20× locally.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist of CLAUDE.md (the dialog: texts wrap in
Russian at 360 dp, buttons stacked full width if they do not fit); guide EN + RU.

## Manual test steps for the owner (short)
1. Settings → Categories → add «Отопление и горячая вода».
2. Open the heating contract → Category → the new one → ✓ → the question lists the bills (and template) → «Поменять».
3. Overview → "Расходы … по категориям" shows the new category; Bills filtered by the new category show the old bills.
4. Another contract: change the category → «Только договор» → its old bills keep the old category.
