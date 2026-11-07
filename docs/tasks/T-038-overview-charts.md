# T-038: Overview charts — this year by month, spending by category, consumption trends (+ two small fixes)
Phase: 9 · Depends on: T-036c (#263) · Size: M–L

Owner (2026-11-07): the guide promises charts on Overview ("Графики на этом экране появятся позже"). He chose all three
variants of the lead's mock-up: **A** this year by month vs last year, **B** spending by category, **C** consumption of the
last 12 months. Reuse what exists: the report queries in `HomeUtilities.Core/Reports` (`CostsReport`, `ConsumptionReport`,
`ReportBuilder`), `ChartModel` / `ReportChartBuilder` (ViewModels) and `ChartDrawable` / `ReportChartsView` (App, drawn with
`Microsoft.Maui.Graphics` — **no new chart package**).

## Scope — do

### A. "Этот год" card: months of this year next to last year
- Under the existing year line (keep it as it is): grouped bars per month Jan–Dec, this year in the accent colour, last
  year muted (legend "2026 · 2025"). Same basis as the year line: bills with VAT by bill month. Future months of this year
  empty; months with incomplete data drawn muted as in Reports.
- No previous-year data → only this year's bars, no empty "2025" legend (same rule as C-05).
- Tap on a month → **Bills** filtered to that month (and the Overview property filter).

### B. New card "Расходы за {год} по категориям"
- Year to date (January – current month), bills with VAT, grouped by the bill's category, sorted by amount; up to 5
  categories, the rest as "Прочее". One stacked horizontal bar in the **category colours** + a row per category with the
  amount (and the share in % if it fits). Subtitle "январь – октябрь, всего 1 002,00 €".
- Tap on a category row → **Bills** with this category and year filter.
- No bills this year → the card shows one short line ("Счетов за этот год пока нет."), no empty chart.

### C. New card "Потребление"
- One row per **service kind with meters** of the filtered properties (cold water, hot water, electricity day/night or
  total, gas, heat…, as `ConsumptionReport` groups them): name, consumption of the **last complete month** with unit (NBSP,
  C-18 helper) and the change against the average of the previous 11 months ("+8 %"), and a small line of the last
  12 months (sparkline, same colours as in Reports).
- A noticeable rise (e.g. > 30 % above the average — constant in one place, unit-tested) is marked with the warning
  colour + icon; it is a hint, not an alarm.
- Months without readings → gaps / muted (no invented zeros); fewer than 3 months of data → only the number, no line.
- Tap on a row → **Reports** "Потребление" for this service and period.
- No meters / readings → the card is hidden.

### Common rules
- All three follow the **Объект** filter on top of Overview, both themes (colours via resources), phone one column,
  wide window right column (§4); charts are read-only, the tap targets ≥ 48 dp rows.
- Data loads **off the UI thread** in the existing Overview load (`RunLoadAsync`, `ReloadGate` reload on data change only);
  one query pass per card, no per-month queries in a loop. Measure with the `Performance:` log — Overview must not become
  noticeably slower (state before/after in the PR).
- Accessibility: each chart has a text summary (`SemanticProperties.Description`), e.g. "Октябрь 2026: 95 €, 2025: 90 €".
- Guide EN + RU: replace "Графики на этом экране появятся позже" with the three charts (what they show, what a tap does,
  the consumption hint). Move nothing else.

### Two small items collected 2026-11-07
1. **Readings: keep "Передать онлайн" visible after a submit** — opening the supplier's page does not always mean the
   readings were submitted. The button stays; the "submitted" mark (date) is shown next to it, as before.
2. **Remove `Platforms/Windows/DatePickerLanguage.cs`** and its registration (T-036c optional item; it does not change the
   calendar language on the owner's PC; owner: leave the calendar English).

## Not in this task
No new report types, no chart settings, no export from Overview.

## Tests
Concrete numbers: A — two years of bills → 12 + 12 values, future months empty, no previous year → one series; B — top 5 +
"Прочее", sorting, subtitle text, empty year; C — last complete month, average of 11 months, change in %, the rise
threshold, gaps without readings, < 3 months → no line; filter by property; texts EN/RU.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist of CLAUDE.md; Performance lines before /
after in the PR. Owner: three charts on Overview on phone and Windows, taps open the right screens, both themes.

## Manual test steps for the owner (short)
Overview with all properties and with one property; tap a month bar, a category row, a consumption row; dark theme;
phone portrait; Readings → "Передать онлайн" stays after a submit.
