# T-047: Reading reminders — own day per contract, one notification per contract
Phase: 9 · Depends on: T-046 · Size: S–M · Owner 2026-11-11

## Why
Today the reading reminder comes on the **first day** of the contract's reading window (`ReminderPlanner`, readings part)
and, if the readings are still missing, again on the **last day**; contracts of one property with the same window are merged
into one notification. The owner wants to choose **one day inside the window** per contract with meters, and **one
notification per contract**.

## Scope — do
1. **`Contract.ReadingReminderDay`** (`int?`, day of month; null = first day of the window, as today). EF migration, sync
   payload/snapshot, backup restore of older backups (null), `SampleDataGenerator` (one contract with a day).
2. **Contract editor**, next to the switch «Напоминать о показаниях» (only when the contract has a reading window and the
   switch is on): choice field **«День напоминания»** (choice dialog, §3a, returns the value, not the text) with
   «В первый день периода» + one row per day of the window in window order (window 28 – 3 → 28, 29, 30, 31, 1, 2, 3; the
   31st is listed but in shorter months the reminder falls on the last day of that month). Hint one line: «Если к последнему
   дню показаний нет, придёт ещё одно напоминание.» EN + RU.
   If the window is changed so that the saved day is outside it, the day falls back to «В первый день периода» (silently on
   save, or a validation message — say which).
3. **Planner**: for each window occurrence the reminder is planned on the chosen day of that occurrence (a window crossing the
   month end: day ≥ start day → in the start month, day < start day → in the next month); **one notification per contract**
   (title «Передайте показания», body «{объект}: {договор} — показания до {последний день}.» or similar, EN + RU), id per
   contract and window so the existing de-duplication works. The **last-day** reminder stays per contract and only when the
   chosen day is not the last day. No reminder when the readings of the window are already entered or the switch is off
   (unchanged). The common time from Settings → Reminders is used (no per-contract time).
4. Reminders page "Запланированные напоминания" and the Overview to-do line show the per-contract entries (check nothing
   else merges them).
5. Guide EN + RU: section 7 Contracts (the field) and section Reminders (rule text: chosen day, one notification per
   contract, last-day reminder).

## Not in this task
No several days per contract, no per-contract time, no change of bill / verification / draft reminders.

## Tests (concrete dates)
- Window 25 – 28, day 26 → reminder on 26 Nov 09:00, last-day reminder on 28 Nov; readings entered on 27 Nov → no
  last-day reminder.
- Window 28 – 3, day 1 → 1 Dec for the November window; day 30 in February (28 days) → 28 Feb.
- Null day → first day (unchanged behaviour).
- Two contracts of one property, same window → two notifications (different ids).
- Day outside the window after a window change → first day.
- Migration / sync / old backup with null.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist (the choice field in the contract editor,
Russian at 360 dp); guide EN + RU.

## Manual test steps for the owner (short)
1. Contract with meters → «День напоминания» → pick a day → ✓.
2. Settings → Reminders → «Запланированные напоминания»: one line per contract on the chosen day (and the last day).
3. Phone: wait for the day (or pick today's day and a time a few minutes ahead) → one notification per contract.
