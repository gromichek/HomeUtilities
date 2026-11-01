# T-028f: One content pane everywhere (no list + editor split)
Phase: 8a · Depends on: T-028a, T-028e, #160 · Size: L

Owner decision 2026-11-01 (variant A): on the tablet the list + editor split leaves the list ~450 dp and everything is
cramped (period buttons cut to "Вс/Го/Ме", the year "2026" wraps digit by digit). The app switches to **one content
pane** on every device: the list fills the content area, tapping an item opens the editor as a **separate page** — the
phone pattern, on tablet and Windows too. The menu (flyout) stays as it is (permanently visible on wide windows).

Read UI_GUIDELINES §3, §4, §6a and the T-017a/T-017d/T-028a tasks (navigation guard, selection kept on return, layout
modes) first.

## Scope — do
1. **All list + editor screens** (Properties, Suppliers, Categories, Contracts, Meters, Readings/history where split,
   Tariffs, VAT rates, Bills, Payments, Templates, Imports, and any other screen that hosts an editor in a side pane):
   - The list uses the whole content width. On wide windows (medium/expanded) cards are laid out in **2–3 columns**
     (`GridItemsLayout`, span from the available width, min card width ~360 dp) instead of stretching one card to
     1600 px. Keep virtualization and "Show more".
   - Tap a card → push the editor page (`EditorPageBase`) on every device. "New …": FAB on compact, button above the list
     on medium/expanded (as now).
   - Editor commands are the phone commands everywhere: toolbar ✓ Save (only saves, stays), Back cancels (with the
     unsaved-changes dialog, T-017a), Delete in the `IconMore` menu or as a red "Delete …" at the end of the form.
     The desktop footer (Save / Cancel / Delete) is removed. Windows window ✕ guard stays.
   - Editor pages on wide windows use `AdaptiveFieldGrid` (2–3 field columns) so fields are not 1600 px wide; cards of a
     form may sit in two columns on expanded. No fixed maximum width of the window.
   - Returning from the editor keeps the list's scroll position and highlights the last opened item (T-017d rule).
2. **Remove the side-pane code paths** (`PaneEditorViewModel.IsHostedInPage == false`, "the layout became two-pane while a
   child page covered this page", empty-pane placeholder texts such as "Select a bill or add a new one.") — delete what
   is no longer used, including unused resx keys (both languages). Layout modes stay (they still drive columns, FAB vs
   button, the menu, dialog presentation).
3. **Filters** (Bills, Payments and the other filter cards): the period selector **All years / Year / Month becomes a
   drop-down** like the other filters, followed by the year/month switcher (‹ 2026 ›) that never wraps the year.
   On medium/expanded the filter fields sit in one or two rows across the full width; collapsible summary line on
   compact stays (T-028a).
4. **Dialogs** for child items keep UI_GUIDELINES §3 (popup on wide, full screen on compact).
5. **Docs via the PR description** (the lead edits): UI_GUIDELINES §3 (one content pane, editor commands everywhere),
   ARCHITECTURE §4.9. You update the user guide EN + RU (screens described as list → editor page; remove two-pane
   wording; screenshots/diagrams only if they show the split).

## Scope — do NOT
- No Settings rework (T-028d follows and will use this pattern), no performance work beyond what the change needs
  (T-028b), no new packages, no change of colours/icons.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (linux, windows).
2. Tablet portrait + landscape, Windows normal and maximised, phone: no list + editor split anywhere; lists in columns on
   wide windows; editors as pages with ✓ at the top; nothing cramped or cut (Russian).
3. Unsaved-changes dialog on Back, menu and window ✕ works on every editor (T-017a manual checks).
4. ViewModel tests adapted (no side-pane behaviour left); tests for the period drop-down filter state.

## Manual test steps for the owner
1. Tablet portrait: Bills → list on the whole width, filters with the period drop-down, year "2026" in one line; tap a
   bill → editor page with ✓ at the top; edit, system Back → "Save changes?".
2. Tablet landscape (menu visible): Bills, Payments, Suppliers, Contracts — cards in 2 columns; editor opens as a page.
3. Windows maximised: lists in 2–3 columns; editor fields in columns, not stretched across the screen; ✕ on the window
   with unsaved changes asks.
4. After closing an editor the list is at the same scroll position, the item you opened is highlighted.
5. Phone: nothing changed compared to T-028a except the period drop-down.
