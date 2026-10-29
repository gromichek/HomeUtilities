# T-027a: Export "like my workbook" — one sheet per contract, the reverse of the import
Phase: 7 · Depends on: T-027 · Size: M

## Goal
The owner wants to get his data back in the shape of his own Excel workbook (one sheet per supplier, one row per
month): to compare the import with the original sheet by sheet, and to keep a familiar spreadsheet as an extra copy.
The layout of each sheet comes from the **import profile** of that contract (T-026/T-027), so an exported file can be
imported again without mapping.

## Context
- `ImportProfile` (columns, header/first row, period rules, positions, fallback total, paid marker, notes columns),
  ClosedXML writer (`HomeUtilities.Import`), file save service (T-023), reports' EUR/LVL handling, meter chains
  (`MeterChain`), bills/payments/readings/notes.

## Scope — do
1. Settings → Import from Excel → **Export to Excel** (and a button in Settings → Imports): choose properties/contracts
   (multi-select dialog §3a), period (all / from–to), currency for amounts before 2014-01 (as stored: LVL, or converted
   to EUR), file name `HomeUtilities-export-YYYY-MM-DD.xlsx`.
2. **One sheet per contract** (name = supplier + contract title, ≤ 31 characters, unique). Layout:
   - with an import profile: header row and columns exactly as in the profile (column letters, header texts as stored
     from the imported file), the date column written according to the profile's period rule *in reverse* (reading
     date for "by window"/"reading date" rules, the 1st of the next month for "previous month" rules, the payment date
     where the profile says the date is the payment date);
   - without a profile: a standard layout — Date (reading date or period), one column per meter position (cold/hot…
     with the register kind for multi-register meters), Consumption, Net, VAT %, VAT, Calculated total, Bill total,
     Difference, Paid (`***` + paid date column), Notes.
   - One row per period; months with a meter replacement get an extra row with the new meters' initial readings on the
     replacement date and a note "replaced: old final … / new initial … / serial …" in the notes column.
   - Numbers as numbers (2 decimals money, 3 decimals readings), dates as dates, header bold + frozen, column widths fit.
3. **Sheet "Total"** (localized "Всего"/"Total"): rows = months, columns = contracts (gross, billed if present else
   calculated), row and column totals — same numbers as Reports → Costs by contract.
4. **Round trip test**: build data → export → import the file into an empty database with the stored profiles →
   same bills (period, totals), payments, readings, meter chains (within rounding). Plus layout tests (profile vs
   standard), sheet-name sanitising, LVL/EUR option.
5. Guide EN + RU: Import section gets "Export like your workbook" (what it contains, how to compare with the original,
   re-import).

## Scope — do NOT
- No formulas reproduced from the original workbook (values only), no charts in the export, no .xls/.ods.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner: after importing his water sheet, the exported water sheet matches the original rows (dates, readings,
   totals, paid marks) apart from intentional fixes; the replacement months show the new meters.
3. Round trip test green.
