# T-023b: Reports polish — choice dialogs, report picker, wheel scrolling, Android spinner; narrower menu
Phase: 6 · Depends on: T-023a · Size: S–M

## Findings (owner, 2026-10-29, screenshots Windows wide + Android phone)
1. **Choice dialogs look foreign** (`MultiSelectDialogView`, `ReportPeriodDialogView`):
   - Windows: a wide empty gap between check box and text; the period presets are a row of buttons that does not fit
     ("Всё вр…" cut off).
   - Android: the sheet shows the title and All/None at the top, then a large empty area, the list **vertically
     centred**, and the buttons at the very bottom; the period sheet has its content at the top and the presets cut off.
2. **Report type buttons** (Costs · Consumption · Unit cost · Calculated vs billed) do not fit on a phone; the owner
   suggests a drop-down on both platforms.
3. **Windows: mouse wheel over the table stops the page scrolling** — the table's horizontal ScrollView takes the wheel,
   so a long table can only be scrolled with the page's scrollbar. Also the table's horizontal scrollbar sits right on
   the last row — more space below the table.
4. **Android: the spinner still keeps turning** after the first load (next to the Table/Chart switch, plus a thin blue
   bar under it); it disappears after switching to another menu item and back. T-023a's fix (busy counter +
   `IsRunning="True"` with `IsVisible="{Binding IsBusy}"`) did not help.
5. (Owner suggestion, all screens) On wide windows the **menu is too wide** for its short labels.

## Scope — do
1. Rework both dialogs to **UI_GUIDELINES §3a** (new section, read it): title top-left, content right under it, list
   rows with the mark directly before the text, buttons bottom-right; phone = full-screen sheet with top title bar,
   scrolling list, bottom button bar; desktop = fixed-width popup with inner scrolling. Period dialog = vertical radio
   rows (This year · Last 12 months · Last year · All · Custom…) with the from/to month pickers shown under "Custom…"
   when selected. Make the shared dialog host (`EditorDialogPage` / `DialogService`) lay out content from the top on
   Android (find why it is centred) — check the other child dialogs still look right.
2. **Report picker**: replace the four buttons by one field at the top, styled like the filter fields:
   "Report: Costs ▾" opening a single-choice dialog (§3a) with the four reports and a one-line explanation each
   ("Costs — what you paid, by month and category"). Keep the selected report remembered.
3. **Wheel scrolling (Windows)**: vertical mouse wheel over the table (and charts) scrolls the page; horizontal
   scrolling of the table stays possible (Shift+wheel, touchpad, scrollbar). Implement in `Platforms/Windows` (e.g.
   handle `PointerWheelChanged` on the inner horizontal ScrollViewer and forward vertical deltas to the outer one, or
   set the inner ScrollViewer's vertical scroll mode/chaining so the wheel chains to the parent) — describe in the PR.
   Add ≥ 24 px bottom padding under the table (scrollbar room) on all platforms.
4. **Android spinner**: find the real cause (suspects: `IsBusy` changes raised off the UI thread — check every await
   on the load path for `ConfigureAwait(false)` and marshal property changes to the UI thread; Android not redrawing
   an `ActivityIndicator` whose visibility changes inside a `ScrollView`; a second indicator/RefreshView producing the
   blue bar). Fix it so the indicator is gone when the table appears, e.g. wrap it per UI_GUIDELINES §6a in a
   `ContentView` bound to `IsBusy` with the indicator's `IsRunning` also bound, or show a small text "Calculating…"
   instead of an animation. VM test: `IsBusy` is false after the first load and after a load that throws, and the
   property change is raised through the UI dispatcher abstraction if one is introduced.
5. **Menu width**: on medium/expanded layouts set the flyout width to fit the longest label in both languages plus
   icon and padding (~200–220 px; measure "Начисления"/"Настройки"/"Platby" style labels — use the longest of EN/RU),
   not the default 320.
6. Guide: Reports section (report picker field, dialogs) in en.md and ru.md if the wording changes.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Dialogs look like the other app dialogs on Windows and Android; nothing cut off.
3. Phone: report picker is one field; Windows: wheel over the table scrolls the page.
4. Android: after the first open of Reports the spinner and the blue bar are gone when the data is shown.
5. Wide window: narrower menu, labels not cut.

## Manual test steps for the owner
1. Windows wide and Android: Reports → Objects / Categories / Period dialogs — list rows, check/radio right before the
   text, buttons bottom-right; Custom period shows the month pickers.
2. Report field → choose each report.
3. Windows: long table (Rows = months, All) → mouse wheel over the table scrolls the page; Shift+wheel scrolls sideways.
4. Android: start the app → Reports → spinner disappears when the table is shown.
5. Wide window: menu narrower, all labels readable in RU and EN.
