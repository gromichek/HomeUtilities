# T-053: User guide — bill kinds and the gaps found in daily use (EN + RU)
Phase: 9 · Depends on: #331 · Size: S–M · Docs only (no app code) · Owner 2026-11-13

## Why
The owner re-read the guide: the bill kind **«Корректировка»** is not explained anywhere ("not clear how it works and what
it is for"), and other places are missing too. Most gaps are things the lead explained in chat in the last days; they
belong in the guide. Facts only from the code, `docs/DATA_MODEL.md` and the existing guide; examples invented.

## Scope — do
1. **Section 13 Bills → new subsection "Виды счетов"** (table, one row per kind, exact names from `Enum_BillKind_*`):
   Обычный счёт, Платёж по плану, Перерасчёт (годовой), Корректировка, Взнос по налогу, Разовый расход. Columns: what it is
   for · how to fill it (period = which month) · where it counts: **expenses by month / Overview / categories** (all kinds),
   **"Расчёт и счета"** (Regular, Equalized, Settlement — not Correction; check `CalculatedVsBilledReport`), **equalized
   settlement** (Equalized billed; Settlement starts the next period, 0,00 € allowed), **"Что сделать" / expected bill**
   (Correction and Settlement are not "the bill of the month" — `TodoBuilder`), **unit cost report** (Regular + Correction).
   Verify each cell in the code and say in the PR where you looked.
2. **Example "Поставщик ошибся в прошлом счёте"** (invented numbers): last month billed 20 € less, this month 20 € more →
   last month's bill stays as issued; this month = an ordinary bill with the month's amounts + a **Корректировка** with
   period = last month, +20,00 €, same bill number; one payment covers both (distribution in the payment editor). What the
   reports then show (month totals right, "Расчёт и счета" keeps the old −20 as history). The alternative "one bill with
   +20" and its drawback in one sentence.
3. **"Количество в счёте"** (`BillConsumptionLabel`): what it is for — the report "Расчёт и счета → Разница в количестве
   (потери)" = Количество в счёте − measured; without it the month is empty; the loss lines of the tariff are not used for
   that report. Put it in "Ввод счёта" and in the report section.
4. **Contract with several meters and different prices per meter** (e.g. one electricity contract for a flat and a summer
   house): the tariff line's **«Что считается» → Счётчики** limits a line to some meters; no meter selected = all meters;
   add a meter to a contract from a date (link dates in the meter's "Договоры" card, tap the row). Short example.
5. **Payment distribution**: one payment for several open bills of a contract (how the editor distributes, how to change it).
6. **Problems and answers** — new entries:
   - "Ожидаемая сумма не совпадает со счётом": check the price date (1st of the service month), the reading window (which
     readings belong to the month), missing readings, the bill's *snapshot* (→ «Обновить снимок»), amounts typed from the
     bill;
   - "Показание попало не в тот месяц": the reading window rule with an example (readings on the 1st → window 28 – 3);
   - "Сравнение с прошлым годом выглядит странно": the per-contract same-months rule (T-051).
7. **Consistency pass** over everything added since T-040 (T-041…T-052, #327, #329, #331): wording matches resx, section
   numbers and help anchors unchanged (or `HelpTopic` updated + tests), EN = RU.

## Not in this task
No app changes; no screenshots.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green (help-topic tests), CI green. PR lists the added subsections and, for item 1,
the code places checked.

## Manual test steps for the owner (short)
Help → 13 Счета → "Виды счетов" and the correction example; 20 Проблемы и ответы → the three new entries.
