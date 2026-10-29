# T-028: Reminders — local notifications for readings, due bills, verification, drafts
Phase: 8 · Depends on: T-026 · Size: L

## Goal
The app reminds the owner even when it is closed: "Submit readings to City Water — window open 25–28", "Bill
Internet 22,95 € due in 2 days", "Water meter Kitchen: verification due in 60 days", "2 draft bills to confirm".
The rules are the same as the Overview to-do (T-022), so they live in Core and are only *scheduled* by the platform.

## Context
- `docs/DATA_MODEL.md` §9 (ReminderRule), ARCHITECTURE "Notifications" (`INotificationService`; Android/iOS:
  **Plugin.LocalNotification** — MIT, allowed new dependency; Windows: Windows App SDK app notifications
  `Microsoft.Windows.AppNotifications` — part of the Windows App SDK already referenced).
- `TodoBuilder` / `OverviewDataLoader` (T-022), `BillTemplate.RemindDaysBefore` (T-025), contract reading windows,
  `Meter.GetVerificationDue()`, drafts. UI_GUIDELINES §6a: **never** use CommunityToolkit Toast/Snackbar for this.

## Battery and Windows (owner decision 2026-10-29)
- **Android: minimal battery use while the app is closed.** Nothing of ours runs in the background: no foreground or
  background service, no periodic `WorkManager`/`JobScheduler` polling, no wake locks, no exact alarms
  (`SCHEDULE_EXACT_ALARM` / `USE_EXACT_ALARM` are **not** requested), no network. The plan is computed only while the
  app is open and handed to the system as **pre-scheduled inexact notifications** (Plugin.LocalNotification →
  AlarmManager); between them the phone does nothing for us. After a reboot only the plugin's boot receiver re-registers
  the stored notifications (no database work). Verify the plugin configuration does this (no exact alarms, no repeating
  background task) and list the resulting Android permissions/receivers in the PR.
- **Windows:** notifications while the app is closed are **not required**. On app start (and while it runs) show the
  reminders that are due now or were missed (one system notification or the in-app snackbar + Overview); no scheduled
  toasts, no background task.

## Scope — do
1. **Core** `ReminderPlanner.Plan(data, settings, now, horizonDays = 45)` → list of `PlannedReminder { Key (stable,
   e.g. "readings:{contractId}:2026-11"), At (local DateTime), Kind, Title, Body (localization keys + args),
   Target (route + params) }`, sorted, at most 60:
   - **Readings**: at the window start day, at the configured time; one per property+window (contracts with the same
     window merged); skipped when the readings of that window are already entered.
   - **Bill due**: `LeadDays` before `DueOn` (default 2) and on the due day itself, only open non-draft bills;
     template bills use the template's `RemindDaysBefore` if set.
   - **Verification**: 60 days before and on the due date.
   - **Drafts**: once a day (configured time) when there are drafts to confirm, only on days when new drafts appeared
     or at most every 3 days.
   - **Tax installments**: as bill due (kind `TaxInstallment` text).
   - Times in the past are skipped; quiet: nothing between 21:00 and 08:00 (move to 08:00 / the configured time).
   Unit tests with concrete dates for each kind, merging, skipping, quiet hours, horizon, max count, stable keys.
2. **Settings** (stored in the existing settings/preferences mechanism, not in the synced DB): Reminders on/off;
   per kind on/off; time of day (default 09:00); bill lead days (0–14, default 2); "Send a test notification".
   Contract editor: switch "Remind me about readings" (default on) — store as `ReminderRule` per contract (entity
   from §9, migration) or a contract flag if simpler; describe the choice in the PR.
3. **Platform** `INotificationService` (ViewModels interface: `RequestPermissionAsync`, `ScheduleAsync(list)`,
   `CancelAllAsync`, `IsSupported`):
   - Android: Plugin.LocalNotification (POST_NOTIFICATIONS permission on Android 13+, request when the user enables
     reminders; channel "Reminders"; notifications survive reboot as the plugin supports; inexact alarms are fine).
   - Windows (unpackaged): on start and while running, show due/missed reminders (see "Battery and Windows"); use
     `AppNotificationManager` only if it works unpackaged without extra registration, else the in-app snackbar. Must
     not crash if notifications are unavailable (`IsSupported = false` → settings shows "Notifications are not
     available on this device; reminders are shown when the app is opened").
   - Reschedule (cancel + schedule the new plan) on app start, on resume, after saving readings/bills/payments/
     templates/settings (debounced, e.g. 2 s) — through one `ReminderScheduler` service in ViewModels (unit tested with
     a fake notification service: idempotent, debounced, disabled = cancel all).
4. **Tap on a notification** opens the app on the target: Readings (property + date), the bill, the meter, Bills
   filtered to drafts. Cold start and running app both work (route passed via notification data).
5. **Guide** EN + RU: section **Reminders** (what is reminded and when, quiet hours, settings, permission on Android,
   Windows limitation if any). Remove "reminders" from "Coming soon".

## Scope — do NOT
- No e-mail/SMS, no server push, no iOS (T-033), no calendar export.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; new package + transitive licences named.
2. Android: no background service / exact alarm / periodic work in the merged manifest (listed in the PR); test notification appears; a bill due tomorrow produces a notification at the configured time; tap opens
   the bill; reminders still fire after a reboot.
3. Windows: test notification appears; no crash when notifications are disabled in Windows settings.

## Manual test steps for the owner
1. Settings → Reminders → on (Android asks for permission) → **Send a test notification**.
2. Sample data: set the time to 2 minutes from now → wait → notifications for due bills/readings appear; tap one → the
   right screen opens.
3. Pay the bill → reminders rescheduled (no notification for it any more).
4. Contract → "Remind me about readings" off → no readings reminder for it.
5. Android: reboot the phone → a reminder scheduled for later still appears. Windows: notifications off in Windows →
   app works, Settings says notifications are not available.
