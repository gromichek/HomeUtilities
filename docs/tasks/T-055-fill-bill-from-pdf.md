# T-055: Fill a bill from the supplier's PDF (header fields) — layouts kept in the user's database
Phase: 9 · Depends on: T-054 (#336) · Size: M–L · Owner 2026-11-14

## Why
The owner gets the bills of his suppliers as PDF files and types number, dates, amounts and VAT by hand. The lead checked
real bills of three suppliers: all are **text PDFs** (no scans), every field has a fixed label next to it, but the layouts
differ (dates in words, "amount to pay" that includes debt/overpayment, two totals on equalized bills). This task adds a
**generic** engine: a *PDF bill layout* says which text identifies the supplier and where each field is (a regular
expression). **Layouts are user data**: they live in the database (synced, in backups), are imported from a JSON file that
the lead writes for the owner, and are **never** in the repository — the repository is public and the supplier names would
tell where the owner lives. Code, tests, samples and the guide use invented suppliers only.

Part 2 (later task, not now): bill quantities / «Количество в счёте» from table rows. Part 3 (maybe): readings.

## Scope — do

### A. Text extraction (HomeUtilities.Import)
1. New dependency **PdfPig** (`PdfPig` on NuGet, Apache-2.0, pure managed; latest stable). Check its transitive packages and
   their licences; name them in the PR. Version in `Directory.Packages.props`. Core gets **no** package.
2. Interface in Core, e.g. `IPdfTextReader.ReadAsync(Stream, CancellationToken) → PdfText` (or `string`), implementation
   in `HomeUtilities.Import`, registered in the App. Never throws for a broken / encrypted / image-only PDF: returns an
   error code (localized message in the ViewModel). Runs off the UI thread. The PDF is read into memory once (they are
   < 1 MB); a file > 20 MB is refused.
3. **Text in reading order, one text line per visual line** (like `pdftotext -layout`, which the lead used for checking):
   words of a page grouped into lines by their baseline (small tolerance), ordered left → right; words with a normal gap
   joined by one space, a wide gap (columns) by **at least two spaces**; lines top → bottom; pages separated by an empty
   line. Keep diacritics (ā, ē, ī, ū, š, ž, ņ, ļ, ķ, ģ, č). Put this "line builder" in a pure function over
   `(text, x, y, width)` words so it is unit-tested without a PDF.

### B. Layouts and the field parser (Core, pure)
4. Entity **`PdfBillLayout : SyncEntity`** — follow `SavedImportProfile` everywhere it appears (DbSet, configuration, JSON
   column, store, sync, JSON export/import of all data, database clean-up, sample data). Fields: `Name`; `Markers`
   (strings that must **all** occur in the PDF text, case-insensitive, e.g. a supplier name and a document title);
   `Fields` (list of `{ Field, Pattern }`); `Notes`. Migration. `Field` = `Number | IssuedOn | Period | DueOn | Gross |
   VatPercent | Vat`.
5. `Pattern` is a .NET regular expression run on the **whole** text (lines joined by `\n`), options IgnoreCase +
   CultureInvariant + Multiline, **match timeout 1 s**. Value = named group `v`, otherwise group 1, otherwise the match.
   First match wins. Invalid regex → validation error on import (field name + the regex error), not a crash.
6. **Value parsers** (unit-tested with the cases below):
   - Money / VAT %: `59.49`, `59,49`, `1 234.56`, `1 234,56`, optional `EUR` / `€` / `%` around → `decimal`, never double.
   - Date: `30.11.2026`, `30.11.2026.` (trailing dot), `2026.gada 30. septembris`, `2026. gada 30. septembris`,
     `2026. gada 1. jūlijs` → `DateOnly`.
   - Period: `2026. gada septembris`, `2026.GADA septembrī`, `09.2026`, `2026-09` → `YearMonth`.
   - Latvian month names in nominative **and** locative, any case: janvāris/janvārī, februāris/februārī, marts/martā,
     aprīlis/aprīlī, maijs/maijā, jūnijs/jūnijā, jūlijs/jūlijā, augusts/augustā, septembris/septembrī, oktobris/oktobrī,
     novembris/novembrī, decembris/decembrī. Also tolerate the same names without diacritics (`julijs`, `septembri`).
7. `PdfBillReader` (or similar): text + layouts → `PdfBillResult { LayoutName?, Values per field, NotFound fields,
   Warnings }`. Layout choice: layouts whose markers all occur; several → the one with most markers; none → result
   "no layout fits". A value that is found but does not parse = not found + warning with the raw text.
8. Layout file: JSON like `ImportProfileFile` (version, list of layouts; **several layouts per file**), read never throws,
   write for export. Import: a layout with the same `Name` is **replaced** (updated in place), others added. Example in the
   guide and tests with an invented supplier, e.g.
   ```json
   { "version": 1, "layouts": [ { "name": "Example Water", "markers": ["SIA \"Piemērs Ūdens\"", "RĒĶINS NR."],
     "fields": [ { "field": "Number", "pattern": "RĒĶINS NR\\.:\\s+(\\S+)" },
                 { "field": "DueOn",  "pattern": "APMAKSAS TERMIŅŠ\\s+(\\d{2}\\.\\d{2}\\.\\d{4})" },
                 { "field": "Gross",  "pattern": "KOPĀ APMAKSAI EUR:\\s+([\\d.,]+)" } ] } ] }
   ```

### C. Bill editor: «Заполнить из PDF»
9. Secondary button **"Fill from PDF"** in the bill editor's document section (next to "Add PDF or photo"; stacked full
   width on compact width, UI checklist). Only when the bill can be edited. It picks a PDF with the same picker as
   "Add PDF or photo" (PDF only), reads it off the UI thread, applies the layout and:
   - sets every **found** field through the same path as when the user edits that field (so checks, the calculated
     comparison and any period-dependent defaults update exactly as by hand). Fields not found stay as they are;
   - **Period** not found → `Bill.DefaultPeriod(contract, IssuedOn)` when `IssuedOn` was found (e.g. a bill issued in
     November for an InArrears contract → October); otherwise unchanged;
   - **amounts**: Gross always from the PDF if found; with Vat found → Net = Gross − Vat (VatPercent from the PDF if found);
     with only VatPercent → Net/Vat derived as the editor does (`MoneyMath`); the invariant Net + Vat = Gross holds.
     Kind (Regular / Equalized / …) is not changed;
   - adds the PDF as a pending attachment (saved with the bill), unless an attachment with the same file name is already
     on the bill;
   - shows a short summary dialog: layout name; filled fields with their values (formatted with the app culture); fields
     not found; warnings. Then the user checks and saves as usual — **nothing is saved automatically**.
