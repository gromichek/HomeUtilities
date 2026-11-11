# T-048: Overview — "Spending by category" shows every category (no "Other")
Phase: 9 · Depends on: T-047 (#310) · Size: S · Owner 2026-11-11

## Why
`OverviewCharts.SpendingByCategory` keeps the 5 largest categories (`MaxCategories = 5`) and sums the rest into "Прочее".
A tap on "Прочее" opens the bills of the year without a category filter, so the owner could not tell what the amount
consists of. He has 7–10 categories in a year (incl. property tax installments and insurance); owner decision: show all.

## Scope — do
1. `SpendingByCategory`: one slice per category with bills in the period (year to date, as today), sorted by amount (then
   as today). **No "Other" slice** for known categories. Bills whose category is missing/deleted keep one slice "Без
   категории" (existing resx text if there is one, otherwise new EN + RU) so the total still adds up; its tap opens the bills
   of the year (as today for "Other"). Remove `MaxCategories` (or leave it unused — say which) and the "Other" text if unused.
2. Stacked bar and rows unchanged otherwise: every category its colour, a tiny share still drawn (minimum visible width
   of 2 dp if a slice would round to 0, so it is never invisible), the row shows amount and share («1 %», and «< 1 %» for a
   share below 0.5 %). Tap on a row → Bills with this category and year (unchanged).
3. Accessibility summary (`SemanticProperties.Description`) lists all categories.
4. Guide EN + RU, Overview section: remove the "up to 5, the rest as Other" sentence; "all categories with bills this year".

## Not in this task
No threshold setting, no change of the other cards.

## Tests
7 categories → 7 slices sorted by amount, totals equal the sum; a deleted category → "Без категории" slice; share texts
(1 %, < 1 %); minimum width rule in the drawable's geometry helper if testable; property filter.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist (card on the phone at 360 dp in Russian,
both themes); guide EN + RU.

## Manual test steps for the owner (short)
Overview → «Расходы за 2026 по категориям»: all categories listed, no «Прочее»; tap the smallest one → its bills.
