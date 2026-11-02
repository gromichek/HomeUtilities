# T-029: Automatic local backups and full JSON export / import
Phase: 9 · Depends on: T-017c (manual backup file), T-028b · Size: M–L

Before sync (T-030–T-032) the owner's ~20 years of data need an automatic safety net. A manual backup zip exists since
T-017c (`BackupService`: zip with `manifest.json`, a consistent SQLite copy via the online backup API and `attachments/`;
restore is staged as `pending.zip` and applied at the next start; `before-restore-*.zip` copies). Read ARCHITECTURE §7–8,
DATA_MODEL §1 and §10 first. Runs in parallel with T-028h — do not touch Overview/Charges loading or the theme.

## 1. Automatic local backups
- Reuse `BackupService.CreateAsync` (same zip format) into `<AppData>/backups/auto/auto-YYYYMMDD-HHmmss.zip`.
- **When:** (a) at app start **after** the first screen is shown, in the background, if the last automatic backup is older
  than the interval (default **1 day**) **and** data changed since then (`IDataChangeNotifier`/a stored change counter);
  (b) **before** risky operations: an Excel import, a restore, "Delete all data", applying migrations at startup (only if
  the database file exists and a migration is pending).
- **Keep** the last N automatic copies (default **7**) plus the last copy of each of the previous 3 months (monthly
  copies), delete older ones. Never delete manual backups or `before-restore-*`.
- Never block the UI, never run two backups at once; a failure goes to the error log and the Backup section shows
  "Last automatic backup failed: …".
- Settings (stored like other app settings, Settings → Backup): on/off (default on), interval (1 / 3 / 7 days), number of
  copies (3 / 7 / 14) — choice rows per UI_GUIDELINES §3a.

## 2. Backup section in Settings
- Shows the last automatic and the last manual backup (date, size), and a list of the automatic copies (date, size).
- Per copy: **Restore** (same staged restore + confirmation as now; the current data are saved as `before-restore` first)
  and **Save copy…** (save/share the zip like the manual backup, so it can go to Google Drive or a PC).
- Text explains: automatic copies are on this device and are deleted with the app; keep a copy elsewhere from time to time.

## 3. Full JSON export / import
- **Export** (Settings → Backup → "Export all data (JSON)…"): one `.json` file (optionally zipped with the attachments as
  `.zip`) with `formatVersion`, `appVersion`, `exportedAt`, and every entity type as an array of full rows **including
  soft-deleted rows**, `Id`, `CreatedAt`, `UpdatedAt`, `IsDeleted`. Decimals as JSON numbers without loss (or strings —
  decide and document), dates `yyyy-MM-dd`, timestamps ISO-8601 UTC. Same row shape as planned for the sync change logs
  (ARCHITECTURE §8) — put the serializer in a place T-031 can reuse (Core/Data, not the App).
- **Import** of such a file: validates `formatVersion` and content first (report errors, nothing written), then
  **replaces all data** via the same staged mechanism as a restore (automatic backup of the current data first; applied at
  the next start). No merge in this task (merge comes with sync).
- Round-trip test: sample data (`SampleDataGenerator`, all entity types) → export → import into an empty database →
  export again → identical JSON (except `exportedAt`). Test for a file of a newer `formatVersion` (refused with a message).

## 4. Android system backup (check and decide)
`AndroidManifest.xml` has `android:allowBackup="true"`. Android Auto Backup may copy the live SQLite file (with WAL) to the
user's Google account and restore it after a reinstall — possibly inconsistent or old. Add backup rules
(`dataExtractionRules` for API 31+ and `fullBackupContent` for older): **exclude** the live database files and
`backups/` temp files, **include** the newest automatic backup zip if under the Auto Backup limit (25 MB) — or exclude
everything and rely on our own backups; explain the choice in the PR. After a reinstall with a restored Auto Backup zip,
the app offers to restore it (Settings → Backup shows it in the list).

## 5. Guide and docs
User guide EN + RU: automatic backups (when, how many, where, how to restore, keep a copy elsewhere), JSON export/import,
what Android's own backup does. DATA_MODEL/ARCHITECTURE changes → PR description (lead edits).

## Scope — do NOT
No Google sign-in / Drive upload (T-030+), no merge import, no new packages unless needed for JSON (System.Text.Json is in
the framework — use it).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Tests: backup schedule (interval, "only if changed", before import/restore/wipe/migration), retention (7 daily + 3
   monthly, manual and before-restore never deleted), JSON round trip, version check, failed backup reported.
3. `SampleDataGenerator` unchanged or extended if new settings entities are added.
4. UI checklist of CLAUDE.md filled in the PR.

## Manual test steps for the owner
1. Start the app → Settings → Backup: "Last automatic backup: today hh:mm", one copy in the list.
2. Change a bill, restart the app the next day (or set the interval and test time as described in the PR for a quick test)
   → a second copy; after 8 days there are never more than 7 daily copies.
3. Import an Excel sheet → a new automatic copy "before import" appears.
4. Tap a copy → Restore → confirmation → restart → data as at that time; the previous state is in `before-restore`.
5. Export all data (JSON) → save to Downloads/PC → open in a text editor: readable, no surprises.
6. On a test device/emulator only: Import JSON → restart → the same data.
