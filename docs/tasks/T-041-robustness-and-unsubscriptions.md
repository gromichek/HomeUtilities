# T-041: Robustness — lost editor load errors, hanging dialog, unsubscriptions, token disposal, startup measurement
Phase: 9 · Depends on: T-040 (#277) · Size: S–M · Source: `docs/PERF_AUDIT_2026-11-09.md` step 1 (read it, including
"Lead notes" at the end)

Owner (2026-11-09): the current speed and smoothness are fine. Fix only clear defects. **Nothing the user sees may change**:
no change of threading (editors keep reading on the UI thread), of the load order, of when a page appears, or of layout.
T-039 was reverted because the app felt different — if the owner notices any difference after this task, it is reverted too.

## Scope — do

1. **R-5 — editor load errors are lost.** `EditorPageBase.cs:46`, `ReadingHistoryPage.xaml.cs:34` and
   `EqualizedSettlementPage.xaml.cs:31` start `LoadAsync(id)` fire-and-forget: an exception is unobserved and the page stays
   empty. Observe it: the standard error dialog + error log entry (`RunSafeAsync` or the same handling), the page stays
   as it is. The load itself starts at the same moment as today.
2. **R-5 — a dialog can hang.** `DialogService.CloseModalAsync`: `MarkClosed()` in a `finally`, so `ShowEditorAsync` always
   finishes (the error of `PopModalAsync` is still logged).
3. **M-1 — Overview subscribes to the sync engine with a lambda and never unsubscribes.** Either a weak message
   (`WeakReferenceMessenger`, e.g. sent by `SyncNotifier`, which already listens to the engine; `HomeUtilities.Data` gets no
   MVVM dependency) or a named handler subscribed while the page is shown + one catch-up `UpdateSyncHint()` on appearing.
   Say in the PR which and why. The sync hint on Overview must update as today.
4. **M-3 — `SnackbarHost`.** Unsubscribe from `SnackbarService` also in `Unloaded` **and subscribe again in `Loaded`** (Shell
   raises `Unloaded` on tab switches; a host that does not subscribe again loses every snackbar — the #220 bug). No double
   subscription when both `Loaded` and `HandlerChanged` happen.
5. **M-4** `ChoiceField`: the `CollectionChanged` subscription of its `ItemsSource` follows `Loaded` / `Unloaded` (or a weak
   event); after `Loaded` the field shows the current items again.
   **M-5** `AdaptiveFieldGrid`: the child `PropertyChanged` handler becomes a named method, removed in `ChildRemoved`.
   **Only that** — the arrange logic stays exactly as it is (the T-039 coalescing was reverted).
   **M-6** `ReadingsPage`: `-=` before `+=` on `Window.Activated`; unsubscribe from the window that was subscribed (keep it
   in a field).
6. **R-4** `ReportsViewModel` (`_buildCancellation`) and `SyncScheduler` (`_debounce`): dispose a replaced
   `CancellationTokenSource` after cancelling it (as `ViewModelBase.BeginLoad`); dispose the last one when the page is left /
   in `SyncScheduler.Dispose()`. Careful: a token still used by a running task must not be disposed under it — cancel,
   then dispose only what nobody awaits any more (or dispose in the task's `finally`).
7. **Startup measurement.** `PerformanceLog.ReportStartupOnce` measures from the **process** start
   (`App.ProcessStart()`). Android sometimes starts the process in the background (e.g. a reminder alarm) and the window
   comes much later — the owner's log shows `Startup to Overview 3987402 ms`. Measure from the process start only when the
   first window was created shortly after it; otherwise from the first `CreateWindow` (or leave the line out). Rule in
   one place, unit-tested.

## Not in this task
No speed or smoothness changes, no change of threading, editor loading or layout (audit "Deferred", P-1…P-7, M-7, M-8).
No photo work (T-042), no sync snapshot (R-3), no `AppShell` change (M-2 not reproduced by the owner).

## Tests
- A leak test as in the audit (Overview view model collected after it is hidden while the fake engine lives). GC tests
  easily become flaky: helper in a separate `NoInlining` method, run the test 20 times locally before the PR and say so.
- Editor load failure → error dialog shown / error logged (ViewModels part); `CloseModalAsync` failure still completes.
- Startup rule with concrete times: process start 10:00:00, window 10:00:01, Overview 10:00:03.3 → 3 300 ms; process start
  09:00, window 10:00:00, Overview 10:00:02 → measured from the window (2 000 ms) or not written — as you decide.
- Existing tests stay green.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green. User guide: n/a (nothing visible), unless the error
log text about the startup line changes — then EN + RU. Owner: the app works and feels exactly as before.

## Manual test steps for the owner (short)
1. Phone and PC, Release: open a few editors, choices, dialogs, Readings — everything as before.
2. Delete a bill → the snackbar with Undo appears; switch to another menu section and back, delete again → the snackbar
   appears again.
3. Overview: the sync line updates after **Sync now** (Settings → Sync), also after switching sections.
4. Next day: Settings → Error log — no `Startup to Overview` line with a huge number.
