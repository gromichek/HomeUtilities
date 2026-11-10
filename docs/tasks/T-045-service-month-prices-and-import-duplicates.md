# T-045: Prices of the service month for every tariff line; import warns about duplicate readings
Phase: 9 · Depends on: T-044 (#298) · Size: M

## Why (owner 2026-11-10, lead's analysis of the owner's backup — no personal data in this file)
1. **Prices.** `ChargeCalculator` takes the price of a *By consumption (meters)* / *Net export* line on the **end reading
   date**, all other lines on the **first day of the period** (T-006). With a reading window that crosses the month end
   (28 – 3) the end reading of September can be dated 1 October, so a new price from 1 October already prices September's
   meter line while the *Quantity from the bill* / fixed lines of the same bill still use September's price. The owner's
   supplier bills show the opposite: **all lines of one bill use the prices of the service month** (a price change appears in
   the meter line, the difference line and the heat line of the same month at once). To make the app match, the owner had
   to date the meter line's prices one month later than the other lines — and got some of them wrong.
   **New rule (DATA_MODEL §5 "Charge calculation", updated by the lead): every component uses the rate valid on
   `Period.FirstDay`.** The reading window keeps deciding which readings belong to the period (unchanged).
2. **Duplicate readings from the import.** A meter linked to two contracts (hot water: water + heating) got its readings
   twice: the water sheet imported them with their dates (26th), the heating sheet's column "Показания счетчика" was
   pre-selected by `ImportColumnGuesser` (header contains "показ") and imported the same values dated with the row date (1st).
   The owner did not notice the pre-selection. Same values, different dates → doubled history, wrong averages.

## Scope — do
### A. One price date
1. `ChargeCalculator`: rate **and** VAT date = `Period.FirstDay` for all bases (`PerUnit`, `NetExport` and tiered lines
   included). Remove the end-reading rate date (`Metered.RateDate`). Tier periods unchanged. Warning `NoRateForDate` names
   the first day of the period.
2. Everything that shows or explains "which price" follows (charges detail, tariff example text, reports "expected" use the
   same calculator — check nothing else computes its own rate date: `rg RateAt`).
3. Existing tests that expect the end-reading date: change them to the new rule and say so in the PR. New tests with
   concrete numbers, e.g. window 28 – 3, price 3.50 until 30 Sep, 3.88 from 1 Oct; September readings 01 Sep 100.000 → 01 Oct
   108.722: September = 8.722 m³ × 3.50 = 30.53 (old rule gave × 3.88 = 33.84); October 01 Oct → 01 Nov 117.444: 8.722 × 3.88
   = 33.84; a *Quantity from the bill* line of the same months uses the same prices.
4. User guide EN + RU, section 11 Tariffs → "Which price a month gets" (added 2026-11-10): replace with the new rule in two or
   three lines: "every line uses the price valid on the 1st of the service month; the reading window only decides which
   readings belong to the month; enter a new price once, with Valid from = the 1st of the month from which the supplier
   applies it". Remove the workaround paragraph.

### B. Import: duplicate readings
1. In the import preview (`ImportPlanner`): a reading for a register that already has a reading with the **same value**
   within **7 days** (constant in one place) — from another import or typed — is a duplicate: not created by default,
   listed in the preview as one warning line with the count, e.g. «Показания: 44 уже есть у счётчика (то же значение в
   пределах 7 дней) — не импортируются.» (EN + RU), with a switch «Импортировать всё равно» (off). Also counted in the result
   (`SkippedCount` or a new count — say which).
2. The column guesser keeps suggesting reading columns, but the preview shows **which meter** each reading column was
   assigned to (one line per register: «Столбец B «Показания счетчика» → счётчик «Ванная · 12345678»»), so a pre-selection
   is visible before the import.
3. Guide EN + RU, section Import: the duplicate rule and the meter line in the preview.

## Not in this task
No change of the reading window logic, no automatic clean-up of existing duplicates (the lead fixes the owner's database
separately), no proration of mid-month price changes.

## Tests
A: as above + tiered line uses the period's first day; NetExport; a period whose end reading is missing (rate date no longer
falls back to the last day). B: duplicates within 7 days skipped, 8 days apart imported, different value imported, switch on
→ imported; counts in preview and result; register assignment lines.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; guide EN + RU; UI checklist for the import preview
lines (wrap in Russian at 360 dp).

## Manual test steps for the owner (short)
After the lead restored the corrected backup (separate step):
1. Heating contract → Charges: October 2025 and June 2026 — expected amounts equal the bills.
2. Tariff → a price → the explanation in the guide ("?" on the tariff).
3. Import a sheet with a reading column of a meter that already has these readings → the preview says they are skipped and
   shows which meter the column goes to. (Cancel the import.)
