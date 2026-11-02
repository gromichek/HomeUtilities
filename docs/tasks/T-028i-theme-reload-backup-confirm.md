# T-028i: Windows theme regression, reload only on change everywhere, button widths, confirm without backup
Phase: 8a/9 · Depends on: T-028h, T-029 · Size: M

Owner findings C5–C8 of 2026-11-02 (PROGRESS "Collected for the next session task" — read them, they contain the logs).
Release builds; phone Android 16, tablet Android 13, Windows.

## C5. Windows: the first theme switch after start draws mixed colours (regression)
- Symptom: after the app starts, the **first** theme choice (any direction) leaves the native parts (menu pane, page
  background, pickers) in the old theme while texts switch; every **later** choice works. Owner: theme switching worked
  correctly after T-011 and broke later.
- Facts: `ThemeService` was unchanged from T-011 until #174; #174, #177 and #181 added workarounds (re-apply, delayed
  re-apply, flip to the opposite theme) — none helped. The Debug log (`Theme` entries) shows the native root
  `RequestedTheme`/`ActualTheme` and `Application.RequestedTheme` now follow the choice, so the switch itself happens.
- Do:
  1. **Remove the workarounds** — back to the T-011 `ThemeService.Set` (keep the Debug log lines while investigating).
  2. Find what changed in the **startup order** since T-011 (`git log` of `App.xaml.cs`, `MauiProgram.cs`,
     `ThemeService`, `AppShell`): where the saved theme is applied (App constructor vs `CreateWindow`, before the window
     and the resource dictionaries exist), what T-028b (startup timing), T-028e (reminders in `CreateWindow`) and T-029
     (backup 5 s after start) added there, and whether MAUI raises `RequestedThemeChanged` on the first change. Also check
     the theme-dependent resources added after T-011 (`SettingsSectionPage`, `FilterPanel`, `ListColumns`,
     `AdaptiveFieldGrid`, flyout styles) for `StaticResource` colours instead of `AppThemeBinding`.
  3. Fix the cause so that **the first** choice after start works in every direction (Light ↔ Dark ↔ System), on Windows
     and Android. Explain the cause in the PR. Remove the Debug log lines at the end (or keep them behind Debug only).

## C6. Equal width of stacked buttons in a card (all of them)
Imports (phone): Export / Delete are equal and full width now, but "Load profile from file" and "Cancel import" in the same
cards keep their natural width. Rule: on compact width **every** stacked action button inside a card has the same full
width, whether it is in an `AdaptiveFieldGrid` or not (a shared style or a small stacked-buttons layout). Exception: the
full-row Delete/Save-as-template buttons under the editor cards keep their natural width (T-028h/#177). Check Imports,
Settings sections, Backup, editors' cards, Overview cards. Update UI_GUIDELINES via the PR description.

## C7. Reload only when something changed — every section
`ReloadGate` (T-028h) covers only Overview and Charges; the owner still feels a reload on every visit.
- Apply it to every section root and list: Bills, Payments, Readings (and history), Reports, Properties, Suppliers,
  Categories, VAT rates, Bill templates, Imports, contracts/meters lists on the property page, Settings section values.
- Debug: log "Performance: <VM> skipped (no change)" so the owner can see the gate working; check why Overview/Charges
  still feel reloaded (something saving on every visit — e.g. draft generation, reminder settings, a "last viewed" write —
  bumps the change version; only real data changes may bump it).
- When a section is skipped, it must still show the latest data after a change made elsewhere (e.g. a bill paid from the
  Overview → Bills shows it paid).

## C8. Confirm before continuing without a backup copy
Owner decision 2026-11-02: before an Excel import, a restore (file or automatic copy), a JSON import or "Delete all data",
if the automatic copy fails, ask: "Could not make a backup copy: <reason>. Continue without a copy?" — buttons Cancel
(secondary, the safe default) and Continue (destructive). Cancel stops the operation. The migration copy at startup keeps
only logging. Texts in resx (EN + RU), guide EN + RU (Backup section, one sentence). Tests for both answers.

## Scope — do NOT
No sync/sign-in work (T-030), no new packages, no visual redesign.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Tests: reload gate per section (skip without change, reload after a change / new day / language), version not bumped by
   non-data writes, C8 dialog (cancel stops, continue proceeds).
3. Owner: first theme choice after start correct on Windows; equal button widths in Imports; second visit of any section
   without reload; the dialog when a copy fails (test hint in the PR, e.g. a Debug switch that makes the copy fail).
4. UI checklist of CLAUDE.md filled in the PR.

## Manual test steps for the owner
1. Windows: start the app → Settings → Theme → choose another theme **once** → everything switches (menu, pages, pickers).
   Restart and repeat with the other directions (Light → Dark, Dark → System, System → Light).
2. Phone: Imports → all buttons in a card equally wide.
3. Phone: Bills → Payments → Bills → Overview → Bills: the second visit shows the list at once; Error log (Debug) shows
   "skipped (no change)". Pay a bill on the Overview → Bills shows it paid.
4. (Debug switch from the PR) make the copy fail → Excel import → the question appears; Cancel → nothing imported;
   Continue → import runs.
