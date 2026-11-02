# T-031: Sync engine — change logs, merge, first join, "Sync now"
Phase: 9 · Depends on: T-030 (sign-in, `ISyncStorage`, `GoogleDriveStorage`), T-029 (`DataExchangeService`, backups) · Size: L

Owner's goal: change data on the PC, see it on the phone, and the other way round. Read ARCHITECTURE §7–§8, DATA_MODEL §1
(SyncEntity) and §10 (AppSetting: `DeviceId`, applied sequences), T-029's `DataExchangeService` (row shape, table/column
model) and T-030's `HomeUtilities.Sync` first. The storage stays behind `ISyncStorage` (Google Drive first; other providers
later, T-032a). Attachments are T-032 — rows of `Attachment` sync here, their files do not yet.

## 1. Model (as in ARCHITECTURE §8 — keep it, refine where needed and say so in the PR)
- **Device id:** stable `DeviceId` (GUID) per installation, created on first use, stored in app settings (not synced).
- **Outbox:** local changes are captured when they are saved — the `SyncEntityInterceptor` records `(table, id)` of every
  row it writes into a local outbox (a table or a settings-backed list; not a `SyncEntity`, never synced) unless the save
  is an **apply of remote changes** (flag/scope), so received rows are not echoed back.
- **Change log files:** `changes/<deviceId>/<sequence:D10>.json` (optionally gzip), `{ formatVersion, appVersion,
  lastMigration, deviceId, sequence, createdAt, rows: { "<Table>": [ {row} ] } }`, rows in the **row shape of
  `DataExchangeService`** (full row incl. `UpdatedAt`, `IsDeleted`; reuse its table/column model and converters).
  Upload in batches (e.g. ≤ 500 rows per file); a file is never changed after upload.
- **Apply:** for each other device, download the files with a sequence above the one already applied (stored per device
  locally), apply rows per table in dependency order inside one transaction per file, then store the new sequence.
  **Merge per row: last writer wins by `UpdatedAt`; on an equal `UpdatedAt` the higher `deviceId` wins** (deterministic);
  a soft delete is a normal change (newer wins). A row that exists only as a soft-deleted row locally and comes back newer
  is updated in place (**resurrect** — PROGRESS known issue). Applying the same file twice changes nothing (idempotent).
- **Schema versions:** a file with a newer `formatVersion` or `lastMigration` than this app is **not** applied: the sync
  stops with "Update the app on this device to continue syncing" (nothing partially applied).
- **Unique indexes / natural keys** (e.g. VAT code, category name, anything with a unique index): list them in the PR.
  If a remote row violates one (two devices created "Water" categories), do not fail the whole sync: apply what you can,
  keep both or rename deterministically (decide per index, explain), log the conflict and count it in the status.
- **Snapshot:** after every N files (e.g. 200) or on request, a device writes `snapshot/<deviceId>-<createdAt>.json.gz`
  (full export in the same row shape + the sequence of every device it contains). A new device starts from the newest
  snapshot and then applies newer change files. Old change files covered by a snapshot older than 30 days may be deleted
  by their own device. Foreign/unknown files in the folder are ignored (OneDrive/Dropbox folders are visible to users).
- **Clock skew:** `UpdatedAt` comes from device clocks. Warn in the status if the device clock differs from the server
  time (`Date` header) by more than 5 minutes; do not try to correct timestamps.

## 2. First join (decision — see the lead's question to the owner; default below)
When an account is connected and sync is switched on for the first time on a device:
- **Cloud folder empty** → upload this device's data as the first snapshot ("This device starts the sync").
- **Cloud has data and this device has data** → ask: **"Replace this device's data with the data from the cloud"**
  (recommended; an automatic backup of the current data is made first, T-029) or **"Merge"** (keeps both; warns that the
  same records entered on both devices will appear twice). Cancel = sync stays off.
- **Cloud has data, this device is empty** → download without asking.

## 3. When it syncs (battery rule: nothing in the background on Android, owner decision 2026-10-29)
- App start / resume (after the first screen), ~10 s after the last saved change (debounced), when leaving the app (flush
  of a pending upload, like the reminders in T-028e), and **Sync now**. Never two syncs at once; a sync never blocks the UI.
- After applying remote changes: notify `IDataChangeNotifier` (screens and the reload gate refresh, reminders re-plan).
- Offline / provider errors: keep the outbox, retry on the next trigger; no error dialogs during automatic syncs — only
  the status line.

## 4. UI — Settings → Sync (extend the T-030 page)
Switch **Sync on this device**, status line ("Synced 18:42", "Waiting for network", "Error: …", "N conflicts"), **Sync now**,
last sync time, the account (from T-030), and a small "Details" list (last uploaded/applied files, conflicts with a short
text). First-join dialog as in 2. Disconnect (T-030) also switches sync off and keeps local data. Overview: a small hint
only if the last sync failed for more than a day. UI checklist of CLAUDE.md; guide EN + RU (how sync works, what happens
on conflicts, first join, offline, what is not synced yet: attachment files).

## 5. Tests (no network; an in-memory `ISyncStorage` fake and two or three in-memory "devices" with their own databases)
- create on A → appears on B; edit on B → back on A; delete on A vs older edit on B → deleted; newer edit vs older delete →
  kept; equal `UpdatedAt` → deviceId tie-break; resurrect of a soft-deleted row.
- no echo (received rows are not uploaded again); idempotent re-apply; interrupted upload (exception after k of n files) →
  next sync completes without duplicates.
- unique-index conflict handled as decided; newer `formatVersion`/migration → stops cleanly.
- snapshot + new device C gets the same data as A and B (compare full exports); old files cleanup respects the 30 days.
- first-join paths (empty cloud, replace, merge, empty device).
- sample data (`SampleDataGenerator`) round-trips through sync.

## Scope — do NOT
No attachment files (T-032), no other providers (T-032a), no background work on Android, no real-time push.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. PR: the final file layout, the list of unique indexes and their conflict rule, the trigger list, decisions.
3. Owner, Windows + phone, his real data: first join on the phone with "Replace" → the phone shows the PC's data; a bill
   paid on the phone appears on the PC after "Sync now"/restart; a reading entered on the PC appears on the phone after
   opening the app; airplane mode → changes go up later.

## Manual test steps for the owner
1. PC: Settings → Sync → Sync on this device → status "Synced …" (first device uploads).
2. Phone: Settings → Sync → on → question → "Replace this device's data with the data from the cloud" → after a moment
   Bills/Readings show the PC's data. (A backup of the phone's old data is in Settings → Backup.)
3. Phone: mark a bill paid → PC: Sync now → the bill is paid. PC: enter a reading → phone: open the app → the reading is there.
4. Phone in airplane mode: change a bill → status "Waiting for network" → airplane mode off → it syncs.
5. Edit the same bill on both devices before syncing → after sync both show the later edit.
