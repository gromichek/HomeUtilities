# T-017e: Fixes after the owner's test of T-017 — VAT code reuse, reorder arrows, double tap opens two pages
Phase: 4 · Depends on: T-017 · Size: S

## Findings (owner, 2026-10-24, Windows + Android)
1. **VAT rate code cannot be reused after delete.** Create a VAT rate (code `LV-TEST`), delete it, create a new one
   with the same code → Save → "Something went wrong… DbUpdateException". Cause: the unique index on
   `VatRates.Code` also covers soft-deleted rows (known issue in PROGRESS). The same applies to
   `Categories.SystemKey` (unique, includes deleted rows).
2. **Reorder arrows in the contract's Tariff card.** With two components, the *disabled* arrows (Up on the first
   row, Down on the last) look exactly like the enabled ones, and on Windows a click on a disabled arrow falls
   through to the row and opens the component editor. The owner reads it as "two arrows move, two open the editor".
3. **Double tap opens the same page twice** (Android emulator; possible on any device with a quick double tap):
   tapping a tariff row twice pushes two component editors; Back then has to be pressed twice. Applies to every
   tap that navigates (list rows, child rows, buttons that open pages).

## Scope — do
1. Unique indexes ignore soft-deleted rows: `VatRates.Code` → filter `"IsDeleted" = 0`; `Categories.SystemKey` →
   `"SystemKey" IS NOT NULL AND "IsDeleted" = 0`. New EF migration (generated with `dotnet ef`, not hand-written).
   Check the VAT editor's own uniqueness validation (code must be unique among **not deleted** rates) gives a field
   message, so a duplicate never reaches the database. Tests (Data): create → delete → create the same code
   succeeds; two active rates with the same code are still refused by the database; same for SystemKey.
2. Reorder arrows: a disabled arrow is **hidden** (`IsVisible="{Binding CanMoveUp}"` etc., keeping its column width so
   the other arrow does not jump) — and a tap on the arrow area never reaches the row's tap gesture (the arrow
   column is outside the tappable area of the row, or the buttons sit in a grid column that has no
   `TapGestureRecognizer`). One component → no arrows at all.
3. Double navigation: `INavigationService` ignores a new **push** (`GoToAsync` to a non-section route) while a
   previous navigation is still running; implement the gate in a small class in ViewModels (`NavigationGate`, unit
   tested: second call while the first is pending is ignored; after completion the next call goes through; an
   exception in a navigation releases the gate) and use it from `ShellNavigationService`. Section switches and Back
   keep working as now (the T-017a guard handles them). Also make editor-opening commands in list VMs non-reentrant
   where they await navigation (`[RelayCommand]` default `AllowConcurrentExecutions = false` already does this for
   async commands — verify the row commands are async and not `async void`).
4. User guide: n/a (no visible change apart from hidden arrows).

## Scope — do NOT
- Nothing else from the T-017 review; T-018 runs separately.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner: VAT rate create → delete → create again with the same code saves fine.
3. Tariff card with 2 components shows only "down" on the first row and only "up" on the last; clicking where a
   hidden arrow would be does nothing.
4. Quick double tap on a tariff row / property row / meter row opens one page; one Back returns.

## Manual test steps for the owner
1. Settings → VAT rates → New → code `LV-TEST`, a version → Save → Delete → New with `LV-TEST` again → Save: no error.
2. Contract with two tariff components: arrows as in criterion 3; reorder works; tap on the row opens the editor.
3. Android: double tap a tariff row, a property, a meter → one page; one Back.
