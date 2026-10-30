# T-028c: Fixes found with the owner's real data (20 years imported)
Phase: 8 · Depends on: T-027a · Size: M–L

Collected from the owner's tests on 2026-10-29/30 (Windows wide + Android emulator) after importing ~20 years of
history (3 properties, ~15 contracts, ~270 monthly rows per sheet). See also PROGRESS "Known issues".

## 1. Long lists freeze the UI
- **Reading history** (Readings → tap a meter → history, ~270 readings per register; also the meter editor's
  readings card): the page freezes while it builds. Load off the UI thread, show "Loading…" (text, not an animated
  indicator — see T-023b), and render with a **virtualizing** `CollectionView` (or chunked rendering like the report
  table, T-023/#133). Same check and treatment for **Bills** and **Payments** lists with "All years", the contract
  editor's Bills/Payments cards (only the last 6 — verify they do not load everything into the UI), Charges month
  switching and Overview refresh. No list may create more than ~100 row views at once.
- Data access: one query per screen load, indexed lookups (no O(n²) over bills/readings); add a perf sanity test
  (20 years × 3 properties × 15 contracts sample, each list VM builds in < 1 s on CI).

## 2. Bills opened from a contract: all cards look selected
Contract editor → "All bills" (`BillsFor`, contract highlighted): tapping one card marks **all** cards of that
contract with the selected border and the selection never clears. Separate "highlighted contract" from "selected
bill" (different visual, or drop the highlight once the user selects a bill); only the selected card gets the
selected border (UI_GUIDELINES §6a explicit base stroke). Test in BillsViewModelTests.

## 3. Scheduled reminders do not arrive on Android (test notification works)
- Add Settings → Reminders → **Planned reminders** (debug list: time, kind, text, target) so it can be checked.
- Verify how Plugin.LocalNotification schedules on Android: if it uses exact alarms, the T-028 manifest removal of
  `SCHEDULE_EXACT_ALARM`/`USE_EXACT_ALARM`/`WAKE_LOCK` breaks it silently — switch to the plugin's inexact scheduling
  (keep the battery rules of T-028: no exact alarms, no foreground service, no periodic work) or keep only the minimum
  permission the receiver needs; document the final permission list in the PR.
- Check the time zone (local vs UTC) of the scheduled times and that the reschedule runs after saves and on start.
- Owner test: bill due in 2 days, reminder time = now + 5 min → notification arrives with the app closed.

## 4. Help: links inside the guide open the browser
Anchor links and links between `en.md`/`ru.md` must scroll inside the Help page (`HelpViewModel.ShouldIntercept` /
`HelpPage.OnNavigating` — the WebView reports them as full URLs like `about:blank#…` or `file:///…#…`); only
`http(s)` links to other sites open the browser. Tests for the URL classification.

## 5. Import: period rule vs reading window
When a sheet uses a reading-date column with the rule "previous month" (or "previous month if day ≤ N") and the
contract has **no reading window** (or one that does not contain those dates), the charges calculation assigns the
reading to the wrong month (owner: gas readings on the 1st → amounts look shifted by one row). The preview shows a
warning "Readings on the 1st belong to the previous month, but the contract has no reading window — set the window to
28–5 so calculations match" with a one-click **Set reading window 28–5** (derived from the dates: from = 28, to = max
day seen + 2, capped at 10). Tests.

## 6. Import: old final from a note on installation-day rows
For an installation-day row (replacement pattern 1) with a note containing `rad:` values, use the note's old final
instead of the last regular reading (known issue from T-027a). Tests with the T-027 synthetic patterns.

## 7. Guide
Update the user guide EN + RU where behaviour changed (planned reminders list, import window warning).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner with his real data: reading history, Bills/Payments (all years) open without a noticeable freeze; one
   selected bill card; a scheduled reminder arrives on Android; help anchors stay inside; the gas import warns about
   the missing window.

## Manual test steps for the owner
1. Readings → gas meter → history (20 years) → opens quickly, scrolls smoothly. Bills / Payments → All years.
2. Contract → All bills → tap one bill → only that card selected; tap another → selection moves.
3. Settings → Reminders → Planned reminders shows the plan; follow the bill-due test → notification arrives.
4. Help → a link to another section → scrolls inside Help.
5. Import the gas sheet again into a contract without a window → warning + "Set reading window" → Charges for August
   match the bill.