10. Supplier check: if the contract's supplier has a `RegistrationNumber` and that number (digits only, also with an `LV`
    prefix) is **not** in the PDF text → warning in the summary "This PDF may be from another supplier" (values are still
    filled).
11. No layout fits → message: no layout for this PDF + hint where layouts are managed and how to show the PDF text.
    Unreadable PDF → message with the reason. Nothing changed in the form in both cases.

### D. Managing layouts (Imports screen, next to the import profiles)
12. Section **"PDF bill layouts"**: list (name, markers in one line, number of fields); **Import layouts…** (JSON file);
    **Export all…** (one JSON file, same format); Delete per layout (soft delete + Undo like the import profiles).
13. **"Show PDF text…"**: pick a PDF → a read-only page/dialog with the extracted text (selectable, monospace if
    available) and **Copy**. This is how the owner sends the lead the text of a new or changed bill layout; say in the guide
    that the text contains personal data and is only meant for whoever writes the layout.

### E. Tests with real PDFs — local only
14. Git-ignored folder **`tests/LocalData/`** (add to `.gitignore` with a comment). A test attribute (e.g.
    `[LocalDataFact]`) skips the test when `tests/LocalData/pdf-bills/` does not exist (CI, cloud sessions). The test reads
    `tests/LocalData/pdf-layouts.json` and every `*.pdf` in `pdf-bills/`, and asserts that a layout fits and that Number,
    DueOn and Gross were found; it writes the found values to the test output. Describe the folder layout in a comment on
    the attribute and in the PR (a README inside the ignored folder would not be committed; `docs/ARCHITECTURE.md` is not
    yours — propose a line for it in the PR).

## Not in this task
Table rows / bill quantities / «Количество в счёте» / readings from the PDF (part 2/3); OCR or photos; layout editor UI
(layouts come as a JSON file); automatic saving; choosing the contract from the PDF (the editor's contract is used); no
layouts of real suppliers anywhere in the repository (not in code, tests, samples, docs or commit messages).

## Tests
- Line builder: two columns on one baseline → one line with ≥ 2 spaces between; slightly different baselines (±1 pt) →
  same line; order top → bottom, left → right; two pages.
- One small **synthetic PDF generated in the test** with PdfPig's `PdfDocumentBuilder` (standard font, ASCII text, two
  columns) → `IPdfTextReader` returns the expected lines. No PDF files from suppliers in the repository.
- Parsers: all formats in A6 (e.g. `"2026.gada 30. septembris"` → 2026-09-30; `"2026.GADA oktobrī"` → 2026-10;
  `"1 234,56 EUR"` → 1234.56; `"12 %"` → 12).
- Reader on invented texts: layout chosen by markers (two layouts, the one with more markers wins; none fits); value
  found / not found / unparsable; regex timeout and invalid regex; named group `v` vs group 1.
- Amount rule: Gross 59.49 + Vat 10.33 → Net 49.16; Gross 34.20 + VatPercent 21 → Net 28.26, Vat 5.94; Gross only → Net
  and Vat stay empty.
- Period fallback: InArrears, IssuedOn 2026-11-06, no period → 2026-10.
- Layout file: read/write round trip, several layouts, replace by name on import, broken JSON → error, never throws.
- Bill editor ViewModel: fill → fields set, pending attachment added once, summary content, nothing saved; no layout →
  nothing changed; supplier-number warning.
- Sample data + its tests: one invented layout. Existing tests stay green.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (Windows + Android build). PR names PdfPig and every
transitive package with its licence and says how much the Android APK grows (approx.). User guide EN + RU: new section
"Filling a bill from a PDF" (what a layout is, that layouts come as a file, Import/Export, Show PDF text, that the result
must be checked, the supplier warning) + Imports section; `HelpTopic` for the new section. UI checklist in the PR.

## Manual test steps for the owner (short)
The lead sends a layouts file made from the owner's bills.
1. Imports → PDF bill layouts → Import layouts… → the file → three layouts listed.
2. Charges or Bills → new bill for water → Fill from PDF → the October water PDF → summary shows the filled fields; number,
   date, due date, period, total and VAT match the paper; the PDF is in the document section. Save.
3. Same for heating (total = «KOPĀ aprēķina mēnesī», not the amount to pay) and electricity (total = «Apmaksai»).
4. Fill a water bill from an electricity PDF → warning about another supplier (if the supplier's registration number is set).
5. Show PDF text… → text readable, Copy works. Phone: the same with a PDF from Downloads.
