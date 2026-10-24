# T-017d: Hotfix — two-pane screens keep the selected item when the user comes back via the menu
Phase: 3 · Depends on: T-017a · Size: S

## Problem (owner, 2026-10-24, Windows wide window)
Objects → select a property (editor in the right pane) → open a contract from the property's Contracts card (pushed
page) → menu **Readings** → menu **Objects**: the list is shown (correct, T-017a) but the right pane is **empty**
with the hint "Select a property or add a new one." Expected (T-017a, decided behaviour 3): the property stays
selected and its editor open.

The cause is not known yet (no way to run the app in the lead's sandbox). Candidates:
- the section's root page / its transient `PropertiesViewModel` is recreated after `PopToRootAsync` inside the
  `Shell.Navigating` deferral (a new VM has no `SelectedEditor`);
- `Detach` runs for the pane editor somewhere on the way (`OnAppearingAsync`, `EditorFinishedAsync`, layout change,
  `MasterDetailViewModel.CanLeaveAsync`).

## Scope — do
1. Find the cause by reading the code paths above (and MAUI Shell behaviour for `ShellContent` with
   `ContentTemplate` + DI-resolved pages); describe it in the PR.
2. Fix it so it does not depend on the page instance surviving: a singleton `ISectionSelectionMemory`
   (ViewModels; `Remember(sectionKey, Guid? id)`, `Recall(sectionKey)`) used by `MasterDetailViewModel`:
   - remember the id whenever a saved item is selected in the pane; forget it on Delete, on closing the editor
     (Cancel / footer) and when the item is a new unsaved one;
   - on `OnAppearingAsync` in two-pane layout with no `SelectedEditor`: if a remembered id exists and the item is
     still in the list → open it in the pane (no navigation, no dialog), highlight the row;
   - compact layout never auto-opens anything (the menu shows the list — T-017a rule 1).
   Applies to every `MasterDetailViewModel` screen (Properties, Suppliers, Categories, …).
   If the root cause is a simple bug (e.g. a wrong `Detach`), fix that too — the memory stays as the safety net.
3. Tests (ViewModels): two-pane — select, recreate the VM (new instance, same memory) → `OnAppearingAsync` reopens the
   item; deleted / archived-and-hidden item → nothing opens; compact → nothing opens; Delete and Cancel forget;
   new unsaved item is not remembered.
4. User guide: n/a unless the wording in §2 needs a fix (it already says the selection stays).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. The owner scenario above shows the property selected with its editor on the right.

## Manual test steps for the owner
1. Windows wide: Objects → select a property → open a contract → menu Readings → menu Objects → the same property
   is selected and its editor is on the right.
2. Same in Suppliers (select a supplier → Readings → Suppliers).
3. Delete the selected property → Readings → Objects → nothing selected, hint shown.
4. Phone layout: Objects → property → Readings → Objects → only the list (no editor opens by itself).
