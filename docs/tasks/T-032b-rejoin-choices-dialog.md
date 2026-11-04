# T-032b: Re-join after restoring an older backup; readable sync choice dialogs
Phase: 9 · Depends on: T-032 (#213) · Size: S

Owner test of T-032, step 5 (2026-11-04, Windows): restore a backup on the PC → switch sync on → expected the C9 choice
"Replace this device's data with the data from the cloud" / "Replace the data in the cloud with this device's data".
Got the generic first-join question (Replace / Merge) **without** "Replace the data in the cloud", and its text was cut off
after two lines (screenshot: "…а записи, созданные отдельно на" — the rest is missing).

## Causes (lead's review)
1. `BackupService.MarkSyncNeedsRejoin` marks only a restored database **that itself had joined** sync ("A copy of a device
   that never joined is left alone"). The owner restored a copy made before sync was switched on → no `DeviceId` in it →
   the device looks new → generic first join. What matters is whether **this device** was syncing before the restore.
2. The first-join / re-join questions use `IDialogService.ChooseAsync` → the system action sheet, whose title is clipped
   on Windows.

## Scope — do
1. **Restore on a syncing device:** before the restored database replaces the live one (and for a JSON import), read the
   **current** sync state: if the current database had joined, carry its join identity into the restored file (device id,
   account e-mail, cloud generation, `Enabled=0`, `NeedsRejoin=1`; applied sequences and outbox from the restored file are
   reset — they belong to an older moment). Then switching on shows the C9 choice as designed. Keep the old behaviour for
   a copy restored on a device that never joined.
2. **First join with data on both sides** (a device that never joined, or after Disconnect / another account): offer
   three choices — "Replace this device's data with the data from the cloud" (recommended, backup first), "Replace the
   data in the cloud with this device's data" (warning: other devices will be asked to replace theirs), "Merge". Cancel =
   sync stays off.
3. **Readable dialogs:** the sync questions (first join, re-join, "cloud replaced on another device") use the app's own
   choice dialog per UI_GUIDELINES §3a (title, the explanation as a wrapping text block, the choices as a list, Cancel / OK)
   — never the system action sheet. Check other `ChooseAsync` calls with long titles and list them in the PR.
4. Guide EN + RU: Backup → Restore and sync (older copies too), Sync → first join (three choices). Exact resx wording.

## Tests
Restore of a pre-sync copy on a joined device → `NeedsRejoin` with the current device id/account/generation; restore on a
never-joined device → unchanged; first join with data on both sides → three choices, ReplaceCloud works from first join;
dialog view model gets the full text.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green. UI checklist of CLAUDE.md in the PR.
2. Owner, Windows: restore an older backup → switch sync on → the two C9 choices, full text readable → "Replace the data
   in the cloud…" → phone asks on its next sync.

## Manual test steps for the owner
1. PC (sync on): Settings → Backup → restore an older copy (made before sync was on) → restart → Settings → Sync is off.
2. Switch on → a full-screen/popup choice with the whole explanation readable and two choices → "Replace the data in the
   cloud with this device's data".
3. Phone: Sync now → "The data in the cloud was replaced on another device…" → Replace → the phone shows the restored data.
