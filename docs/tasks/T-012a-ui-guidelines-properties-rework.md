# T-012a: Apply UI guidelines — icons, button styles, dialogs, Back; rework Properties screen
Phase: 3 · Depends on: T-012 · Size: M

## Goal
After the owner's test of the Properties screen (too many identical buttons, unclear what belongs to what,
non-standard dialog texts, Android bugs), introduce the shared UI building blocks from
`docs/UI_GUIDELINES.md` and rework the Properties screen to be the reference implementation.

## Context
- **`docs/UI_GUIDELINES.md` — read fully, it is the specification.**
- T-012 code: `PropertiesPage`, `PropertyEditPage`, `PropertyEditView`, `PropertiesViewModel`,
  `PropertyEditViewModel`, `EditableViewModel`, `DialogService`, `Styles.xaml`.
- Owner's screenshots (described): every button is a filled blue button; parameter "Edit/Remove" far to the
  right; inline parameter form with its own "Cancel" next to the editor's "Cancel"; tiny "Add" in the Windows
  toolbar.

## Scope — do

### 1. Icons (guidelines §1)
- Add **Material Symbols Outlined** font (TTF, from the official `google/material-design-icons` GitHub repo,
  Apache 2.0) to `Resources/Fonts/`, register it in `MauiProgram` (`ConfigureFonts`), add `LICENSE` next to it.
- `Resources/Styles/Icons.xaml`: one `FontImageSource`/glyph resource per key from the guidelines table with
  `OnPlatform` (Android → Material Symbols ligature/codepoint, Windows → `Segoe Fluent Icons` with
  `Segoe MDL2 Assets` fallback). Merged in `App.xaml` **after** Colors/Styles.

### 2. Styles (guidelines §2)
`PrimaryButton`, `SecondaryButton`, `DestructiveButton`, `IconButton` (transparent, 40×40 hit area, accent
or red tint) in `Styles.xaml`, light + dark. The implicit `Button` style becomes the **secondary** look
(so a forgotten style never produces a primary button). Card style `SectionCard` (Border with rounded
corners, surface colour, padding) and `SectionHeader` (title + optional header button).

### 3. Dialogs (guidelines §5)
- `IDialogService` gets `Task<UnsavedChangesChoice> AskUnsavedChangesAsync()` (enum `Save`, `DontSave`,
  `Cancel`) — implemented with `DisplayActionSheet` or a small custom popup (CommunityToolkit.Maui `Popup`)
  so three buttons are possible on both platforms.
- Child-item editor dialog: generic `IDialogService.ShowEditorAsync(EditorDialogViewModel vm)` (popup on
  Medium/Expanded, full-screen modal page on Compact) returning true when saved.
- Replace all existing dialog texts with the standard wordings and resx keys from §5 (EN + RU); remove the
  old "Discard/Continue" texts.

### 4. Back behaviour (guidelines §4) — fixes owner bug "could not leave the page with Back on Android"
- `EditableViewModel`: `Task<bool> CanLeaveAsync()` — not dirty → true; dirty → ask unsaved-changes dialog:
  Save → save, true if saved; Don't save → true; Cancel → false.
- `PropertyEditPage`: hardware back (`OnBackButtonPressed`) and toolbar back (`Shell.BackButtonBehavior`
  command) both call `CanLeaveAsync` and navigate back when true. After Save/Delete the page closes itself.
  `IsDirty` is reset after Save/Delete/Load. Remove the toolbar "Cancel" on phones.
- Unit-test every combination.

### 5. Rework Properties screen (guidelines §3)
- List: on Compact a floating `IconAdd` button (bottom-right, above the system navigation bar); on
  Medium/Expanded a "＋ New property" button above the list; remove the toolbar "Add". Remove the stray "≡"
  floating button seen on Android (find where it comes from — likely a leftover/FAB or Shell flyout icon —
  and fix it).
- Editor: cards **General** (name, kind, address, notes, archived) and **Parameters** (header with
  `IconAdd`). Parameter groups: kind name, current value line ("54,7 m² с 20.10.2026"), collapsible
  **History (n)**; each history row: tap → edit dialog, `IconDelete` at the end.
- Parameter add/edit opens the child editor dialog (title "New parameter" / "Edit parameter", fields kind,
  custom name, value, valid from, note; Save primary + Cancel).
- Footer on Medium/Expanded: Save (primary), Cancel (secondary), "Delete property" (destructive, far end).
  On Compact: toolbar Back + `IconSave`; "Delete property" as destructive button at the end of the form.
- **Android bug**: the Kind picker shows empty although a kind is selected — fix (bind `SelectedItem` to the
  same option instance from `ItemsSource`, or `SelectedIndex`) and add a VM test that the selected option is
  one of the items.
- No maximum width for the window/panes.

## Scope — do NOT
- Do not touch other screens' logic (Overview/Settings only get the new button styles if they have buttons).
- No new data features.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Properties screen matches guidelines §3 on phone, tablet and Windows; icons are platform-native.
3. Back always works (with/without changes, after archive toggle, after save, after delete) on Android.
4. Kind picker shows the selected kind on Android.
5. Dialog texts are the standard ones in both languages.

## Tests required (ViewModels.Tests)
- `CanLeaveAsync`: clean → true without dialog; dirty+Save valid → saved, true; dirty+Save invalid → false,
  errors shown; dirty+Don't save → true, nothing saved; dirty+Cancel → false.
- After Save and after Delete `IsDirty == false` and the editor requests close.
- Toggling Archived marks dirty; Back after toggling asks the dialog (owner's bug scenario).
- Parameter dialog: Save adds/updates the row; Cancel changes nothing.
- Kind options: `SelectedKind` is reference-equal to an element of `KindOptions` after load.

## Manual test steps for the owner
Use the checklist in `docs/UI_GUIDELINES.md` §7 plus:
1. Windows wide: "＋ New property" above the list; editor has cards; parameters have ＋ in the header, rows
   with trash icon; footer Save (blue) · Cancel · Delete property (red, at the far end).
2. Add a parameter → a dialog "New parameter" opens with its own Save/Cancel.
3. Change the name and click another property in the list → "Save changes?" with Save / Don't save / Cancel.
4. Android phone: open a property, toggle Archived, press Back → "Save changes?"; choose each option once.
   Open again without changes → Back leaves immediately. Kind shows the selected value.
5. Android: no strange "≡" button; "＋" floating button on the list.
6. Dark theme + English: everything readable.
