# T-014: Contracts — list in the property editor, contract editor
Phase: 3 · Depends on: T-013 · Size: L

## Goal
Let the owner set up the contracts of each property (water, electricity, internet, kindergarten…): supplier,
category, numbers, billing mode and timing, reading window, links, payment details (overriding the supplier's),
custom field definitions and notes. Also move the Properties screen onto the shared master/detail base classes
introduced in T-013 so all screens behave identically.

## Context
- `docs/UI_GUIDELINES.md` (incl. §6a pitfalls) — follow exactly; reference screens: Properties, Suppliers.
- `docs/DATA_MODEL.md` §3 (Contract, PaymentDetails, Link), §6 (CustomFieldDefinition), `docs/PAYMENT_TYPES.md`.
- Existing: `IContractStore`, `ISupplierStore`, `ICategoryStore`, `ContractValidator`, `CustomFieldValidator`,
  `PaymentDetails.Effective`, `Contract.GetEffectiveLinks`, `Bill.DefaultPeriod`, `PaymentPurpose`,
  `BuiltInCategories.FindFor`, T-013's `MasterDetailViewModel` / `PaneEditorViewModel` / `EditorPageBase`.
- Known issue to close: `ContractValidator` must validate `Contract.CustomFields` with
  `CustomFieldValidator.ValidateAll` (add it in Core + test).

## Scope — do

### 0. Refactor Properties onto the shared bases
Move `PropertiesViewModel`/`PropertyEditViewModel`/pages onto `MasterDetailViewModel`, `PaneEditorViewModel`,
`EditorPageBase` from T-013 (suggestion in PR #34). Behaviour must stay identical; existing tests keep passing
(adapt only construction details).

### 1. Contracts card in the property editor
- New card **Contracts** in `PropertyEditView` (below Parameters), header with `IconAdd` (disabled with hint
  "Save the property first" for an unsaved new property).
- Rows: category colour dot, title, supplier name, service kind (localized), "ended {date}" / archived badge;
  tap → open the contract editor; no delete icon in the row (delete is inside the editor).
- "Show ended/archived" toggle inside the card (default off).

### 2. Contract editor (route `ContractEdit`, parameters `propertyId`, `id` optional)
Opens as a **page on all layouts** (it is too large for a pane/dialog). Cards:
1. **General**: Title*, Service kind* (picker, localized `ServiceKind` names), Category* (picker with colour dots;
   when the service kind changes and the user has not picked a category yet, preselect
   `BuiltInCategories.FindFor(kind)`), Supplier* (picker with search; item "＋ New supplier…" opens the supplier
   editor and returns the created supplier), Currency (EUR default; LVL only shown if already set), Archived.
2. **Account**: Customer number, Contract number, Portal login (hint: "Passwords are not stored — your browser
   remembers them"), Valid from* (date), Valid to (optional: switch "Has end date" + date).
3. **Billing**: Billing mode (picker: by consumption / equalized / fixed / tax), Billing timing
   (in arrears / in advance — with one-line explanations), Reading window (switch + From/To day 1–31, hint
   "e.g. 25–28"; wrapping window allowed), Usual bill day, Payment due day (optional numbers 1–31).
4. **Links**: own links (same row + dialog pattern as Suppliers); below, the **inherited supplier links**
   (muted, with Open button only, label "from supplier") — computed with `GetEffectiveLinks`.
5. **Payment details**: same fields as the supplier; each empty field shows the supplier's value as placeholder
   ("from supplier: …"); **effective details** preview with copy buttons (IBAN, recipient, purpose); purpose
   rendered with the **real** contract context: customer number, contract number, period =
   `Bill.DefaultPeriod(contract, today)`, property name.
6. **Custom fields**: list of definitions (name, kind, applies to bill/line), row → dialog (name, kind picker,
   target picker), trash icon with confirm; explanation line "These fields appear when you enter a bill".
7. **Notes**.
Footer/toolbar per guidelines (Save primary, Cancel, "Delete contract" destructive with confirm).
Validation via `ContractValidator` (+ custom fields), errors under fields, field paths mapped
(`Links[0].Url`, `PaymentDetails.Iban`, `CustomFields[1].Name`, …).

### 3. Resx (EN + RU)
All labels, hints, enum names (`BillingMode`, `BillingTiming`, `CustomFieldKind`, `CustomFieldTarget`), error
messages for every code used by `ContractValidator`, `CustomFieldValidator`, `PaymentDetailsValidator`.

## Scope — do NOT
- No meters (T-015), no tariffs (T-017), no bills. No contract templates (T-025).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. On Windows and Android: create a water contract for a property with supplier, category preselected from
   the service kind, reading window 25–28, own portal link, supplier website inherited, IBAN inherited from the
   supplier, purpose example with the real customer number; save, reopen, edit, delete.
3. Properties screen behaves exactly as before the refactor.
4. Known issue "ContractValidator does not validate CustomFields" closed.

## Tests required
- Core: `ContractValidatorTests` — duplicate custom field names → error with field path.
- ViewModels: `ContractEditViewModelTests` — category preselect on service change only while untouched;
  "＋ New supplier" flow selects the returned supplier; has-end-date switch; reading window both-or-none error;
  inherited links shown and not duplicated by own links of the same kind; payment placeholders from supplier;
  effective purpose `Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}` with customer `CUST-1`, fake today
  2026-10-23, timing InArrears → `Abon. Nr. CUST-1, 09.2026`, InAdvance → `11.2026`; custom field dialog
  add/edit/delete; CanLeaveAsync combinations; save calls store with FK `PropertyId` set.
- `PropertyEditViewModelTests` (extended): contracts card lists the property's contracts, hides ended/archived by
  default, add disabled for unsaved property.

## Manual test steps for the owner
Checklist `docs/UI_GUIDELINES.md` §7, plus:
1. Suppliers: create "Water utility" with website + IBAN `LV80BANK0000435195001` + purpose template
   `Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}`.
2. Objects → your apartment → card **Contracts** → ＋. Service kind "Cold water" → category "Cold water"
   preselected. Supplier "Water utility". Customer number `12345`. Reading window 25–28.
3. Links card: the supplier website appears greyed "from supplier" with Open. Add own "Customer portal".
4. Payment details: IBAN field empty with placeholder from supplier; effective preview shows the IBAN and
   purpose "Abon. Nr. 12345, 09.2026" (previous month). Copy → paste in Notepad.
5. Custom fields: add "Losses m³" (Number, bill).
6. Save → contract appears in the card. Reopen → everything kept. Back with changes → "Save changes?".
7. Delete contract → confirm → gone. Phone + tablet short check.
