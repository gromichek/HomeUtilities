# T-024: Charts for the reports; category filter on Bills
Phase: 6 · Depends on: T-023 · Size: M

## Goal
The report tables of T-023 get charts: monthly costs as bars grouped by property or category, consumption and unit
cost as lines, year over year as side-by-side bars/lines, calculated vs billed difference as bars around zero. And the
Bills screen gets the category filter the Costs drill-down needs (open question of T-023).

## Context
- `ReportTable` / `ReportSheet` (Core, T-023), `ReportsViewModel`, `ReportTableViewModel`, category colours.
- Licence rule (CLAUDE.md): **LiveCharts2** (`LiveChartsCore.SkiaSharpView.Maui`, MIT; SkiaSharp MIT) is the preferred
  library; use the newest version that is at least two weeks old — if only a prerelease (rc) exists for .NET 10 MAUI,
  it is acceptable, name it in the PR. Fallback if it does not build for both targets: draw simple bar/line charts
  with `Microsoft.Maui.Graphics` (`GraphicsView` + `IDrawable`) — no other chart packages. Check all transitive
  licences and list them in the PR.

## Scope — do
1. **Chart model in ViewModels** (`ReportChartBuilder`): from a `ReportTable` build series (name, colour, points) — no
   chart-library types in ViewModels (map them in App), so it is unit tested:
   - Costs: stacked or grouped bars per month (series = the grouping: category colours / property / service / contract);
     totals line optional.
   - Consumption: one line per resource/unit (separate axes not needed — one chart per unit: m³, kWh, MWh).
   - Unit cost: line per contract (€/unit), one chart per unit.
   - Calculated vs billed: bars of the difference per month (positive/negative), losses line (m³) as a second chart.
   - Year over year: one series per year over the months 1–12.
   - Incomplete (≈) points: drawn in a muted/dashed style or with a marker; legend explains it.
   - Max ~8 series in a chart; the rest summed as "Other" (localized).
2. **Reports screen**: switch **Table / Chart** (remembered); chart above or instead of the table on wide layouts
   (choose: chart top, table below on expanded; toggle on phones). Legend with category colours; tooltips/tap on a
   point shows period + value; dark theme colours readable; axis labels localized (month names short), money with €.
3. **Bills category filter**: Category (All + categories with colour dot) in the Bills filter bar, remembered with the
   other filters; the Costs drill-down (cell tap) now filters by category (instead of only highlighting) when the
   report is grouped by category. Tests.
4. Help/guide EN + RU: Reports section gets "Charts" (how to read each chart, ≈ points, Other); Bills section the
   category filter. Remove "charts" from "Coming soon".

## Scope — do NOT
- No chart export as image/PDF (later if wanted), no dashboard charts on Overview.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green on both targets with the chart package.
2. Sample data: every report shows a chart consistent with its table (same numbers, tested on the builder level).
3. Bills: category filter works; Costs cell → Bills filtered by property, month and category.

## Manual test steps for the owner
1. Fill with sample data → Reports → Costs → **Chart**: bars per month, colours = categories, legend; tap a bar →
   value.
2. Consumption (water m³, electricity kWh), Unit cost, Calculated vs billed (difference around zero), Year over year.
3. Phone portrait/landscape, tablet, wide window; dark theme; Russian month names.
4. Bills → category filter; Reports Costs by category → tap a cell → Bills with that category, property and month.
