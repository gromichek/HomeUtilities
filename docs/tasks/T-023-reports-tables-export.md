# T-023: Reports — costs, consumption, unit cost, calculated vs billed; tables and Excel export
Phase: 6 · Depends on: T-022 · Size: L

## Goal
The **Reports** menu item (placeholder today) answers the owner's questions over months and years: how much each
property costs, per category/service; how much water/electricity/heat was used; what one m³ / kWh / MWh really costs;
how far the bills are from the calculation (e.g. water losses). This task builds the report **queries and tables** with
**Excel export**; charts on top of the same data follow in T-024.

## Context
- `docs/DATA_MODEL.md` §11; Core: `BillSpread.ByMonth` (spreads bills with ServiceFrom/To over months), `BillComparison`,
  `ConsumptionCalculator` / `ChargeInputLoader`, categories (colour), properties, contracts, `PeriodFilter` (Bills /
  Payments), `IBackupFileService` (save dialog / share — generalise it for any file, keep backups working).
- Licence rule (CLAUDE.md): Excel via **ClosedXML** (MIT) — allowed as a new dependency for this task; name the version
  and licence in the PR. No EPPlus.

## Scope — do

### 1. Core report queries (`HomeUtilities.Core/Reports`, pure functions over loaded data, fully unit tested)
- **Costs**: rows = months (or years), columns = properties × (category | service kind | contract), value = gross
  (net / VAT optional). Bills with a service period are spread with `BillSpread`; one-off expenses included (category);
  equalized bills count as billed amounts; settlements in their month. Totals per row/column, grand total. Filters:
  period range (from/to month), properties (multi), categories (multi).
- **Consumption**: per meter resource/unit (cold water m³, hot water m³, electricity kWh day/night/total, heat MWh…),
  per property (and per meter as detail), per month — from readings via the same reading-selection rules as charges
  (contract reading windows); replacement chains summed; incomplete months marked (not zero).
- **Unit cost**: per contract per month = bill gross (or calculated gross if no bill, marked) ÷ consumption of the
  contract's metered quantity; also net excl. VAT variant. Not for fixed/area/resident-only contracts.
- **Calculated vs billed**: per contract per month: calculated (snapshot if stored, else current calculation), billed,
  difference €/%, and quantity difference (`ConsumptionOnBill` − metered) → water losses over time.
- **Year over year**: for costs and consumption: the same months of each year side by side.
- Tests with written-out numbers for each query (spread of a 3-month bill 30,00 → 10,00 × 3; replacement chain; month
  without readings incomplete; unit cost 46,02 / 12,150 m³ = 3,79 €/m³; YoY).

### 2. Reports screen (menu **Reports**)
- Report picker (tabs or a picker): Costs · Consumption · Unit cost · Calculated vs billed.
- Common filter bar: period (from–to months, presets "This year", "Last 12 months", "Last year", "All"), properties
  (multi-select, default all), grouping (Costs: by category / by service / by contract / by property; rows: months /
  years), Year over year switch.
- Table view: frozen first column (period) on wide layouts; on phones a scrollable table (horizontal scroll inside the
  table area only — no page-wide horizontal scroll) or a row-per-period card list with the values; totals row; cells
  with incomplete data marked (e.g. "≈" + muted) with a legend; tap a cell → the underlying bills (Bills screen filtered
  to property/month/category) or readings.
- Performance: loads through a Data loader (`ReportDataLoader`) once per filter change; works with 10 years × 3
  properties × 15 contracts (test with generated data, no N+1).

### 3. Export
- **Export to Excel** (ClosedXML): the current report as shown (filters in a header block, one sheet; numbers as
  numbers with 2/3 decimals, dates as dates, totals with formulas or values — values are fine), file name
  `HomeUtilities-<report>-<from>-<to>.xlsx`; save via the generalised file service (Windows save dialog, Android
  save dialog with share fallback — like backups). Also **CSV** (UTF-8 with BOM, `;` separator for Latvian/Russian
  Excel) as a lightweight alternative.
- Unit tests: workbook opened back with ClosedXML contains the expected cells.

### 4. Sample data, help, guide
- Sample data covers ≥ 13 months for all reports (already mostly true); add a test running every report over it.
- `HelpTopic.Reports` + "?"; user guide EN + RU: section **Reports** (each report, what "≈ incomplete" means, unit cost,
  water losses, YoY, export to Excel/CSV, where files go). Remove "reports" from "Coming soon" (charts stay there).

## Scope — do NOT
- No charts (T-024), no PDF export, no saved/custom report definitions.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; ClosedXML in `Directory.Packages.props` with
   licence named in the PR.
2. Sample data: each report shows plausible numbers; totals equal the Bills screen totals for the same filters (test).
3. Excel file opens in Excel/LibreOffice with the same numbers as the screen.

## Manual test steps for the owner
1. Settings → Test data → Delete all data → reopen → Fill with sample data.
2. Reports → Costs → This year, all properties, by category → totals equal Bills (year filter) totals.
3. Consumption → water of the flat per month; the replacement month is not a dip; a month without readings shows "≈".
4. Unit cost → water €/m³ per month; Calculated vs billed → water losses line.
5. Year over year on Costs. Tap a cell → the bills behind it.
6. Export to Excel → open the file on Windows; CSV on Android → share/save. Phone / wide / dark theme / Russian texts.
