# T-012: Properties — list, edit, parameters (first data screens + shared UI patterns)
Phase: 3 · Depends on: T-011 · Size: L

## Goal
The first real data screens: list of properties and an editor with time-versioned parameters (area,
residents, …). Because every later screen follows the same patterns, this task also builds the **shared UI
helpers**: localized validation messages, localized enum names, culture-tolerant number input, list/detail
(two-pane) layout, unsaved-changes guard, delete confirmation.

## Context
- `docs/DATA_MODEL.md` §1 (comments), §2 (Property, PropertyParameter)
- `docs/ARCHITECTURE.md` §4.8–4.9 (thin UI, adaptive layout), §5 (localization)
- Existing: `IPropertyStore`, `PropertyValidator`, `PropertyParameterValidator`, `ErrorCodes`, `ViewModelBase`,
  `INavigationService`, `IDialogService`, `ILayoutModeService`, `Routes`, `IClock`.
- Known issue: `AggregateMerger` requires the child's FK to be set (set `PropertyParameter.PropertyId`).

## Scope — do

### 1. Shared helpers (`HomeUtilities.ViewModels.Common`)
- `ValidationText.For(ILocalizer, ValidationError)` → localized message; resx key = `Error_` + code with
  `.` replaced by `_` (e.g. `Error_property_name_required`); format args from `ValidationError.Args`;
  fallback `Error_Generic` = "Invalid value" / "Неверное значение" + code in debug builds.
  Add resx entries for **all error codes used by the property validators** (EN + RU) and `common.notes_too_long`.
- `EnumText.Get<TEnum>(ILocalizer, TEnum)` → resx key `Enum_<Type>_<Member>`; add entries for `PropertyKind`
  and `PropertyParameterKind` (EN + RU).
- `DecimalInput.TryParse(string? text, CultureInfo culture, out decimal value)` — accepts both `,` and `.` as
  decimal separator, ignores spaces (incl. non-breaking) as group separators, rejects letters, empty → false.
  `DecimalInput.Format(decimal value, CultureInfo culture, int maxDecimals)` — no trailing zeros beyond needed
  (e.g. `52.3` → "52,3" in ru-RU).
- `EditableViewModel` base (derives `ViewModelBase`): `IsDirty`, `HasErrors`, `Errors` (field → message
  dictionary for binding), `SaveCommand`, `CancelCommand` (asks "Discard changes?" via `IDialogService`
  when dirty), `DeleteCommand` (confirm). Subclasses implement load/validate/save.

### 2. View models
- `PropertiesViewModel` (route `Properties`): list of `PropertyItemViewModel` (Name, localized Kind, Address,
  current residents and area **as of today** via `Property.GetParameter`, archived badge), `ShowArchived`
  toggle, `AddCommand`, `SelectCommand(item)`, `SelectedProperty` (editor VM for two-pane), reload on appearing.
  Sorted by `SortOrder`, then `Name`.
- `PropertyEditViewModel` (route `PropertyEdit`, query parameter `id`, absent = new):
  fields Name, Kind (picker with localized names), Address, Notes, IsArchived;
  **Parameters section**: grouped by kind (custom by name); each group shows the current value and its
  history (`ValidFrom` descending); `AddParameterCommand` opens an inline form (kind, custom name when Custom,
  value as text → `DecimalInput`, valid from = today by default, note), `EditParameterCommand`,
  `RemoveParameterCommand` (confirm). Validation via `PropertyValidator` on save; errors shown under fields
  (map `ValidationError.Field`, e.g. `Parameters[2].Value`, to the inline row).
  Save → `IPropertyStore.SaveAsync`, then navigate back (compact) or refresh the list (two-pane).
  Delete → confirm "Delete «{name}»? History is kept but hidden." → `DeleteAsync`.

### 3. Pages (App)
- `PropertiesPage`: in **Compact** a list; tap → navigate to `PropertyEditPage`. In **Medium/Expanded**
  list on the left (~40%) and the editor on the right in the same page (no navigation). Switching layout
  mode while editing must keep the entered data (the editor VM instance is reused).
- `PropertyEditPage` (compact) hosts the same editor view (`PropertyEditView` ContentView) used in the
  right pane. Floating/toolbar "Add" button, Save/Cancel in the toolbar.
- Empty state: "No properties yet. Add your first one." with an Add button.
- Replace the placeholder for the Properties section. Remove the language toggle from Overview (now in Settings).
- All texts in resx (EN + RU).

## Scope — do NOT
- No contracts/meters/suppliers on this screen (next tasks). No search. No reordering by drag
  (SortOrder stays as stored; new properties get max+1).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Create, edit, archive, delete a property with parameters on Windows and Android; data survives restart.
3. Phone: list → editor navigation. Tablet landscape / wide Windows window: list + editor side by side; resizing
   while editing keeps the input.
4. Entering `52,3` and `52.3` both work in Russian and English UI.

## Tests required (ViewModels.Tests, fake stores)
- `DecimalInputTests`: "52,3", "52.3", "1 234,56", "1 234,56" (nbsp), "-12.10" → values; "", "abc", "1,2,3" → false;
  format 52.3 in ru-RU → "52,3", in en-US → "52.3".
- `ValidationTextTests`: known code → localized text with args; unknown code → generic.
- `EnumTextTests`: localized name; missing key → member name.
- `PropertiesViewModelTests`: list sorted; archived hidden unless toggled; current residents/area as of
  `IClock.Today`; Add/Select set the editor.
- `PropertyEditViewModelTests`: new property save calls store with entered data and parameter FK set;
  invalid name shows error and does not save; adding residents 2.5 shows error on that row; history order;
  remove parameter → not in saved graph; cancel with changes asks to discard; delete asks to confirm and
  calls `DeleteAsync`.

## Manual test steps for the owner
1. Pull, run on Windows (wide window). Menu → **Objects**: empty state with "Add".
2. Add "Apartment" (kind Apartment, address), add parameters: total area 52,3 from 01.01.2020; residents 3 from
   01.01.2020, residents 2 from 01.06.2026 → list shows "2 residents, 52,3 m²". Save.
3. Add "Cottage". Archive it → disappears; "Show archived" → visible with a badge.
4. Narrow the window while editing → editor stays with your input (opens as a separate page).
5. Close and restart → data is there. Delete the cottage → confirmation → gone.
6. Repeat a short version on Android phone and tablet.
