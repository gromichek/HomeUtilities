# T-028a: Device polish — layouts on phone, tablet and Windows; Android Back
Phase: 8a · Depends on: T-028c · Size: L

Owner findings on a real phone, an Android tablet and Windows (2026-10-31 / 11-01, PROGRESS "T-028a findings" 1, 2, 4).
Settings are a separate task (T-028d). Read `docs/UI_GUIDELINES.md` first; update it where this task sets a new rule.

## 1. Bills and Payments: the filter block takes the whole screen
The filter card (Property, Period: All years / Year / Month + year switcher, Category or Contract, Status, totals line)
is fixed above the list. Phone portrait: little room for cards; **phone landscape: no card visible at all**; tablet the
same; **Windows**: bad in a normal window, better only when maximised.
- The filters scroll away with the list: put them into the list **header** (`CollectionView.Header`) so the cards get the
  whole height once the user scrolls. Keep "Show more" paging (T-028c) and virtualization.
- On compact width the filter card is **collapsible**: one summary line (e.g. "All properties · 2026 · All categories ·
  All · Billed 78,13 € · Paid 78,13 €") with a filter icon/expander; tap expands the full filters. Remember the
  expanded/collapsed state per screen for the session.
- Medium/expanded (tablet, Windows): filters in a compact row/grid (two or three columns), not one field per line.
- The totals line stays visible (in the summary line when collapsed).
- Check the other list screens with a filter block the same way (Readings, Charges, Reports, Imports) and apply the same
  pattern where the list gets less than about half of the height.

## 2. Two-pane editors on a phone in landscape
Suppliers on a phone in landscape (width ~ 900 dp → `Medium`, height ~ 400 dp) show the tablet footer
"Save / Cancel / Delete supplier"; the buttons **overlap** ("Cancel" under "Delete supplier"). Owner: Save is not the
standard one and not at the top.
- Low height (window height < 480 dp) counts as a **phone**: single pane and the phone editor commands (toolbar ✓ Save,
  Back cancels, Delete in the `IconMore` menu / at the end of the form) — UI_GUIDELINES §3. Implement this in
  `LayoutModes`/`LayoutModeService` (mode from width **and** height; tests in `LayoutModes` tests) and document it in
  ARCHITECTURE §4.9 via the PR description (the lead edits ARCHITECTURE).
- Wherever the footer remains (tablet, Windows): it must never overlap — buttons wrap to a second line or shrink;
  Delete stays at the far end. Check **every** two-pane editor (Properties, Suppliers, Categories, Contracts, Meters,
  Tariffs, VAT rates, Bills, Payments, Templates, Readings), not only Suppliers.

## 3. Texts are cut off
Example: Property → Contracts, switch "Показывать завершённые и архивные" (Show ended and archived) is cut off.
Owner: check this **everywhere**.
- Every switch/check box label wraps (`LineBreakMode=WordWrap`); a long label puts the switch at the end of the
  first line or under the text — never cuts the text.
- Button texts never cut: buttons in a row wrap to the next row or stack vertically on compact width (Settings has the
  same problem — fixed in T-028d, but use the same shared style/layout here).
- Card lines (names, amounts, dates) wrap or ellipsize only where the full text is visible elsewhere (tap → editor).
- Go through **all screens in Russian** (the longer language) at 360 dp width and in landscape; list in the PR what you
  changed per screen. Add the rule to UI_GUIDELINES §6 via the PR description.

## 4. Android system Back minimizes the app on the phone
On the phone the system Back (button/gesture) minimizes the app where the in-app Back works; on the tablet it works.
Phone: Android 16; tablet: Android 13. Two possible causes — find out which (or both) and say so in the PR:
(a) targetSdk 36 on Android 16 → **predictive back**: `onBackPressed`/`OnBackButtonPressed` may not be called any more
(check MAUI 10 docs/issues); (b) owner's view: the tablet uses the two-pane layout (editor in the right pane, like
Windows) while the phone pushes separate pages, and Back is not handled on those pages. Test on the phone in compact
layout **and** on the tablet with pushed pages/dialogs (e.g. a child dialog, Reading history).
- Make the system Back do exactly what the in-app Back does on every page: pushed pages and editors → the same path as
  the toolbar arrow (unsaved-changes dialog, UI_GUIDELINES §4); dialogs/sheets → Cancel; a section root other than
  Overview → go to Overview; Overview → leave the app (Android default).
- Prefer the AndroidX `OnBackPressedDispatcher` / MAUI-supported way; `android:enableOnBackInvokedCallback="false"` only
  as a documented fallback. Explain the cause in the PR.

## Scope — do NOT
- No Settings rework (T-028d), no reminder changes (T-028e), no new packages.
- No change of colours/branding; keep platform icons and button styles (UI_GUIDELINES §1–2).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (linux, windows).
2. Phone portrait + landscape, tablet, Windows (normal window, not maximised): Bills and Payments show cards; scrolling
   moves the filters away; no overlapping buttons in any editor; no cut texts in Russian.
3. Android system Back behaves like the in-app Back on the phone and on the tablet.
4. User guide EN + RU updated (filters collapse, Back behaviour), screenshots/diagrams only if needed.

## Manual test steps for the owner
1. Phone portrait: Bills → the filter card is one line; tap it → full filters; change Year → list updates; scroll → the
   filters scroll away. Same for Payments.
2. Phone landscape: Bills → cards are visible; Suppliers → tap a supplier → editor opens as a page with ✓ at the top,
   Delete in the ⋮ menu; no overlapping buttons.
3. Tablet landscape and Windows (normal window): Bills/Payments filters in two or three columns, cards visible;
   every editor footer: Save, Cancel, Delete do not overlap.
4. Russian UI, phone portrait: Property → Contracts switch text fully visible; go through all screens and look for cut
   texts.
5. Phone: open a supplier, change the name, press the system Back → "Save changes?" dialog (not minimizing). On a
   section root (e.g. Bills) system Back → Overview; on Overview → the app goes to background.
