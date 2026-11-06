# T-036b: UI review part 2 — choice field, field look, phone dialogs, list layout, delete with Undo, Windows icons
Phase: 9 · Depends on: T-036a (#239), #242–#250 · Size: L

Input: `docs/UI_REVIEW_2026-11-05.md` (R-NN), `docs/UI_REVIEW_T-036a_CHECK.md` (C-NN), `docs/UI_GUIDELINES.md` revision 1
and the list "Collected for the next session task (2026-11-06)" in `docs/PROGRESS.md`. Read each finding for details and
code places. **Do only what is listed here**; T-036c (screen-by-screen usability) follows.

## Scope — do

### 1. Choices (R-03, C-06, C-07, §3a)
- **§3a rule change (owner 2026-11-05, C-06):** a row **without extra inputs selects and closes the dialog at once**; OK is
  shown only when a row has extra inputs (e.g. "Другой период…" with dates). Cancel / Back stay. Update §3a in
  `UI_GUIDELINES.md` (you may edit this one section — the owner decided it).
- One **choice field** control (label + value + chevron, the bordered field look of §2 below) that opens the
  `SingleChoiceDialogView` and returns the **Id/index** (T-036a mechanism). Replace the native `Picker` everywhere
  (~45 places, R-03): forms and filters, both platforms. No match by display text anywhere.
- **C-07** "＋ Новый поставщик…" (and similar "create new" entries) as a distinct action row at the end of the dialog,
  not a radio row.

### 2. Field look (R-07, owner's dark-theme screenshot)
- Bordered fields (Entry, choice field, date/month field, Editor) per §2 / R-07 on both platforms: label above, outline in
  `Outline*` colour, focus in accent; this also removes the black Android underline in the dark theme.
- **C-09** month / date fields look tappable (chevron or calendar icon). **R-09** month / year switcher as described in R-09.
- **C-10** all full dates via the app culture helper (`dd.MM.yyyy` for ru/lv, one format everywhere, §9).

### 3. Phone dialogs and system bars (R-29, R-14, R-30, C-04, owner 2026-11-05)
- Dialogs on the phone get the editor frame: top bar with ← and ✓ (or the dialog's primary action), content scrolls, the
  primary action is never hidden by the keyboard (R-29); reading dialog R-14 / R-30.
- **C-04** status bar icons readable on dialog pages (both themes).
- **Android navigation bar (◁ ○ ▢)** in the page colour with matching button contrast (owner): Android 13/14
  `SetNavigationBarColor` + light/dark navigation icons; Android 15+ is edge-to-edge — draw the page colour behind the bar
  and set `isNavigationBarContrastEnforced = false`. Content must not slide under the bar.

### 4. Lists and layout
- **Date lists in one column** (owner): Bills, Payments always one centred column (max width per §4).
- **R-21 `ListColumns` on Windows** (owner confirmed: Properties show one column in a maximised window). Read the MAUI
  Windows `ItemsView` handler first; likely fix: keep one `GridItemsLayout` and change only its `Span` instead of replacing
  `ItemsLayout`. Catalog lists 1–3 columns on Windows and Android.
- **Compact-by-height only on Android** (owner): a low but wide Windows window keeps the desktop layout.
- **C-11** the ~30 action buttons still at natural width on the phone → full width (§3.3, list in C-11). **R-27** landscape /
  full-screen Windows per R-27. **R-24** comparison card per R-24. **R-40** "Таблица | График" as a segmented control.

### 5. Delete (owner 2026-11-05)
- **Delete only as a red button at the end of the form**; remove Delete from every ⋮ menu.
- **Single delete without a confirmation, with Undo**: snackbar (≥ 56 dp, action ≥ 48 dp — done in T-036a) "Удалено: …
  Отменить". Undo restores **exactly** what the delete changed (the record, its attachments — see T-037a — and rows the
  delete touched, e.g. released payment allocations). Where that cannot be guaranteed (records with dependants, e.g. a
  property, a contract with bills), keep the confirmation dialog with the list of dependants. Test Undo per entity.

### 6. Windows icons (owner 2026-11-06)
- **Toolbar ✓ / ?**: back to the state **before #250** — remove `HookChromeIconScale` / `ApplyChromeIconScale` from
  `AppShell.xaml.cs` (the owner found it looked better before).
- **Menu icons as text**: own flyout item template (`Shell.ItemTemplate` / `FlyoutItem` template) with a `Label` in the
  Material font, the **same glyph code points** as now, 20–24 px, colours as now (both themes), selection highlight and
  the locked-flyout (wide window) look kept. Settings section icons are already text — no change.
- Update the §6a pitfall line in `UI_GUIDELINES.md` accordingly.

### 7. Guide
User guide EN + RU: choice dialogs (one tap), delete + Undo, wording changes. Exact resx wording.

## Not in this task (T-036c)
R-23, R-31, R-32, R-34, R-42, R-47–R-50; C-05, C-08, C-12, C-14, C-15, C-16, C-18.

## Tests
Choice field returns Id/index (two same names); dialog closes on a plain row, needs OK on a row with inputs; Undo per
entity restores the exact previous state (record, attachments, allocations); delete with dependants asks; ListColumns span
rule; date formatting helper.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist of CLAUDE.md in the PR.
2. Owner: no native Picker left; one-tap choices; bordered fields readable in the dark theme; phone dialogs with a top bar,
   Save never under the keyboard; Bills / Payments one column, Properties 2–3 columns on Windows; delete + Undo; Windows
   menu icons sharp, toolbar as before #250; navigation bar in the page colour.

## Manual test steps for the owner (short, in the PR)
Payment editor (choice field, one tap), reading dialog on the phone with the keyboard open, dark theme fields, Bills and
Properties on a maximised Windows window, delete a payment → Undo, Windows menu icons at 100 %, phone navigation bar in
both themes.
