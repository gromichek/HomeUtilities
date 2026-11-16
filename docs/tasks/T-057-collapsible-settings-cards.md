# T-057: Collapsible cards on Imports and Backup
Phase: 9 · Depends on: T-056 (#344) · Size: S–M · Owner 2026-11-16

## Why
Settings → Imports and Settings → Backup became long lists (import history, profiles, PDF layouts, the copies on this device):
the owner has to scroll to find a setting. Cards that are rarely needed are collapsed to one header line with a short summary;
the main action of each screen stays open.

## Scope — do
1. **One reusable collapsible card** (e.g. `views:CollapsibleCard` — a `SectionCard` whose header row is
   `Grid *,Auto,Auto`: title (`SectionHeader`, wraps) | summary (`MutedLabel`, wraps, may go under the title on compact
   width) | `IconExpand` / `IconCollapse`). The **whole header row** is the tap target (≥ 48 dp), with a screen-reader
   description "expand / collapse <title>". The content is not created / not laid out while collapsed (no ~100 row views
   built for a hidden list). Reuse the pattern already in Sync («Подробности», «Как это работает») — migrating those two is
   **not** part of this task.
2. **Expanded state** is a ViewModel property per card, **remembered per card on this device** across restarts through
   `ISettingsStore` (one key per card, not synced — it is a view preference). Default = the table below.
3. **Imports** (`ImportsPage`):
   | Card | Default | Summary in the header |
   |---|---|---|
   | Import… / Export to Excel | always open (not collapsible) | — |
   | Import history | collapsed | "12 imports · last 10.11.2026" / "none" |
   | Import profiles | collapsed | "3 profiles" / "none" |
   | PDF bill layouts | collapsed | "3 layouts" / "none" |
   | Link repair | collapsed | "nothing to repair" / "found: 2" |
4. **Backup** (`SettingsSectionPage`, backup part):
   | Card | Default | Summary in the header |
   |---|---|---|
   | Save / restore a backup (manual) | always open | — |
   | Automatic backups (settings) | collapsed | "every day · keep 10" / "off" (same wording as the choices) |
   | Copies on this device | collapsed | "8 copies · last 16.11.2026 03:00" / "none" |
   | Database clean-up | collapsed | — |
   | Data export (JSON) | collapsed | — |
   An error of the last automatic backup (`HasLastAutoBackupError`) is shown **in the header summary** in the error style
   too, so it is not hidden by collapsing.
5. Summaries are formatted in the ViewModel with the app culture (dates as elsewhere), counts with correct Russian plural
   forms (use the existing plural helper if there is one, otherwise add one with tests), update when the list changes
   (import, delete + Undo, new copy). All texts in `.resx` EN + RU.
6. `docs/UI_GUIDELINES.md` is the lead's file — propose in the PR a short rule "Collapsible cards" (when to use, header
   layout, summary, remembered state, main action never collapsed); the lead adds it.
7. User guide EN + RU: Imports and Backup sections mention that cards open with a tap on the header and show a summary
   when closed.

## Not in this task
Other settings screens, the Sync section, filters of Bills / Payments, any change of what the cards contain or do.

## Tests
- Summary texts with concrete numbers: 0 / 1 / 2 / 5 / 21 profiles in RU and EN (e.g. RU "1 профиль", "2 профиля",
  "5 профилей", "21 профиль"); history "12 imports · last 10.11.2026"; copies with the last date; auto backup off / on.
- Expanded state: default per card, toggling saves the key, a new ViewModel reads it back; two cards independent.
- Summaries update after a delete and its Undo (profiles or layouts) and after a new copy.
- Existing tests stay green.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green. UI checklist in the PR (Russian, 360 dp, landscape,
both themes). Guide EN + RU updated.

## Manual test steps for the owner (short)
1. Settings → Imports: only the import buttons are open, the other cards show one line with a summary; open PDF layouts →
   the list; restart the app → PDF layouts is still open, the others closed.
2. Settings → Backup: "Copies on this device" closed with the number and the last date; open it → the list.
3. Phone, Russian, portrait and landscape: headers and summaries wrap, nothing is cut.
