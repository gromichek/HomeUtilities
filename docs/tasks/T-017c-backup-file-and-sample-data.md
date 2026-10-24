# T-017c: Backup to a file / restore + sample data for testing
Phase: 3 · Depends on: T-017a · Size: M

## Goal
The owner reinstalls the app on Android while testing and loses everything he typed (uninstall wipes the app's
private folder). He needs (1) a backup he can save outside the app and restore after a reinstall or on another
device (Windows ↔ Android), and (2) a one-tap set of realistic **invented** data so new screens can be tested
without typing. The full JSON export/import and automatic backups stay in T-029.

## Decided (lead, 2026-10-24)

### Backup file
- One `.zip` file, name `HomeUtilities-backup-YYYY-MM-DD-HHmm.zip`, containing:
  - `manifest.json`: format version (1), app version, last applied EF migration id, created at (UTC), device
    platform, counts (properties, readings, attachments);
  - `homeutilities.db`: a **consistent copy** made with the SQLite online backup API
    (`SqliteConnection.BackupDatabase`) — never a raw file copy of a database with an open WAL;
  - `attachments/…`: the attachment files as stored (paths relative to the attachments folder).
- Logic in `HomeUtilities.Data` (`BackupService`: `CreateAsync(Stream)`, `ValidateAsync(Stream)` → manifest or
  error, `ApplyPendingRestore()`); file system access through the existing `AppPaths`-style abstraction so it is
  testable on Linux with temp folders.
- **Create (Settings → Backup → "Save backup…")**: write the zip to the cache folder, then
  - Android: system share sheet (`Share.RequestAsync(new ShareFileRequest …)`) → Drive, mail, Downloads…;
  - Windows: save dialog (`CommunityToolkit.Maui.Storage.FileSaver`); if it fails in the unpackaged app, save to
    `Documents\HomeUtilities\Backups` and show the path.
- **Restore ("Restore from backup…")**: file picker (`FilePicker.PickAsync`, zip) → validate → dialog
  "Restore backup from {date}? Your current data will be replaced. A copy of it is kept." (Restore / Cancel) →
  the file is **staged** (`AppData/restore/pending.zip`) and the app asks to restart:
  "Close and reopen the app to finish restoring." with a **Close app** button (`Application.Current.Quit()`).
- **On startup, before the database is opened**: if `pending.zip` exists → validate again → zip the current
  database + attachments into `AppData/backups/before-restore-YYYY-MM-DD-HHmm.zip` (keep the last 3) → replace the
  database (delete `-wal`/`-shm`) and the attachments folder → delete `pending.zip` → normal startup (migrations
  bring an older backup up to date). Any failure → keep the current data, delete nothing, log it and show
  "The backup could not be restored: {0}" once.
- Refuse a backup whose migration is **newer** than the app knows ("This backup was made by a newer version of the
  app. Update the app first.") and anything that is not a valid backup — never crash.
- Settings card **Backup** shows the date of the last backup created on this device (stored in preferences).

### Sample data (Debug builds only)
- Settings card **Test data**, shown only when compiled with `DEBUG` (`#if DEBUG` in the App, a
  `IsDebugBuild` flag passed to the VM): buttons **Fill with sample data** and **Delete all data** (both confirmed;
  delete-all is the one allowed hard delete — debug only — and removes the database file and attachments, then the
  app restarts like after a restore). Fill is allowed only when there are no properties yet; otherwise a message.
- Generator `SampleDataGenerator` in `HomeUtilities.Data` (uses the stores, deterministic: fixed seed, dates
  relative to today, so tests are stable). **Everything is invented** — names like "Riverside Water", URLs on
  `example.com`, IBANs generated with a valid checksum for a fictitious bank code. Content:
  - Properties: **Flat in town** (apartment, total area 52.3 m², residents 2 → 3 from six months ago),
    **Cottage** (cottage, land area), **Garage**.
  - Suppliers with links (portal, pay online, submit readings, tariffs) and payment details with purpose templates.
  - Contracts: flat — water + sewerage, heating + water heating, electricity, waste (per resident), maintenance
    (per area), internet + TV (in advance); cottage — electricity (dual tariff), land tax (assessed);
    garage — electricity (fixed), property tax. Reading windows (e.g. 25–28 and 28–3), customer numbers.
  - Meters: flat — 4 water meters (cold + hot in kitchen and bathroom; hot meters linked to water **and** heating
    contracts), electricity (single); cottage — dual-tariff meter with an **Export** register (solar);
    one water meter **replaced** five months ago (final + initial readings), one meter with a **roll-over**
    (5 digits, 99 9xx → 00 0xx), one meter with verification **overdue**.
  - Readings: monthly for the last 13 months (realistic amounts, some marked submitted, one estimated).
  - No photos (keep the generator fast).
  - When later tasks add entities (tariffs in T-017, bills/payments in T-019/T-020…), **they extend the generator** —
    add this rule as one line to `CLAUDE.md` under "Tests".

## Scope — do
1. `BackupService` + startup hook + Settings "Backup" card (both platforms) as above; localized texts EN/RU.
2. `SampleDataGenerator` + Settings "Test data" card (Debug only).
3. Tests (Data, temp folders/SQLite files):
   - create → validate → stage → apply round trip restores the same counts and attachment files;
   - backup made while the database has uncommitted WAL pages contains all rows;
   - newer migration refused; garbage zip / non-zip / missing manifest refused; failed apply keeps current data;
   - before-restore copy created, only 3 kept;
   - generator: runs on an empty DB, passes all validators, the expected counts (3 properties, 9+ contracts,
     4 water meters in the flat, one replacement chain, one roll-over), deterministic for a fixed "today",
     refused when properties exist; `ConsumptionCalculator` over the replaced meter gives the expected total.
   - ViewModels: Settings backup/test-data commands, confirmations, debug flag hides the card.
4. User guide (`docs/user-guide/en.md`, `ru.md`): Settings → Backup (how to save it on Android/Windows, how to
   restore after a reinstall, that the current data is kept as a copy). Test data is **not** described (debug only).

## Scope — do NOT
- No automatic/scheduled backups, no JSON export, no Google Drive API (T-029–T-031).
- No encryption (the owner keeps the file himself; mention in the guide that it contains all his data).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Android: fill sample data → save backup to Downloads/Drive → uninstall → install → restore → everything is back,
   including photos added before the backup.
3. Windows → Android transfer with the same file works.
4. A wrong file gives a message, never a crash; the current data is never lost.

## Manual test steps for the owner
1. Debug build, empty app: Settings → **Test data → Fill with sample data** → Objects shows Flat in town, Cottage,
   Garage; Readings has a year of values; one meter shows "Verification overdue".
2. Add a photo to a meter. Settings → **Backup → Save backup…** → Android: choose Drive or Files/Downloads;
   Windows: choose a folder.
3. Android: uninstall the app, deploy again from Visual Studio, Settings → **Restore from backup…** → pick the
   file → confirm → **Close app** → open again → data and photo are back.
4. Pick a non-backup file (e.g. a photo) → message, nothing changes.
5. Settings → Test data → **Delete all data** → confirm → app closes; after reopening it is empty.
