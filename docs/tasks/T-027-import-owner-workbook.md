# T-027: Import wizard for the owner's real workbook — reading dates, meter positions, automatic replacements
Phase: 7 · Depends on: T-026 · Size: L

## Background (lead analysed the owner's workbook locally on 2026-10-29; it is private and NOT in the repo — all
numbers below are invented but have the same shape)
- One sheet per supplier, one row per month, ~22 years (2004–2026). Column A is a **date**, but its meaning differs:
  - water sheet: **date the readings were taken** — first years on the 1st of the month (01.05 = readings for April),
    later on the 26th (26.10 = readings for October; the contract's reading window is 25–28);
  - heating sheet: the 1st of the month **after** the service month (01.09 row = August bill, its hot-water m³ equals
    the August reading of the water sheet);
  - electricity sheet: the 1st of the month = **reading date for the previous month** (01.09 = August readings);
  - fixed-amount sheets (maintenance, waste, internet, TV): the 2nd of each month, shown as "August 2025" by the cell
    format = **payment date for the previous month** (02.08.2025 = July paid on 2 August) — owner, 2026-10-29;
  - internet: from a date in 2023 the service is **prepaid**; the first prepaid row pays two months plus a connection
    fee.
- Water: cold and hot reading columns, a consumption column (both meters together), own calculated net / VAT / total
  columns, a "paid" column with `***`, and since 2016 an extra column with the **real bill total** (the calculated total
  remains next to it) and a difference column; notes in side columns.
- **Meter replacements** (6 per position in 22 years), visible as:
  1. an extra row in the same month with a much lower (or much higher) value = readings of the new meters on the
     installation day (e.g. 01.05: 202 / 190, then 07.05: 93 / 477 — the new hot meter started at 477);
  2. a regular monthly row where the value drops (e.g. 399 → 6) — no final reading of the old meter, the month's
     consumption is still in the consumption column;
  3. like 2 plus a note with the replacement date ("поменяли счетчики 30.04.2015") and new serial numbers;
  4. like 2 plus a note with exact values: "skaititaja nr.: Auksta - 1210007 rad:398,054 rad:0,072" = old final 398.054,
     new initial 0.072 (Auksta = cold, Karsta = hot).
  Two dates in one month **without** a drop/jump are **not** a replacement (the switch from the 1st to the 26th).

## Scope — do
1. **Period from a reading date.** Profile option *Period rule* for the date column:
   - **By the contract's reading window** (default when the contract has one): the reading date is assigned to a period
     with the same rules as charges (DATA_MODEL §4 rule 5: inside the window → that month; late reading after the window
     end and before the next window start → that month) — 01.05 with window 25–28 → April; 26.10 → October.
   - **Month of the date**; **Previous month** (date = 1st of the following month — heating); **Previous month if the
     day ≤ N, else the month of the date** (N configurable, default 10).
   The reading keeps its real date; the bill gets the period. Preview shows "date 01.05.2004 → period 04.2004".
   - The rule can **change from a date** (e.g. internet: "previous month" until 01.2023, "month of the date" or "next
     month" from 02.2023 for prepaid) — a list of `{ FromDate, Rule }` in the profile.
   - Option **"The date is the payment date"**: with a paid marker the payment gets the row's date (fixed-amount sheets).
   - **Per-row override in the preview**: the owner can change a row's period and mark it as covering several months
     (sets `ServiceFrom/ServiceTo`, the amount is spread by reports), e.g. the first prepaid internet row; a remark field
     for "incl. connection fee" goes to the bill notes. Overrides are stored in the profile by row date, so a re-import
     keeps them.
   Defaults the wizard suggests: water → by reading window; heating, electricity → previous month; fixed-amount sheets
   → previous month + date is the payment date.
   Two rows in one period: readings of both rows are imported (both dates), the bill/payment only once (from the row that
   has amounts; if both have, the later one, with a warning).
2. **Meter positions instead of meters.** The register mapping lists **positions** = replacement chains of the contract
   (one per first meter of a chain: "Cold water — Bathroom", "Hot water — Bathroom"), not every meter. A reading is stored
   on the meter of the chain that is installed on the reading date. If the owner set up only the first meter, the
   chain grows by automatic replacements (3).
3. **Automatic replacements** (`ReplacementDetector` in Core, unit tested on synthetic series of each pattern above):
   - Detect per position: value lower than the previous reading (and the meter has no dial digits, or the drop is not a
     plausible roll-over), or higher than previous + max(5 × median monthly consumption of the last 12 readings, 50).
   - For each detected replacement propose: replacement date (date in a note if one is found near the row, else the
     row's own date for pattern 1, else the day after the previous reading), old meter **final** and new meter
     **initial**:
     - pattern 1: final = last reading before, initial = the low/high value of the extra row (exact, that row is the
       installation day);
     - pattern 4: values parsed from the note (`rad:` numbers with `,` or `.`; first = old final, second = new initial;
       Auksta/Karsta or the column order decide the position);
     - otherwise initial = 0 and final = previous reading + (row consumption from the consumption column − new readings'
       consumption), split between the positions in proportion to their consumption of the previous month; without a
       consumption column final = previous reading. Such values are marked **estimated** (`ReadingKind.Estimated` for the
       final, note "estimated during import").
   - Preview: a section **Meter replacements** with one editable row per replacement (position, date, final, initial,
     new serial number from the note if found) — the owner can correct values from paper before importing.
   - Import creates the new meters like the replacement wizard does (same resource/unit/registers/location, Digits
     copied, `ReplacedMeterId`, links of the old meter ended the day before, new links from the date; final + initial
     readings) — reuse the replacement wizard's Core/Data logic, in the same transaction as the batch; Undo import removes
     them too.
4. **Amount columns with fallback.** "Bill total" may name **two** columns: preferred + fallback ("real bill total, else
   calculated total") — the preview marks which one was used. Same for Net/VAT optional.
5. **Readings from another sheet.** A sheet may map **no** reading columns (heating sheet: hot m³ come from the water
   sheet) — then only bills/payments are imported; a "consumption on bill" column can be mapped to
   `ConsumptionOnBill`.
6. **Profile export/import** stays data-free. Guide: Import section (period rules with examples, positions, automatic
   replacements and how to check them, fallback columns, heating sheet without readings).
7. **Reports wheel fix (from T-023b test):** Shift+wheel over the report table does not scroll it sideways on Windows —
   make it scroll horizontally (and keep plain wheel → page).

## Tests
- Synthetic workbook generator extended with all four replacement patterns, the 1st→26th switch, a real-total column
  that starts in a later year, a heating-like sheet with dates on the 1st of the next month and no readings, fixed sheets
  on the 2nd. Import → counts, periods, chain lengths, final/initial values (exact vs estimated), consumption of the
  replacement months equals the consumption column (± rounding), no replacement detected at the 1st→26th switch,
  re-import adds nothing, undo removes meters/readings/bills/payments.
- Period rules with concrete dates (window 25–28, window 28–3, previous month, day ≤ 10).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner: the water sheet imports with two positions and the replacements proposed in the preview; after the import the
   Readings history shows the meter chains; Reports → Consumption shows no dips/jumps at replacements.
3. The heating sheet imports bills for the right months without importing readings twice.
