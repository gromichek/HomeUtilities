# T-037: Database clean-up — remove deleted records for good
Phase: 9 · Depends on: T-029 (backups), T-032 (sync) · Size: S–M

Owner (2026-11-05): while entering his data he made mistakes and deleted records. Every delete is a soft delete
(`SyncEntityInterceptor`: `IsDeleted = true`), nothing is ever purged, and attachment files of deleted attachments stay in
`attachments/`. After the initial data entry he wants to clean the database once, on one device, then bring the result
to the other devices (backup restore or sync "Replace this device's data with the data from the cloud").

## Scope — do
1. **Settings → Backup**: a command "Очистить базу данных…" / "Clean up the database…" (secondary button, not red: it
   removes nothing the user can see). A dialog first shows what would be removed — "Deleted records: 37 · files of
   deleted photos and documents: 5 (2,3 MB)" — and explains in one sentence that deleted records cannot be brought back
   afterwards, except from the backup made now. Nothing to remove → a short message, no clean-up.
2. On confirm:
   1. automatic backup with a new reason `BeforeCleanUp` (same "continue without a copy?" rule as before a wipe);
   2. in one transaction, hard-delete every row with `IsDeleted = true` in all data tables, **bypassing the soft-delete
      interceptor** (raw SQL per table, like `DataExchangeService`/`RowApplier`), child tables first so foreign keys hold;
      rows that still reference a deleted parent are a bug — count them, do not delete live rows, list them in the error
      log;
   3. delete the files in `attachments/` that belong to no live attachment row (deleted rows and orphans);
   4. `VACUUM` the database (after the transaction, with no other connection open — same care as the backup service);
   5. result message: records removed, files removed, database size before → after.
3. **Sync safety** (the owner will clean before switching sync on, but the app must stay safe): if sync is **on**, the
   dialog says that the clean result will be uploaded as the new cloud data and other devices will be asked to replace
   theirs; after the clean-up the existing "Replace the data in the cloud with this device's data" flow runs. If the user
   cancels that, sync is switched off and marked `NeedsRejoin` (as after a restore), so stale cloud changes can never
   bring the purged rows back. Sync off → nothing extra.
4. **Undo snackbars**: a clean-up clears any pending Undo (an Undo must never point at a purged row).
5. User guide EN + RU: Backup → "Clean up the database" — what it does, when to use it (after the initial data entry, now
   and then), that a backup is made first, how to bring the clean data to other devices.

## Not in this task
No automatic or scheduled purge, no recycle bin / restore of deleted records, no change of the soft-delete rule.

## Tests (Data)
Purge removes exactly the soft-deleted rows of every data table and keeps live rows (count per table, concrete numbers);
child-before-parent order with foreign keys on; a live row referencing a deleted parent is kept and reported; orphan
attachment files deleted, files of live attachments kept; nothing to clean → no backup, no change; the backup is made
before any change; with sync on the database ends `NeedsRejoin` when the replace is cancelled. Extend
`SampleDataGenerator` only if a helper for soft-deleted sample rows is needed.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist of CLAUDE.md in the PR.
2. Owner: Settings → Backup → Clean up → the counts → confirm → backup appears in the list, result message, deleted
   records gone, everything else unchanged, smaller database file (About shows the path).

## Manual test steps for the owner
1. Delete two test records (e.g. a payment and a reading with a photo).
2. Settings → Backup → "Очистить базу данных…" → the dialog shows 2+ records and 1 file → confirm.
3. A new automatic copy "before clean-up" is in the backup list; the result message shows the removed counts and sizes.
4. Run it again → "nothing to clean".
5. Everything else (lists, Overview, reports) looks exactly as before.
