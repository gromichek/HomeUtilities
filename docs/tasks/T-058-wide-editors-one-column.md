# T-058: Editors on a wide Windows window — one centred column, short fields in pairs (bill, payment)
Phase: 9 · Depends on: T-057 (#347) · Size: S–M · Owner 2026-11-16

## Why
On a wide Windows window the 10 editor pages put their cards into two columns (`AdaptiveFieldGrid MinColumnWidth="560"
MaxColumns="2"`). The owner, after daily use (screenshots of «Новый счёт»): the rows of cards leave big holes (a short
«Суммы» card next to the long «Основное»), every field is stretched to ~800 dp, and the order reads in a zig-zag. Decision
(owner 2026-11-16): **on a wide Windows window** the editor is **one centred column** (max ~840 dp); in the bill and payment
editors short fields sit **in pairs**. **Nothing changes on Android** (phone and tablet) **and on a narrow Windows window**
— the owner insists that what works stays exactly as it is.

Editors: Bill, Payment, Property, Supplier, Contract, Meter, Tariff component, Category, VAT rate, Bill template
(`BillEditView`, `PaymentEditView`, `PropertyEditView`, `SupplierEditView`, `ContractEditPage`, `MeterEditPage`,
`TariffComponentEditPage`, `CategoryEditView`, `VatRateEditView`, `BillTemplateEditView`).

## Scope — do
1. **`AdaptiveFieldGrid`**: new property, e.g. `WideSingleColumnMaxWidth` (double, default **0 = off**, today's behaviour
   exactly). When it is above 0 **and** the width would give 2 or more columns, the grid uses **1 column** and centres it
   with that maximum width (same centring as `SingleColumnMaxWidth`). When the width gives 1 column anyway, nothing
   changes (the existing `SingleColumnMaxWidth` 600 applies). The decision rule (width → columns, max width) in a pure
   function in `ListLayoutRules` (or next to it), unit-tested. The arrange code otherwise stays as it is.
2. The 10 editors set it **for Windows only**: `WideSingleColumnMaxWidth="{OnPlatform Default=0, WinUI=840}"` (or one
   shared style/resource used by all 10). Android: the attribute resolves to 0 → identical to today.
3. **Pairs in the bill editor** (General and Amounts cards) and **the payment editor** (General card): groups of fields in an
   inner `AdaptiveFieldGrid` with `MinColumnWidth="300"`, `MaxColumns="{OnPlatform Default=1, WinUI=2}"` and
   **`SingleColumnMaxWidth="0"`** (otherwise the inner grid would centre itself at 600 inside the card). Each cell is one
   field block (caption + field + its error label, or switch row + its field). **The order of the fields in one column
   (phone, narrow window) must stay exactly as today** — only consecutive fields are paired:
   - Bill · General: [Property | Contract] · Period stays a full row (centred ‹ › group as today) · [Kind | Title] ·
     [Number] · [issued-on switch + date | due-on switch + date] · service period: switch full row, [From | To] ·
     [Category | Supplier]. «Из шаблона…» and the type picker stay full rows.
   - Bill · Amounts: hint full row · [Net | VAT %] · [VAT | Total] · consumption full row.
   - Payment · General: [Property | Contract] · [Paid on | Amount] · [Method | Reference].
   A hidden block closes its gap (the grid already does this), e.g. a one-off bill without a contract: Property alone.
   If a pair does not fit (both blocks < 300 dp), it is one column — no cut texts.
4. Other cards and the other 8 editors: only item 1–2 (one column); fields inside unchanged.
5. Nothing else changes: no view model, no command, no text, no card order, no behaviour, no Android code. If something in
   the editors needs more than this to work, stop and write it under "Open questions".

## Not in this task
Pairs in the other 8 editors (later if the owner wants them); lists, filters, dialogs; Android tablet layout.

## Tests
- Rule (pure function): Windows wide max 840: available 1800 dp, min column 560, max 2 → 1 column, side margin
  (1800 − 840) / 2 = 480; available 1000 → 1 column (unchanged rule, 600 max); max width 0 → today's results for 600 / 1000 /
  1200 / 1800 dp (2 columns from ~1136 dp available, as today).
- Existing tests stay green. UI is not testable in the sandbox — say so.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (Windows + Android). UI checklist in the PR, including:
every changed XAML file listed; for each editor "Android: no change (attribute resolves to 0 / MaxColumns 1)". User guide:
n/a unless it describes the two-column layout — then EN + RU (search for it and say what you found).

## Manual test steps for the owner (short)
1. Windows, maximised: open each of the 10 editors → one column in the middle, no holes; bill and payment: short fields in
   pairs; everything works as before (choices, dates, switches, «Заполнить из PDF», Save, Back).
2. Windows, window narrowed to about half the screen → looks as before this task.
3. Phone and tablet (portrait and landscape): the editors look exactly as before.
