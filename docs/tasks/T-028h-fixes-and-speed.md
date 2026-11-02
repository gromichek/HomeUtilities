# T-028h: Collected fixes and speed (Overview, Charges)
Phase: 8a · Depends on: T-028b, T-028g, #177 · Size: M

Owner findings 2026-11-02 (PROGRESS "Collected for the next session task", C1–C4). Release builds; phone Android 16,
tablet Android 13, Windows. Runs in parallel with T-029 — do not touch backup code or Settings → Backup.

## C1. Windows: first switch to the dark theme draws mixed colours
Settings → Theme → Dark on Windows: texts switch to dark-theme colours, but the **menu pane, the page background and the
pickers stay light** (white menu with almost invisible grey texts). Choosing the theme a **second time** fixes it.
Already tried and **not** enough (do not repeat blindly): #174 sets `RequestedTheme` of `Window.Content` + re-applies
`UserAppTheme` after dispatch; #177 applies the native theme again after dispatch and after 300 ms (`ThemeService`).
- Find the real cause: which colours come from WinUI theme resources (NavigationView pane, page, ComboBox) vs MAUI
  styles (`AppThemeBinding`), whether some styles/pages use `StaticResource` colours captured at start, what the second
  choice changes that the first does not (log `Application.RequestedTheme`, `UserAppTheme`, native `ActualTheme` of the
  root and of the NavigationView before/after each choice into the error log in Debug).
- Fix it so **one** choice is enough in every direction (Light → Dark → System → Light). Remove the earlier workaround if
  it is not needed. Android: verify nothing changes.

## C2. Help: wide tables are squeezed
On the phone the tables of the guide are squeezed to the page width and do **not** scroll sideways (zoom works since
T-028g). Each table must keep readable column widths and scroll horizontally **inside its own block** (wrap the `<table>`
in a `div` with `overflow-x: auto`; `table { width: max-content; min-width: 100% }`; cells `white-space: normal` with a
sensible `min-width`). Check the HTML produced by `HelpContentService` and the Android WebView (no `layout_algorithm`
that forces text to fit the screen). Test with the widest table of the guide in Russian.

## C3. Imports → saved profiles: stacked buttons of different widths
Phone portrait: Export / Delete are stacked but have different widths. Stacked buttons always have the **same width**
(fill the card width). Make this the shared rule/style for every stacked button group (Settings sections, Imports,
editors) and check them.

## C4. Speed of Overview and Charges
`Performance:` lines from the owner's phone (Release, real data ~20 years): **Overview 0.5–0.9 s on every appearance**,
**Charges 0.9–1.2 s on every switch**, Reading history 0.4 s (269 items), startup to Overview 2.9 s; Windows: Overview
1.3–1.7 s, startup 3.8 s. Fast menu switching still lags a little.
- **Reload only when needed:** a section root keeps its data and does not reload on appearing if nothing changed since
  the last load (`IDataChangeNotifier` version/counter; language change and date change — new day — also count as a
  change). Overview, Charges, Bills, Payments, Readings, Reports, Properties, Suppliers etc.
- **Profile Overview and Charges** (`OverviewDataLoader`, `TodoBuilder`, settlement 24 months, `ChargeCalculator`
  inputs): one query per entity type, no N+1, no repeated calculation of the same contract/month in one load; cache what
  is reused within a load. Target: Overview < 300 ms and Charges < 400 ms on the phone with real-size sample data.
- **Startup:** show the Overview as early as possible (load its data after the first frame; nothing else on the way).
- Keep the `Performance:` lines; add before/after numbers from the sandbox (sample data of real size) to the PR.

## Scope — do NOT
No backup changes (T-029), no new packages, no visual redesign.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Tests: "no reload when nothing changed", "reload after a change / language change / new day", Overview and Charges
   loader query counts or timings on the large sample (limits only for gross slowdowns, CI machines vary).
3. Owner: one theme choice is enough on Windows; tables scroll sideways; equal-width stacked buttons; Overview/Charges
   appear without a noticeable pause when switching back and forth.
4. UI checklist of CLAUDE.md filled in the PR.

## Manual test steps for the owner
1. Windows: Settings → Theme → Dark **once** → menu, pages, pickers dark; → Light once; → System once.
2. Phone: Help → a chapter with a wide table → the table scrolls sideways, the text stays readable; pinch zoom still works.
3. Phone portrait: Imports → saved profile → Export and Delete equally wide.
4. Phone: switch Overview ↔ Charges ↔ Bills several times → no pause on the second visit; Error log → `Performance:` lines
   only on the first visit or after a change.
