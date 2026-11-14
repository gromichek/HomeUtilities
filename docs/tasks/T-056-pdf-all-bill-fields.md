# T-056: Fill from PDF — every remaining bill field (quantities, tariff lines, notes, lines, custom fields)
Phase: 9 · Depends on: T-055 (#340) · Size: M–L · Owner 2026-11-14

## Why
T-055 fills the bill header from the supplier's PDF; the owner tested it with the layouts file from the lead — all OK. He
now also wants from the same PDF: **«Количество в счёте»** (`Bill.ConsumptionOnBill`), the **quantities of tariff lines
taken from the bill** (`BillQuantities`, components with basis "quantity from the bill", e.g. water losses, heat in MWh,
hot water difference) and **meter readings with meter numbers written into «Комментарий»** (`Notes`). The lead checked
the owner's PDFs: one supplier prints the consumption as the **sum of two lines**, the readings appear **once per meter**,
so a field must be able to take more than the first match. Layouts stay user data (T-055 rules: nothing real in the repo).
The owner (2026-11-14, revision of this task): cover **every field of the bill editor** now, so that layouts never wait for
another code change — hence also net amount, service period from–to, title, the amount of a tariff line, custom fields and
bill lines.

## Scope — do

### A. Layout format (Core) — backward compatible, no migration (fields are a JSON column)
1. New `PdfBillField` values (keep the existing numeric values; append the new ones):
   | Field | Bill | Value |
   |---|---|---|
   | `Net` | `Net` (amount without VAT) | money |
   | `ServiceFrom`, `ServiceTo` | service period from / to (`HasServicePeriod` on when both found) | date |
   | `Title` | title | text |
   | `Consumption` | `ConsumptionOnBill` («Количество в счёте») | number |
   | `Quantity` | quantity of one tariff line (`BillQuantities`) | number, needs `component` |
   | `ComponentAmount` | the amount of that tariff line from the bill (`BillQuantity.Amount`) | money, needs `component` |
   | `CustomField` | the bill's custom field with that name (`CustomFields`, target Bill) | by the field's kind: Text / Number / Money / Date; needs `name` |
   | `Notes` | «Комментарий» | text |
   | `Lines` | bill lines («Строки счёта») | one line per match, see A2 |
   Every entry may appear several times with different `component` / `name`.
2. New optional properties of `PdfBillFieldPattern` (and in the JSON file, camelCase, omitted when default):
   - **`component`** (string) — required for `Quantity` and `ComponentAmount`: the **name** of the tariff line of the
     bill's contract (case-insensitive, trimmed). **`name`** (string) — required for `CustomField`: the name of the custom
     field. Missing → validation error on import.
   - **`combine`**: `first` (default, today's behaviour) | `sum` (every match parsed as a decimal and added; any match that
     does not parse → the field is not found + warning) | `all` (only for `Notes` and `Lines`: every match).
   - **`format`** (only for `Notes`): a .NET substitution string applied to each match (`Match.Result`, e.g.
     `"${meter}: ${from} – ${to}"`); default = the value group as today. Matches are joined with `"; "`.
   - **`Lines`** always takes every match (`combine` ignored / must be `all`); each match gives one bill line from the
     named groups `description` (required), `quantity`, `unit`, `price` (unit price), `net`, `vat` — missing groups stay
     empty; a match whose numbers do not parse is skipped + warning.
   Validation on import: `combine: all` or `format` on another field, `sum` on a non-number field (Number, Title, dates,
   Period, Notes, Lines, a Text/Date custom field is checked when applied) → error with the field name; `Lines` without a
   `description` group → error.
3. `PdfBillReader` / `PdfBillResult`: values for the new fields; quantities as a list `(component name, value)`; Found /
   NotFound / warnings per entry (a `Quantity` entry is named by its component in the summary, not by the field kind).
   Same regex options and 1 s timeout per match operation; `sum` and `all` loop over `Matches`, the timeout covers the
   whole loop.

### B. Bill editor
4. Apply after the header fields (and after the period is set, because the quantity rows depend on the period and the
   contract's tariff lines — if the rows are rebuilt asynchronously after a period change, wait for that, do not race):
   - `Consumption` → `ConsumptionText` (same formatting as typing), so the bill comparison updates.
   - each `Quantity` → the quantity row of the component with that name (`QuantityRows`, same path as typing). No row with
     that name on this bill (unknown name, the line is not "quantity from the bill", or not valid in the period) →
     warning in the summary: tariff line «{name}» is not on this bill — nothing else changes.
   - `Notes` → appended to the existing notes on a new line; **not added again** if the notes already contain exactly that
     text (filling twice from the same PDF does not duplicate it). Respect the notes length limit (cut + warning).
   - `Net` → with Gross: Vat = Gross − Net (VatPercent from the PDF if found, otherwise derived as the editor does); with
     Gross and Vat as well, Gross wins and a mismatch of more than 0.01 → warning. Net + Vat = Gross always holds.
   - `ServiceFrom` / `ServiceTo` → both found: service period on with these dates; only one → warning, nothing set.
   - `Title`, `CustomField` (row by name, case-insensitive; unknown name → warning; value parsed by the field's kind, not
     parsable → warning), `ComponentAmount` (same row lookup as `Quantity`) → same path as typing.
   - `Lines` → added as bill lines **only when the bill has no lines yet**; otherwise warning "the bill already has lines —
     not changed". Lines get no category of their own (as a new line typed by hand). A sum of lines that does not match the
     bill is shown by the editor's existing checks, not changed by this task.
5. Summary: the new values in the "filled" list with their localized names (Количество в счёте, the component's own name,
   Комментарий — the notes text itself shortened to ~80 characters), not-found and warnings as in T-055.

### C. Guide, samples
6. User guide EN + RU, section "Filling a bill from a PDF": the new fields, `component` = the name of the tariff line exactly
   as in the contract (renaming the line needs the layout updated), `sum` and `all`/`format` with an **invented** example
   (two invented lines added; one invented meter with readings to notes). Mention that the layouts file comes from the lead.
7. Sample data: extend the invented sample layout with one `Consumption` and one `Quantity` entry.

## Not in this task
Readings into the Readings screen (part 3, maybe later — a different entity); bill kind, currency, template, draft;
choosing the contract from the PDF;
any change of the header logic of T-055; layouts of real suppliers anywhere in the repository.

## Tests
- Parsing (Core, invented texts): `Consumption` with `sum` over two lines `"Hot water declared 6.420 m3"` and
  `"Hot water difference 0.310 m3"` → **6.730**; one unparsable match in `sum` → not found + warning; `Quantity` for two
  components; `Notes` with `all` + `format` over two meter lines `"1111111111  105.20 - 106.10"`,
  `"2222222222  8200.00 - 8315.40"` and format `"${m}: ${a} – ${b}"` → `"1111111111: 105.20 – 106.10; 2222222222:
  8200.00 – 8315.40"`; timeout over a `sum` loop.
- Net: Gross 60.48 + Net 54.00 → Vat 6.48; Gross + Net + Vat with a 0.05 mismatch → warning, Net + Vat = Gross.
- Service period: `"01.10.2026 - 31.10.2026"` with two entries → both set; only one found → warning, unchanged.
- Custom fields: Text, Number, Money, Date by name; unknown name → warning; `"abc"` for a Number field → warning.
- Lines: two invented table rows → two lines with description, quantity, unit, price, net; a bill with lines → unchanged +
  warning; a row with an unparsable number skipped + warning.
- Layout file: new properties round trip; old T-055 files (without them) read unchanged; validation errors listed in A2.
- Bill editor: consumption and two component quantities set (row lookup by name, case-insensitive); unknown component →
  warning, other values still filled; notes appended once (fill twice → no duplicate), appended to existing notes on a new
  line; period change before quantities (a component valid only in the PDF's period gets its quantity).
- Existing T-055 tests stay green.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green. Guide EN + RU updated. UI checklist: n/a for layout
(no new controls) — say so; the summary dialog text wraps.

## Manual test steps for the owner (short)
The lead sends an updated layouts file (import replaces the layouts with the same name).
1. Water bill → Fill from PDF (October) → «Количество в счёте» and the water-loss line filled as on the paper.
2. Heating bill → Fill from PDF (October) → «Количество в счёте» (sum of both hot-water lines), heat in MWh and the hot water difference as on the paper.
3. Electricity bill → Fill from PDF (October) → «Комментарий» has both meters with readings «from – to»; fill again → the
   comment is not doubled.
4. If the layouts file has lines for a supplier: a new bill gets its lines from the PDF; a bill that already has lines keeps them.
