# T-013: Suppliers and categories screens (links, payment details, copy, open in browser)
Phase: 3 · Depends on: T-012 · Size: L

## Goal
Screens for suppliers (with web links that open in the browser and bank payment details with copy buttons)
and for payment categories (rename built-ins, recolour, add own categories). Reuse the patterns from T-012
(`EditableViewModel`, two-pane list/editor, `ValidationText`, `EnumText`, `DecimalInput` where needed).

## Context
- `docs/DATA_MODEL.md` §3 (Category, PaymentDetails, Supplier, Link), `docs/PAYMENT_TYPES.md`
- Existing: `ISupplierStore`, `ICategoryStore`, `SupplierValidator`, `LinkValidator`, `CategoryValidator`,
  `PaymentDetailsValidator`, `Iban`, `PaymentPurpose`, `BuiltInCategories`, T-012 helpers and page structure.
- `CLAUDE.md` startup-order rules.

## Scope — do

### 1. Platform services (interfaces in ViewModels, implementations in App)
- `ILauncherService.OpenAsync(Uri uri)` → `Launcher.Default.OpenAsync` (system browser; the browser's own
  password manager handles logins — we never store passwords).
- `IClipboardService.SetTextAsync(string text)` → `Clipboard.Default`.
- `IToastService.ShowAsync(string message)` → CommunityToolkit.Maui `Toast` (short).

### 2. Suppliers
- Shell: new flyout item **Suppliers** after Objects (route `Suppliers`, localized title, glyph).
- `SuppliersViewModel` — list (Name, number of links, has payment details icon), search box filtering by name
  (case-insensitive, contains), Add/Select, two-pane like Properties.
- `SupplierEditViewModel` — Name, Registration number, Phone, Email, Notes;
  **Links**: rows (localized kind, title or kind name, URL), add/edit inline (kind picker, title, URL),
  remove (confirm), **Open** button per row → `ILauncherService` (only for valid http/https URLs; invalid URL
  shows the validation message instead);
  **Payment details**: Recipient, Recipient reg. no., IBAN (shown formatted with `Iban.Format`, stored
  normalized), BIC, Bank, Purpose template; **live purpose preview** rendered with `PaymentPurpose.Render`
  using a sample context (customer number "12345", period = current month, amount 59.49, property "Sample")
  — label "Example:"; **copy buttons**: Copy IBAN, Copy recipient, Copy purpose example → clipboard + toast
  "Copied".
  Validation on save via `SupplierValidator` (includes links and payment details), errors under fields.
- Phone/email are plain text for now (no dial/mail actions).

### 3. Categories
- Settings page gets a row **Categories** → navigates to `CategoriesPage` (route `Categories`).
- `CategoriesViewModel` — list ordered by `SortOrder`: colour dot, display name (built-in: `Name` if set,
  else localized `Enum_ServiceKind_<Kind>`; user: `Name`), "built-in" badge, archived badge; "Show archived".
  Add → editor for a new user category (`SystemKey = null`, `SortOrder = max + 1`, colour = next unused
  palette colour).
- `CategoryEditViewModel` — Name (for built-ins: empty = use the default localized name, with a hint showing
  it), Colour: palette of 12 swatches + hex entry (`#RRGGBB`), Notes, Archived. Built-ins cannot be deleted
  (no Delete button); user categories can be deleted (soft) with confirm.
- Resx: names for all `ServiceKind` members (EN + RU) — they are the default category names; `LinkKind`
  members; all new labels and error messages for codes used by these validators.

## Scope — do NOT
- No contracts (T-014). No phone/email actions. No category reordering by drag.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Supplier with links and payment details can be created/edited/deleted; links open in the system browser on
   Windows and Android; copy buttons put the text on the clipboard.
3. Categories: rename a built-in (and clear the name to get the default back), recolour, add/archive/delete a
   user category; 16 built-ins visible after first start.
4. Phone: list → editor page; tablet landscape / wide window: two panes.

## Tests required (ViewModels.Tests, fakes for stores and platform services)
- `SupplierEditViewModelTests`: save with links + payment details; IBAN `lv80 bank 0000 4351 9500 1` → stored
  `LV80BANK0000435195001`, displayed `LV80 BANK 0000 4351 9500 1`; invalid IBAN error; invalid URL blocks
  Open and shows error; Open calls launcher with the URI; purpose preview updates when the template changes
  (`Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}` → `Abon. Nr. 12345, 10.2026` with fake clock 2026-10-20);
  copy calls clipboard + toast; remove link confirm.
- `SuppliersViewModelTests`: search filter; order by name.
- `CategoriesViewModelTests`: built-in display name localized when `Name` null, user name otherwise; archived
  hidden; new category gets next colour and max+1 sort order.
- `CategoryEditViewModelTests`: built-in has no delete; clearing name stores null; bad hex shows error.

## Manual test steps for the owner
1. Pull, run on Windows. Menu → **Suppliers** → Add "Water utility" with links: Website
   `https://example.com`, Customer portal `https://example.com/portal`. Press **Open** → browser opens.
2. Payment details: IBAN `LV80BANK0000435195001` (public example), purpose template
   `Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}` → example shows "Abon. Nr. 12345, 10.2026". Copy IBAN → paste
   in Notepad.
3. Enter a wrong IBAN (change one digit) → error under the field, Save blocked.
4. Settings → **Categories**: 16 built-ins. Rename "Water" → "Вода Рига", change colour; clear the name →
   default name returns. Add "Kindergarten", archive it, show archived, delete it.
5. Restart → everything kept. Short check on Android phone + tablet (Open link, copy).
