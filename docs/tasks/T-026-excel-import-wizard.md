# T-026: Excel import wizard — sheet → contract, column mapping, preview, dry run, undo
Phase: 7 · Depends on: T-025 · Size: L

## Goal
The owner has years of history in his own Excel workbook (one sheet per supplier, one row per month). It is private and
must **never** be in the repository, so the import cannot be hard-coded against it. Instead the app gets a general
**import wizard**: the owner picks a sheet, tells which column means what, sees a preview and a dry run, imports, and
can **undo a whole import**. The mapping is saved as a reusable **import profile**, so a sheet can be re-imported after
fixes. (T-027 is then the guided import of his workbook, with fixes found on the way.)

## Context
- `docs/DATA_MODEL.md` §12 (patterns in the owner's workbook — read carefully), §4 readings, §6 bills/payments,
  `docs/PAYMENT_TYPES.md` ("Legacy Excel: `***` → payment = bill total …").
- `HomeUtilities.Import` project exists (ClosedXML, T-023). Stores, validators, `Amounts.Complete`, `MeterReading`
  kinds, `PaymentRules`, LVL currency, file picker (`IBackupFileService.PickAsync` style).
- Privacy (CLAUDE.md): tests use a **synthetic** workbook built in code or `tests/**/Fixtures/*.xlsx` generated from
  invented data, shaped like §12.

## Scope — do
### 1. Import model (Core / Import)
- `ImportProfile` (JSON-serialisable): sheet name, header row, first data row, last data row (optional, else first
  empty period), target contract id, column mappings: **Period** (date or month cell; formats: date, "MM.yyyy",
  "yyyy-MM", text month names RU/LV/EN) — required; per **meter register** a reading column (+ optional reading date
  column); **Bill total**; optional **Net**, **VAT %**, **VAT**; **Paid marker** column + marker text (default `***`)
  + optional **Paid date** column; **Quantity on bill**; **Notes** columns (several, concatenated with the column
  header as prefix); **Currency rule**: EUR / LVL / "LVL before 2014-01, EUR after" (default the latter).
  Stored in a new `ImportProfiles` table (SyncEntity, JSON column) — migration.
- `SheetReader` (ClosedXML): reads cells as typed values (numbers, dates, text) with the formatted text; handles merged
  header cells, empty rows, formulas (use cached values).
- `ImportPlanner.Plan(profile, rows, existing data)` → per row: what will be created (reading per register, bill,
  payment + allocation, notes) or skipped, with **issues** (error = row skipped, warning = imported with a note):
  unparsable period/number, reading lower than previous without dial digits, duplicate period (a bill of that contract
  and period exists → skip, warning), reading already exists on that date → skip, total missing but readings present →
  readings only, amounts inconsistent (Net + VAT ≠ Total ± 0,02) → import Total, warning. Numbers accept `,` and `.`.
- `ImportBatch : SyncEntity { ProfileId, FileName, ImportedAt, Counts, Notes }` and `ImportBatchId` (Guid?) on
  `Bill`, `Payment`, `MeterReading` (migration). Readings imported as `Regular` (or `Initial` for the very first of a
  meter if the meter has none), bills as `Regular` (Equalized for equalized contracts), payments with
  `Reference = "imported"` and `Notes`.
- **Undo import**: soft-deletes everything of the batch (bills, their allocations, payments, readings) in one
  transaction; refused for items changed after the import (`UpdatedAt > ImportedAt`) unless the user confirms
  "Some imported records were edited later. Undo anyway?".

### 2. Wizard (Settings → **Import from Excel**)
1. Pick file (.xlsx) → list of sheets with row counts.
2. Pick a sheet → choose an existing profile for it or new; target **property → contract** (meters of the contract are
   listed for step 3).
3. **Map columns**: table preview of the first ~15 rows with column letters and header texts; for each target field
   a picker of columns (with the header text), header/first data row numbers, marker text, currency rule. Guesses by
   header text (RU/LV/EN keywords: "дата/datums/date", "итого/kopā/total", "оплачено/apmaksāts/paid", "показ/rādījums",
   "НДС/PVN/VAT"…) as a starting point.
4. **Preview / dry run**: per row the parsed values and the planned actions; filters "errors / warnings / all";
   totals ("132 readings, 66 bills, 60 payments, 3 rows skipped").
5. **Import** → progress → summary with a link to the batch; profile saved.
- Settings → **Imports**: list of batches (date, file, sheet, counts) with **Undo import** and the saved profiles
  (rename, delete, export/import profile as JSON file — so a profile can be shared with the project lead without data).
- Works on Windows (primary) and Android (file picker); large sheets (1 000 rows) stay responsive (parse off the UI
  thread).

### 3. Tests
- Synthetic workbook builder (code) producing sheets like §12: water (cold + hot readings, total, `***`, notes, VAT
  change, a meter replacement noted in a comment), heat (hot m³ × price + 12 %), fixed amounts (net + VAT / total
  only), electricity with a 1200 kWh/year period, propane equalized; LVL rows before 2014-01.
- Planner tests per issue type; import → counts; re-import same sheet → all duplicates skipped; undo → nothing left
  (soft-deleted) and balances back; currency rule; month text parsing (RU/LV/EN); `***` → payment = total, allocated,
  paid date = paid-date column or bill date or 1st of month.
- ViewModels: wizard steps, guesses by header, validation (period column required), dry-run summary.

### 4. Guide
- User guide EN + RU: section **Import from Excel** (prepare the workbook, map columns, read the preview, what is
  created, LVL, `***`, duplicates, undo, profiles). Remove "Excel import" from "Coming soon".

## Scope — do NOT
- No tariff/price import (prices stay in tariffs; the owner enters current tariffs by hand), no automatic contract or
  meter creation (they must exist — the wizard says so and links to the contract), no .xls (old format) or .ods.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Synthetic workbook: each sheet imports with the expected counts; re-import adds nothing; undo removes everything.
3. No real data in the repo; fixtures are generated.

## Manual test steps for the owner
1. Make a **backup** first (Settings → Backup). Settings → Import from Excel → your workbook → water sheet → contract
   "Water" of the flat → map: month, cold/hot readings, total, paid `***`, notes → Preview: check errors/warnings.
2. Import → Bills/Readings show the history; Charges/Reports for old months look plausible.
3. Settings → Imports → Undo import → history gone; import again.
4. Export the profile (JSON) and send it to the lead if something does not map — it contains no amounts or readings.
