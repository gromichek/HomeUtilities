# T-017a: Navigation guard — menu always opens a section's main screen, unsaved-changes check on every exit
Phase: 3 · Depends on: T-017 (can run before it if T-017 has not started) · Size: M

## Problem (owner, 2026-10-24, Windows + Android)
"I edit a contract inside a property, switch to Readings via the menu, then pick Objects again — and I land in that
contract editor. It is confusing." Cause: Shell keeps a separate navigation stack per flyout section; switching
sections keeps the old stack, so the section reopens on the last pushed page. Also: leaving an editor through the
menu does not ask about unsaved changes.

## Decided behaviour (owner + lead, 2026-10-24)
1. **Selecting a menu item always shows that section's root page** (list). Pages pushed inside the section being left
   (contract editor, meter editor, tariff component editor, compact editor pages, history pages…) are closed.
2. **Before any navigation away from a page with unsaved changes** — menu item, toolbar back, Android hardware/gesture
   back, in-app navigation — the standard dialog "Save changes?" (Save / Don't save / Cancel) is shown:
   Save → save, then navigate if the save succeeded (validation errors keep the user on the page);
   Don't save → discard and navigate; Cancel → stay, nothing changes. No unsaved changes → navigate immediately.
3. Two-pane layouts (tablet/desktop): the right-pane editor is part of the section root; returning to the section
   keeps the selected item highlighted and its editor open. Its unsaved changes are guarded the same way when
   leaving the section.
4. **Windows window close (X)** with unsaved changes anywhere in the current section → same dialog; Cancel keeps the
   window open.
5. The menu is never disabled.

## Scope — do
- One central mechanism, not per page:
  - `INavigationGuard` (ViewModels): `Task<bool> CanLeaveAsync()` = asks the **currently active** editable view model
    (topmost page's VM, or the two-pane editor VM) via the existing `EditableViewModel.CanLeaveAsync()`.
  - App: handle `Shell.Navigating` — for every navigation that leaves a page with an editable VM, take a deferral
    (`e.GetDeferral()`), await the guard, `e.Cancel()` when it returns false. Avoid re-entrancy (a second navigation
    while the dialog is open is cancelled).
  - On a **section switch** (flyout/tab: target route differs in the first segment), after the guard approves,
    pop the **source** section's stack to its root (`Navigation.PopToRootAsync(false)` on that section) so the
    section reopens on its list next time; navigate to the target section's root (`//Route`).
  - Remove now-redundant per-page Back handling only where the central guard covers it; keep `OnBackButtonPressed`
    routing into the same guard (Android hardware back does not raise `Shell.Navigating` on the root page).
  - Windows: hook `AppWindow.Closing` (WinUI `Microsoft.UI.Windowing.AppWindow`) in `Platforms/Windows`, set
    `args.Cancel = true` while asking, then close programmatically when allowed.
- Tests (ViewModels): guard asks only the active editor; clean editor → no dialog; dirty + Save valid/invalid,
  Don't save, Cancel outcomes; re-entrancy (second request while dialog open → false).
- Manual-test hooks: `ShellNavigationService` unit-testable parts behind interfaces where reasonable.

## Scope — do NOT
- No draft autosave on Android backgrounding (later, if wanted).
- No visual changes to pages.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner scenario: Objects → property → contract editor → menu Readings → menu Objects → **list of objects**
   (Windows wide: list with the property selected in the right pane).
3. Same scenario with an unsaved change in the contract → "Save changes?" appears when tapping Readings; Cancel stays;
   Don't save goes to Readings and the change is gone; Save saves and goes.
4. Android hardware back and toolbar back behave as before (dialog only with changes).
5. Windows: closing the window with unsaved changes asks; Cancel keeps it open.

## Manual test steps for the owner
1. Windows wide and Android phone: Objects → property → contract → change the title → menu **Readings** → dialog.
   Try Cancel (stay), Don't save (Readings, title unchanged), Save (Readings, title saved).
2. Without changes: contract → menu Readings → no dialog → menu Objects → **object list** (phone) / list + selected
   property (Windows wide).
3. Same with a meter editor and a tariff component (after T-017).
4. Windows: change something, click the window's ✕ → dialog; Cancel keeps the app open.
