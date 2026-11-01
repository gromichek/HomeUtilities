# T-028e: Reliable reminders on Android
Phase: 8a · Depends on: T-028c, fixes #147/#149 · Size: M

Owner tests on a real phone (release build), 2026-10-31 / 11-01. Read T-028 and the battery rules in it first.

## Problem
- Before #149: reminder time = now + 5 min, add a **new bill** due in 2 days, Save, leave the app → **no notification**.
  If after adding the bill the reminder **time** is changed, the notification arrives (~1 min late — fine).
- After #149 (opening Settings → Reminders → **Planned reminders** first calls `ReminderScheduler.RescheduleAsync`, then
  compares the plan with `INotificationService.GetPendingIdsAsync`): **every** row is marked "not handed to the system"
  and **no** notification arrives, whatever the order of the steps.
- What is known about Plugin.LocalNotification 14.1.2 (Android, read its source, tag `v14.1.2`):
  - `Show` → `ShowLater` → `AlarmManager.SetAndAllowWhileIdle` with a broadcast `PendingIntent` (request code =
    notification id) to `ScheduledAlarmReceiver`; the request is serialized into the intent and into a
    SharedPreferences "PendingList". `GetPendingNotificationList` only reads that list.
  - `CancelAll` cancels the alarm intent of every id in the pending list **and calls `PendingIntent.Cancel()`**, clears
    the list and all shown notifications.
  - The receiver drops an alarm delivered later than `AllowedDelay` (we set 6 h in #147; default was 1 min).
  - `Show` returns `false` silently (no exception) when notifications are disabled or the time is not valid; we ignore
    the return value today.
- The exact cause is **not** confirmed. Do not guess: find it, and make the result verifiable on the device.

## Scope — do
1. **Own Android scheduler (recommended).** Replace the plugin's *scheduling* with a small implementation in
   `HomeUtilities.App/Platforms/Android` that we fully control:
   - `AlarmManager.SetAndAllowWhileIdle(RtcWakeup, …)` (inexact, no `SCHEDULE_EXACT_ALARM`) with a broadcast
     `PendingIntent` (`FLAG_IMMUTABLE | FLAG_UPDATE_CURRENT`, request code = notification id) to **our** receiver.
   - The receiver shows the notification with `NotificationCompat` on the existing channel `reminders`, with the
     tap target (`ReminderTarget` string) so that a tap opens the screen exactly as today (`NotificationTapRouter`).
     Late delivery: show if not more than 6 h late, otherwise skip.
   - Store the handed-over plan (id, time, title, body, target) in app preferences/a small JSON file, so that
     **boot** (`RECEIVE_BOOT_COMPLETED`, already allowed) and **app update** re-register the future alarms.
   - Cancel = cancel every stored id's alarm, then clear the store.
   - "Held by the system" check = `PendingIntent.GetBroadcast(…, FLAG_NO_CREATE | FLAG_IMMUTABLE) != null` for each id
     (this asks Android itself, not a list we wrote).
   - Keep using the plugin only if needed for the permission request / channel / test notification, or drop it
     entirely if nothing else uses it (then remove it from `Directory.Packages.props`). Either way only one component
     may schedule alarms.
   If you find a clear, proven cause inside the plugin that a one-line change fixes, you may keep the plugin instead —
   explain the cause with the plugin's code in the PR and still do points 2–5.
2. **Every path that must hand the plan to the system** (list them in the PR and test each):
   app start, resume, any data save (bills, payments, readings, templates/drafts, contracts, meters, properties,
   tax installments, import, restore, sample data, delete all), reminder settings change, language change.
   - Data saves raise `IDataChangeNotifier.Changed` from whatever thread saved; the scheduler must work from any thread.
   - **Leaving the app** (window `Stopped`/`Deactivated` on Android) must run a pending debounced reschedule
     **immediately** (a bill saved 1 s before closing the app must be handed over). Await it; it is short.
   - Rescheduling must never lose reminders: build the new plan first; only then cancel the old alarms and register
     the new ones.
3. **Diagnostics in "Planned reminders"** (Settings → Reminders), kept for the owner:
   - Per row: held by the system ✓ / not held (red, as now) — from the real system check above.
   - Summary: "Handed to the system: M of N. Last update: <local time> (<reason: start / change / settings /
     leaving the app>)".
   - Failures (exception, refused time, notifications disabled) are written to the error log with the reminder id and
     time, so the owner can copy the log (Settings → Error log) and send it.
4. **Notifications disabled in the system** (owner switched them off in Android settings): the Reminders card shows a
   warning with a button that opens the app's notification settings; nothing crashes.
5. Unit tests for the planner/scheduler parts that do not need Android (paths that trigger a reschedule, "flush on
   leave", plan built before cancel, diagnostics texts). Android-specific code stays thin.
6. User guide EN + RU: how reminders are delivered, the check marks, what to do if a reminder is not held.

## Scope — do NOT
- No exact alarms, no foreground service, no periodic background work (`WorkManager`), no wake lock — owner decision
  2026-10-29 (battery). Inexact alarms that are ~1 minute late are fine.
- No quiet hours (removed in #149, owner decision 2026-10-31).
- Windows behaviour unchanged (summary on start).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (linux, windows).
2. Owner, release build on a real phone, each scenario with the app **closed** afterwards (swiped away):
   a) reminder time = now + 5 min, then add a new bill due in 2 days, Save, close the app → notification arrives;
   b) same, but the bill is changed (amount) instead of added → arrives once (no duplicate);
   c) bill marked paid before the time → nothing arrives;
   d) phone restarted before the time → arrives after the restart (if the time has not passed by more than 6 h).
3. Planned reminders shows ✓ for every row in a), b) and the correct "Last update" reason.

## Manual test steps for the owner
1. Settings → Reminders: on, bills on, lead 2 days, time = now + 5 min.
2. Bills → New bill, due date = today + 2 days, amount 10,00 → Save → Settings → Reminders → Planned reminders:
   the row "Bill due … in 2 days" at today's time has ✓; summary "Handed to the system: N of N. Last update: … (change)".
3. Close the app (swipe it away from recent apps). Wait until the time (+ up to a few minutes) → notification. Tap it →
   the bill opens.
4. Repeat 1–3 with the bill **changed** instead of new, then with the bill **paid** (nothing must come).
5. Set the time 10 minutes ahead, restart the phone, do not open the app → notification arrives.
6. If anything fails: Settings → Error log → Copy, send it to the lead.
