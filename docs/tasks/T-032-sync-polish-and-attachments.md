# T-032: Sync polish (C9–C14) and attachment files sync
Phase: 9 · Depends on: T-031 (#203) · Size: L

Owner tests of T-031 (2026-11-03/04, Windows + phone, real data) passed: first join, both directions, offline, same-record
conflict (later edit wins). This task collects the owner's findings C9–C14 (PROGRESS "Collected …" — read them, they have
the details and the owner's decisions) and adds the sync of attachment **files** (T-031 syncs the `Attachment` rows only).
Read ARCHITECTURE §7–§8, T-031's PR #203, `SyncEngine`, `SyncScheduler`, `SyncSettingsViewModel`, `AttachmentStore`.

**User guide (owner, 2026-11-04): every change the user can see updates `docs/user-guide/en.md` and `ru.md` in this PR** —
Sync chapter (pause/resume, restore and sync, network, messages, cloud files, attachments), Backup chapter (restore switches
sync off), the "?" topics. Use the exact resx wording. List the changed guide sections in the PR.

## Part A — sync polish
1. **C14 pause / resume.** Switching sync off pauses it: keep the device id, applied sequences and outbox. Switching it on
   again on the same account with the same local database resumes without a question (outbox goes up, others' changes
   come down, last writer wins). The first-join question only when this device never joined this cloud, after C9, or after
   Disconnect / another account (store the account e-mail with the join). Reword the first-join dialog: "appear twice"
   applies only to records created separately on both devices; records that were synced before merge by the later edit.
2. **C9 restore and sync.** Restore (file or automatic copy), JSON import and "Delete all data" switch sync off on this
   device and mark it "needs re-join". Switching on again offers: **"Replace this device's data with the data from the
   cloud"** or **"Replace the data in the cloud with this device's data"** (new: this device uploads a fresh snapshot and
   becomes the source; other devices then get the question "The data in the cloud was replaced on another device —
   replace this device's data?" on their next sync, with a backup first). Design the marker for "cloud replaced"
   (e.g. a generation number in the snapshot name/header) and describe it in the PR.
3. **C10 network back.** While the app is in the foreground: sync when connectivity returns (MAUI
   `Connectivity.ConnectivityChanged` behind an interface in ViewModels/Core), and retry every ~60 s while the status is
   "waiting for network" or "failed". Nothing in the background on Android.
4. **C11 UI thread.** `SyncEngine.StatusChanged` (and any other engine/scheduler event) is raised off the UI thread:
   marshal every bindable update and `NotifyCanExecuteChanged` to the UI thread (captured `SynchronizationContext` or a
   dispatcher interface); unsubscribe transient view models from the singleton engine (no leak). Same check for the
   Overview sync hint. A test that raises the event from another thread.
5. **C12 files in the cloud.** Settings → Sync → "Files in the cloud" (read-only): change files grouped by device (short
   id, "this device", count, total size, newest date), snapshots (date, size), attachment files (count, size), foreign
   files; total. Nothing can be deleted there. Guide: where Google Drive shows the hidden app data (drive.google.com →
   Settings → Manage apps → HomeUtilities; "Delete hidden app data" wipes the cloud copy only).
6. **C13 sync messages** (rules agreed by the owner): snackbar 1–2 s, never blocking. "Syncing…" only for Sync now and the
   first sync after start; "Synced: N received, M sent" only when something was received or sent; errors always, the same
   kind only once until the next success. Switch "Show sync messages" (default on). Duration in one constant.

## Part B — attachment files (photos of meters/readings, bill documents)
1. **Upload:** when an `Attachment` row is synced, its file goes to `attachments/<attachmentId><ext>` in the same storage
   (`ISyncStorage`), once (the file never changes; `Sha256` is known). Upload after the change file with the row, retry
   through the outbox if it fails. Big files: stream, do not load into memory.
2. **Download:** a received `Attachment` row whose file is missing locally is downloaded — **automatically on Wi-Fi /
   Ethernet**, on mobile data **only when the user opens it** (setting "Download photos and documents on mobile data",
   default off). Verify the SHA-256; a mismatch is logged and the file re-downloaded once. Until it is there, the
   photo/document place shows "Not downloaded yet" with a Download button.
3. **Deleted attachments:** a soft-deleted `Attachment` keeps its cloud file for 30 days, then the device that deleted it
   removes the cloud file (same rule style as the change-file clean-up). Local files of soft-deleted rows: unchanged
   (known tech debt).
4. **Snapshots/first join:** Replace / new device downloads the attachment files by the same rules (Wi-Fi automatically).
5. Backups (T-029) already contain the local attachment files — no change.

## Tests (in-memory storage, two or three devices, as in T-031)
Pause/resume keeps ids and uploads changes made while paused; re-join after restore; "replace the cloud" → other device
gets the question; connectivity event triggers a sync; retry timer only in foreground and only when waiting/failed; events
from another thread reach the UI thread; cloud file list grouping; message rules (no message for empty automatic syncs,
error once); attachment upload after the row, download on Wi-Fi vs on demand, SHA mismatch, 30-day cloud clean-up.

## Scope — do NOT
No other providers (T-032a), no background work on Android, no change of the merge rule (last writer wins).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. UI checklist of CLAUDE.md and the list of updated guide sections in the PR.
3. Owner, Windows + phone: pause/resume without a question; restore → sync off → "replace the cloud" → the other device
   asks; network back → syncs by itself; short messages not annoying; files in the cloud listed; a meter photo taken on the
   phone appears on the PC.

## Manual test steps for the owner
1. Phone: Sync off → change a bill → Sync on → no question, the change reaches the PC.
2. Phone: airplane mode → change a bill → airplane mode off (app open) → within a minute "Synced: 0 received, 1 sent".
3. PC: Settings → Sync → Files in the cloud → two devices, snapshots, sizes.
4. Phone: take a photo of a meter reading → PC (Wi-Fi): open the reading → the photo is there.
5. PC: restore a backup → Sync is off → switch on → "Replace the data in the cloud with this device's data" → phone: on the
   next sync the question appears → Replace → the phone shows the restored data.
6. Help: the Sync and Backup chapters describe all of the above.
