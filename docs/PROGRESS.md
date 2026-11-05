# Progress

Maintained by the project lead only (coding sessions do not edit it). Statuses: `Planned`, `Ready` (task file written), `In progress`,
`In review` (PR open), `Done` (merged), `Reverted`.

## Current

| Task | Title | Status | PR | Notes |
|---|---|---|---|---|

## Done

| Task | Title | Merged | Notes |
|---|---|---|---|
| T-001 | Solution skeleton, MAUI shell, localization, CI | 2026-10-19, #1 | App not buildable in the cloud sandbox (Android SDK download blocked); Android + Windows builds verified by CI. |
| — | Docs: Android tablet, adaptive layouts | 2026-10-19, #2 | ARCHITECTURE §4.9 |
| T-002 | Domain base types | 2026-10-19, #3 | 52 new tests. `Versioned.At` returns the first item on duplicate ValidFrom; `Validate` flags it. |
| — | Windows first TFM on Windows hosts | 2026-10-19, #4 | VS defaults to Windows Machine |
| T-003 | Properties, suppliers, links, contracts | 2026-10-19, #6 | 57 new tests (109 total). Deleted contract link does not override supplier link. |
| T-004 | Meters, readings, consumption calculator | 2026-10-19, #8 | 88 new tests (197 total). Window fallback refined in T-005. Replacement meter does not copy Digits/SerialNumber (UI pre-fills). |
| T-005 | Window fallback fix, VAT catalog, tariffs | 2026-10-19, #10 | 62 new tests (259 total). LV VAT defaults to be verified by owner. |
| T-006 | Charge calculator | 2026-10-20, #13 | 36 new tests (295 total). Contract inactive only if inactive on both first and last day. Warning `NoMeters` added. |
| — | Docs: categories, payment details, comments | 2026-10-20, #12 | |
| — | Docs: agreed payment types (PAYMENT_TYPES.md) | 2026-10-20, #14 | custom fields confirmed afterwards |
| T-007 | User categories, payment details, comments | 2026-10-20, #15 | 74 new tests (369 total). 4-meter water test included. |
| T-008 | Bills, custom fields, payments, allocations, balances | 2026-10-20, #17 | 103 new tests (472 total). BillLine/PaymentAllocation are SyncEntities. Equalized plans moved to T-020. |
| T-009 | EF Core persistence | 2026-10-20, #19 | 33 Data tests. ClientCascade for child collections (DB FKs stay RESTRICT). `Pay_Present` shadow column. |
| T-010 | Data stores, seed data, database in the App | 2026-10-20, #21 | 29 Data + 5 VM tests. Stores are singletons over a context factory. Owner manual test pending. |
| — | Docs: payment status, Paid quick action, undo | 2026-10-20, #23, #25 | `Payment.CreatedByQuickAction` planned for T-018 |
| T-011 | App shell, navigation, settings, adaptive layout | 2026-10-20, #24 | 16 new VM tests (558 total). Messenger-based language refresh. |
| — | Hotfixes: AppShell resources, startup language | 2026-10-20, #27, #28 | Startup-order rules added to CLAUDE.md |
| T-012 | Properties screens + shared UI patterns | 2026-10-20, #29 | 63 new VM tests (618 total). Owner manual test pending. |
| T-012a | UI guidelines applied, Properties reworked | 2026-10-20, #32 | Icon font replaced by lead with 330 KB static Material Icons. Owner manual test pending. |
| — | Hotfix: icon font bundling, editor visibility, duplicate add | 2026-10-23, #35 | pitfalls added to UI_GUIDELINES §6a |
| T-013 | Suppliers and categories screens | 2026-10-23, #34 | Lead applied #35 fixes to Suppliers/Categories pages. Shared MasterDetail bases introduced. Owner manual test pending. |
| — | Hotfix: Windows toast crash | 2026-10-23, #37 | |
| — | Hotfix: purpose format crash | 2026-10-23, #41 | invalid `{Period:…}` kept as typed |
| T-014 | Contracts screens | 2026-10-23, #38 | 135 new tests (753 total). Properties moved to shared MasterDetail bases. Supplier picker without search (OK for now). |
| T-014a | Robustness, stay after Save, error log, Android layout | 2026-10-23, #43 | Session worked from the first task version; lead merged #41, fixed flaky VM tests (sequential run) and added CI failure annotations. 963 tests. |
| — | Hotfix: list selection highlight (Windows) | 2026-10-24, #50 | explicit base stroke |
| — | CI: skip builds for docs-only changes | 2026-10-24, #51 | code PRs ~4 min, docs PRs seconds |
| — | Hotfix: colour swatches clipped on Android | 2026-10-23, #46 | FlexLayout replaced by two fixed rows |
| T-015 | Meters screens, contract links, replacement wizard | 2026-10-23, #45 | 1128 tests. Replacement saved atomically; meter + links saved in two steps otherwise. |
| T-016 | Readings entry, history, submit link | 2026-10-24, #48 | 1271 tests. Windows: no "saved" toast (rows clearing is the feedback) until T-019. |
| — | User guide EN/RU started (covers T-011…T-016a) | 2026-10-24 | sessions extend it with every visible change |
| T-020a | Quantity from the bill, VAT on the total | 2026-10-26, #93 | Sample FromBill component moved to cottage electricity. Newest bill of a month supplies the quantities. |
| T-032 | Sync polish (C9–C14) + attachment files | 2026-11-04, #213 | Pause/resume (`FirstJoinChoice.Resume`, account stored with the join; Disconnect forgets the join); restore/JSON import mark NeedsRejoin; "Replace the cloud" = snapshot generation `-g<N>` + old files deleted, other devices stop with `CloudReplaced` and are asked; `ISyncConnectivity` + 60 s foreground retry; `IUiDispatcher` for engine events; Files in the cloud; `SyncNotifier` 1.5 s (switch also silences errors — confirmed by the owner 2026-11-04); attachments `attachments/<id><ext>` (reconcile on every sync, SHA-256, Wi-Fi auto / mobile on demand, 30-day cloud clean-up). Guide EN + RU updated. Open: Drive download buffers in memory. |
| T-032b | Re-join after restoring an older backup; readable sync choice dialogs | 2026-11-04, #217 | Restore carries THIS device's join identity (device id, account, generation) into the restored file with Enabled=0, NeedsRejoin=1, outbox/state of the copy reset (tables created if missing). First join with data on both sides offers three choices; choice dialogs use `SingleChoiceDialogViewModel` with a wrapping message (`ChooseWithMessageAsync`). Guide EN + RU updated. Owner test OK (2026-11-04). |
| T-034 | Release identity: signing key, app icon, house notification icon | 2026-11-04, #222 | Optional git-ignored `signing.props` imported for every Android build (template + `docs/SIGNING.md`); icon/splash = white house with a drop cut-out on `#2F5D8A`; `ic_stat_reminder.xml` = same house. Owner test OK (2026-11-05). |
| T-035 | Release builds: version, signed APK, Inno Setup installer, release script | 2026-11-05, #227 | Version computed in the csproj for every build (`yyyy.M.d`, build = minutes since 2026-01-01 UTC), also as `AssemblyMetadata` for About; `scripts/publish-release.ps1` → `release\` (APK + setup); `installer/HomeUtilities.iss` per user, no admin, `{localappdata}\Programs\HomeUtilities`, lead added `DisableDirPage` (InstallDelete empties `{app}`). Script/installer not runnable in the sandbox — owner test pending; data path confirmed: `%LocalAppData%\HomeUtilities.App\HomeUtilities.App\Data`. Fixes after the owner's first run: #229 (password file needs two lines for apksigner), #230 (Windows publish with `RuntimeIdentifierOverride`, NU1102 with `-r`). |
| T-031 | Sync engine | 2026-11-03, #203 | `SyncOutbox` filled by the interceptor (received rows via raw SQL → no echo); `changes/<deviceId>/<seq:D10>.json` (≤ 500 rows), `snapshot/<deviceId>-<ts>.json.gz`; LWW by `UpdatedAt`, tie → higher deviceId; resurrect; unique clashes only `Categories.SystemKey` / `VatRates.Code` (lower id keeps it); newer format/migration stops; first join (Replace preselected + backup, Merge, empty device); `SyncScheduler` (start +3 s, resume ≥ 30 s, 10 s after a change, flush on leave, Sync now). New DeviceId per first join. Open: restore/JSON import on a syncing device (C9). |
| — | Fix: icon images use the bundled font on all platforms | 2026-11-02, #199 | Unpackaged Windows: MAUI resolves an image font to a file URI; only a bundled font file works (system/default font → relative URI → exception; menu icons were invisible, theme change crashed). Possibly also the root of C5 (mixed colours). |
| — | Fix: theme-switch crash on Windows, Google reconnect on Android | 2026-11-02, #197 | `IconImageFontFamily` (one family for `FontImageSource` on Windows); Android Disconnect/Connect clears the cached token (`AuthorizationClient.ClearToken`). |
| T-030 | Google sign-in per platform | 2026-11-02, #194 | New `HomeUtilities.Sync` project: PKCE + loopback (Windows, DPAPI for the refresh token), Google Identity Services Authorization API (Android, `Xamarin.GooglePlayServices.Auth` — licence exception), `GoogleDriveStorage` (Drive v3 REST, `appDataFolder`), `ISyncAccountService`/`ISyncStorage` in Core; Settings → Sync (connect, check, disconnect); `secrets.json` git-ignored, template committed. Lead aligned AndroidX Ktx companions (NU1608). Open: Android sign-out may need `RevokeAccess`. |
| T-028i | Theme regression, reload gate everywhere, button widths, confirm without backup | 2026-11-02, #191 | Theme: T-011 `ThemeService` back + `UserAppTheme` applied after dispatch (hypothesis: Windows Picker popup still open) — not verified; reload gate for all list sections; interceptor notifies only when rows were written; Imports buttons Fill; `BackupGuard` asks "Continue without a copy?". |
| T-029 | Automatic backups, JSON export/import, Android backup rules | 2026-11-02, #184 | `backups/auto` zips (1/3/7 days, only if changed; always before import/restore/JSON import/wipe/migration), keep N (3/7/14) + 3 monthly; Settings → Backup list with Restore / Save copy; `DataExchangeService` JSON (all tables incl. deleted, decimals as numbers, strict import, staged); Android Auto Backup only `files/backups/system-copy/` (newest zip). A failed pre-operation copy does not block the operation (open question to the owner). |
| T-028h | Collected fixes and speed | 2026-11-02, #181 | Windows theme: root `RequestedTheme` flipped to the opposite and back (not verified); help tables in scroll blocks; stacked grid buttons fill their cell (lead: not FullRow buttons); `ReloadGate` (Overview, Charges reload only after a change / new day / language); `LoadManyAsync` batches charge inputs (Overview, settlement, Charges). Other section roots not gated yet. |
| T-028b | Performance: menu switching, scrolling, memory | 2026-11-02, #175 | Loads off the UI thread (Sqlite "async" is synchronous — likely ANR cause), latest-wins cancellation on leave/new load, `ItemsSync` keeps scroll position, Bills/Payments cards flattened; `Performance:` lines ≥ 300 ms + startup time in the error log. No device numbers yet; root pages not made singletons (Shell caches root pages). Editors (Bill, Contract) still load on the UI thread. |
| T-028g | UI fixes round 3 | 2026-11-02, #174 | Imports help, profile buttons wrap/stack, payment row with a small cancel icon, help pinch zoom (Android), Windows dark theme re-applied (cause not confirmed). |
| T-028d | Settings as a list of sections | 2026-11-01, #165 | 10 rows (icon, title, value); Language, Theme, Reminders, Backup, Error log, About open `SettingsSectionPage`; Categories, VAT rates, Bill templates, Imports open their screens; buttons stacked; Clear log confirmed; new HelpTopics. Segoe glyphs chosen from memory — owner to check icons on Windows. |
| — | Reminders pop up and show on the lock screen | 2026-11-01, #166 | Channel `reminder_alerts` with **high** importance (default importance: no pop-up, not on the owner's lock screen); old channels deleted. |
| T-028f | One content pane everywhere | 2026-11-01, #163 | No list + editor split; editors are pages with ✓ everywhere (desktop footer removed); lists in 1–3 columns (`ListColumns`), editor cards in up to 2 columns; highlight + scroll to the last opened card; period filter as a drop-down; side-pane code and 8 resx keys removed. Owner to check the 2-column long editors (Bill, Contract) — `MaxColumns="1"` if they look bad. |
| — | Reminder text on the lock screen | 2026-11-01, #160 | New channel `reminders_public` (lock-screen visibility public), old channel deleted. |
| T-028e | Reliable reminders on Android | 2026-11-01, #158 | Own scheduler (`ReminderAlarms`: inexact `SetAndAllowWhileIdle`, own receiver, plan in prefs, boot receiver); plugin only for permission/channel/test. Flush on leaving the app; real `FLAG_NO_CREATE` check + reason in Planned reminders; failures to the error log. Lead fixed the Android build (`IntentFilter` ambiguity, nullable builder chain) and merged after T-028a (MainActivity conflict). Cause of the old failure not confirmed on a device. |
| T-028a | Device polish: layouts, cut texts, Android Back | 2026-11-01, #157 | Bills/Payments filters in the list header, collapsible summary on compact; height < 480 dp → Compact; footers never overlap; 33 switch rows wrap; AndroidX `OnBackPressedCallback` → `IWindow.BackButtonClicked` (predictive back on Android 16), section root Back → Overview. |
| — | Reminders without quiet hours; Planned reminders list hands the plan over first | 2026-10-31, #149 | Owner decision: reminder time used exactly as set (21:00–08:00 shift removed). False "not handed" marks came from comparing a fresh plan with the last debounced one. |
| — | Fix: late reminders dropped; Planned reminders diagnostics; help link to the other guide on Windows | 2026-10-31, #147 | Cause: Plugin.LocalNotification drops an alarm delivered > `AllowedDelay` (default 1 min) late; inexact alarms are late → now 6 h. List shows "Handed to the system: N of M" and refreshes on settings change. Windows WebView serves help from `https://0.0.0.1/`. |
| T-028c | Fixes from real-data testing | 2026-10-30, #143 | "Show more" paging for long lists; Overview settlement capped at 24 months; Planned reminders debug list; reminder cause not confirmed (WAKE_LOCK still removed — re-allow if the plan is non-empty and nothing arrives). Lead relaxed two flaky perf limits. |
| T-027a | Export like the owner's workbook | 2026-10-30, #132 | Total sheet = billed only (report parity). Paid mark only for fully paid bills. |
| — | Fix: editor reloaded after closing a child dialog | 2026-10-30, #136 | Shell re-applied the query on return → lost name/prices in the tariff editor. |
| — | Reports: metered contracts without tariff, equalized months by tariff, months before contract start, 20-year performance | 2026-10-30, #133 | Sonnet helper (resumed after the weekly limit). |
| — | Meter–contract links start at installation; editable link dates; repair | 2026-10-30, #134 | Sonnet helper. Old rule started new links "today", hiding history. |
| — | Consumption report: group by resource / contract / meter | 2026-10-29, #130 | Sonnet helper; water total of the flat = contract grouping. |
| — | Reminder on the last window day; equalized months decided by plan coverage | 2026-10-29, #127, #128 | Sonnet helpers. Switching a contract to equalized keeps it; months before the first plan stay "by consumption". |
| T-028 | Reminders (local notifications) | 2026-10-29, #124 | Plugin.LocalNotification 14.1.2 (MIT). Manifest removes exact-alarm, wake-lock and foreground-service permissions — watch for a crash if the plugin needs them. Windows: in-app summary snackbar once a day (owner OK). Contract flag instead of ReminderRule. |
| T-027 | Import: reading dates, positions, automatic replacements | 2026-10-29, #125 | Migration AddImportReplacements; estimated finals accepted by ConsumptionCalculator; DetectReplacements on by default. |
| T-026 | Excel import wizard, profiles, undo | 2026-10-29, #116 | Lower reading without dial digits = error (row skipped) — T-027 may need replacement handling. Lead resolved resx conflict; `.gitattributes` now uses union merge for resx (#118). |
| — | Reports: coloured property groups, calendar for custom period | 2026-10-29, #117 | Sonnet helper; range from bills + readings (payments not counted). |
| T-023b | Reports polish: dialogs, report picker, wheel, spinner; narrower menu | 2026-10-29, #114 | Android centred list caused by MaximumHeightRequest in a * row. Spinner replaced by "Calculating…" text. Wheel forwarding on Windows. Flyout width 200. |
| T-025 | Bill templates with schedules, draft bills | 2026-10-28, #111 | Drafts excluded from per-contract/per-property lists and loaders; `ListAsync` still returns them. Draft counts as entered for BillMissing. |
| T-023a | Reports: compact filters, grouped table, Android spinner | 2026-10-28, #110 | Spinner cause not reproducible — busy counter + IsVisible-bound indicator. Lead fixed the build (ambiguous Grid.SetRow) and a resx merge conflict. |
| T-024 | Charts; category filter on Bills | 2026-10-28, #108 | No chart package: drawn with Microsoft.Maui.Graphics (`GraphicsView`); `ChartModel` library-free, LiveCharts can be adopted later. |
| T-023 | Reports: tables + Excel/CSV export | 2026-10-28, #105 | ClosedXML 0.105.0 (MIT; SixLabors.Fonts 1.0.0 Apache-2.0 — newer SixLabors versions use the Split licence: do not upgrade it). New project HomeUtilities.Import. All reports in EUR (LVL converted). |
| T-022 | Overview: to-do and money | 2026-10-27, #102 | `TodoBuilder` + `OverviewMoney` in Core, `OverviewDataLoader`. "This year" = year to date vs same months last year. Settlement calc duplicated in loader (extract later). |
| T-021 | Equalized plans + settlement, tax installments | 2026-10-26, #99 | Incomplete months excluded on both sides of the settlement; new plan auto-ends the previous one; replace keeps paid installments. Lead resolved resx merge conflict. |
| — | Fix: bill due-date error + optional line amount for quantities from the bill | 2026-10-26, #95, #97 | Sonnet helpers. Due-date root cause not confirmed (stale error / empty date / converter) — all three fixed. |
| — | Bills and Payments: period filter (all years / year / month) | 2026-10-26, #91 | Sonnet helper; shared `PeriodFilter`. |
| T-020 | Payments, allocation, prepayment, balance | 2026-10-26, #89 | `PaymentRules` (quick flag cleared), `PrepaymentService` uses only the contract's own payments. |
| T-019 | Bills, comparison, Paid + Undo, in-app snackbar | 2026-10-25, #86 | Snackbar via page ControlTemplate (watch on devices; fallback: host in AppShell). Comparison skipped when any line is "from bill". Bill delete releases allocations (money stays as prepayment). |
| T-018 | Charges screen | 2026-10-24, #81 | `ChargePeriodDefaults`; `ChargeLine.PriceIncludesVat`; pushed routes `ReadingsFor`/`ChargesFor`. Open question (VAT from rounded net) decided: yes → #82 (Sonnet helper). |
| T-017e | Fixes: VAT code reuse, reorder arrows, double tap | 2026-10-24, #79 | Unique indexes filtered on IsDeleted (migration); NavigationGate for pushes. |
| T-017 | Tariff editor + VAT catalog screen | 2026-10-24, #74 | ~230 new tests. FromBill needs no price. Open question (replacement vs `MeterIds`) decided: a selected meter includes its replacement chain → fix PR by a Sonnet helper. |
| T-017b | In-app help (user guide in the app) | 2026-10-24, #70 | Markdig; diagrams as SVG rendered from .mmd (scripts/render-guide-diagrams.sh). |
| T-017d | Two-pane screens keep the selection on return | 2026-10-24, #69 | Cause inferred (root page recreated); singleton selection memory. Lead fixed the App build (missing using) and a test after merging #70. |
| — | Hotfix: restore deadlock at startup, Android save dialog | 2026-10-24, #66 | sync-over-async on the UI thread hung the app after Restore (Windows) |
| T-017c | Backup file / restore + sample data | 2026-10-24, #64 | 43 new tests (1413). Restore and "Delete all data" are staged and applied at the next start. Windows: cancelling the save dialog falls back to Documents\HomeUtilities\Backups (owner to check). |
| T-017a | Navigation guard, menu opens section root | 2026-10-24, #60 | 12 new VM tests. Lead fixed the Windows build (ambiguous `Window`) and a double dialog on root Back. Pop-to-root runs inside the Navigating deferral — watch for flicker. Only the topmost page is asked. |
| T-016a | Attachments, meter/reading photos, submit links on meter cards | 2026-10-24, #54 | Image shrinking on Android only. Soft-deleted attachment files kept (cleanup later). |

## Known issues / tech debt

### Collected for the next session task (owner: collect fixes and hand them over together, 2026-11-02)

(C1–C4 done in T-028h #181. Suggestions for later: `ReloadGate` for the other section roots; `MeterStore.IncludeAll` loads all readings.)

- C5. **(Solved by #199)** **Windows theme — a regression.** Owner: switching worked correctly after the first implementation (T-011) and broke
  later. `ThemeService` itself was unchanged from T-011 until #174, so the break came from another change between T-011
  and T-028d (owner first reported it after T-028d: Settings sections — the theme choice now lives on a pushed
  `SettingsSectionPage`; T-028f one pane may also matter). #174/#177/#181 workarounds did not help. Debug log of the
  current build shows the native root theme now changes as asked (`root.RequestedTheme`/`ActualTheme` follow the choice,
  `Application.RequestedTheme` correct), yet the first switch still looks mixed. Next task: **remove the workarounds**
  (back to the T-011 `ThemeService`), then find the change that broke it (theme choice on a pushed page? implicit styles,
  `StaticResource` colours in the new pages/controls — `SettingsSectionPage`, `AdaptiveFieldGrid`, `ListColumns`,
  `FilterPanel`, Shell flyout styles?) by comparing with the T-011…T-028c XAML/styles.
  **Log of three switches (owner, 2026-11-02; each switch writes a "before" and an "after" line):** 12:30:16 Dark → System
  **wrong**, 12:30:17 System → Dark **wrong**, 12:32:20 Dark → System **correct**. In all three the logged native values
  (`root.RequestedTheme`, `ActualTheme`, `Application.RequestedTheme`) match the choice — so what is drawn does not follow
  what is logged; the logged values alone do not show the problem. (The lead first misread this as "only the first switch
  after start is wrong" — that is not established.)
- C6. **Imports card buttons (phone):** Export / Delete are now equal and full width, but "Load profile from file" below
  them has its natural width and "Cancel import" too — within one card all stacked action buttons on compact width have
  the same full width (rule in UI_GUIDELINES §3 must cover buttons outside `AdaptiveFieldGrid` too).
- C7. **Sections still feel reloaded on every visit** (phone). `ReloadGate` covers only Overview and Charges; apply it to
  every section root (Bills, Payments, Readings, Reports, Properties, Suppliers, Contracts, Meters, Settings lists) and
  check why Overview/Charges still feel slow (log whether the gate skipped: add "Performance: <VM> skipped (no change)" in
  Debug).
- (C9–C14 done in T-032 #213.) C9. **Restore / JSON import / "Delete all data" on a device with sync on** (T-031 open question): the restored database
  carries an old sync state (applied sequences, outbox), so the next sync re-applies newer cloud changes over the restored
  data, and the restore is silently undone. Proposal: these operations switch sync off; switching it on again asks
  "Replace this device's data with the cloud data" **or "Replace the cloud data with this device's data"** (new: upload this
  device as the new source — needed to bring a backup back to all devices). **Owner agreed (2026-11-03).**
- C10. **Sync after the network returns** (owner, 2026-11-03): the status stays "Waiting for the network" until the next
  trigger (resume/change/Sync now). While the app is **in the foreground**: listen to connectivity changes
  (MAUI `Connectivity.ConnectivityChanged` behind an interface) and sync when the network is back; plus a retry every
  ~60 s while the status is "waiting"/"failed" and the app is in the foreground. Nothing in the background (battery rule).
- C11. **Sync status events off the UI thread** (lead review of #203): `SyncEngine.StatusChanged` is raised on the sync
  thread and `SyncSettingsViewModel` updates bindable properties and `NotifyCanExecuteChanged` directly → WinUI throws on
  a wrong thread / stale button states. Marshal to the UI thread (captured `SynchronizationContext` or a dispatcher
  interface); unsubscribe the transient VM from the singleton engine (leak). Check the same for the Overview sync hint.
- C12. **"Files in the cloud"** (owner, 2026-11-03): the hidden Drive `appDataFolder` is not visible in Google Drive. Add a
  read-only list in Settings → Sync: change files grouped by device (device id short form, which one is "this device",
  count, total size, newest date), snapshots (date, size), foreign files; total size. Nothing can be deleted from there.
  Guide EN + RU: where to see the hidden data in Google Drive (Settings → Manage apps → HomeUtilities, "Delete hidden app
  data" wipes the cloud copy only).
- C13. **Sync messages** (owner, 2026-11-03): short in-app messages (the existing snackbar, `ISnackbarService`) about the
  sync stages — "Syncing…", "Synced: 3 received, 1 sent", "Sync failed: <reason>" — visible 1–2 s, never blocking.
  To avoid noise from the automatic syncs (start, resume, 10 s after a change): "Syncing…" only for Sync now and the
  first sync after start; "Synced" only when something was received or sent; errors always (once per kind until the next
  success). A switch "Show sync messages" (default on) in Settings → Sync. Rules agreed by the owner (2026-11-03); the
  duration is tuned after his test.
- C14. **Switching sync off and on is a new first join** (owner test 2026-11-04): `DisableAsync` only clears "enabled";
  `EnableAsync` always creates a new device id, clears the applied sequences and asks Replace/Merge again (Merge re-uploads
  every row). Owner expects **pause / resume**: switching on again on the same account and an unchanged local database
  continues with the same device id, applied sequences and outbox, without a question (changes made while off are in the
  outbox and go up; the others' changes come down, last writer wins). The first-join question only for a device that never
  joined this cloud, after a restore / JSON import / delete-all (C9) or after Disconnect/another account. Also the dialog
  text: "appear twice" applies only to records created separately on both devices, not to records synced before (same ids
  merge by last edit) — reword.
- C8. **Backup before a risky operation failed** (owner decision 2026-11-02, T-029 open question): before an Excel import,
  a restore, a JSON import or "Delete all data", if the automatic copy fails, ask "Could not make a backup copy: <reason>.
  Continue without a copy?" (Cancel = default/secondary safe choice, Continue = destructive style). The migration copy at
  startup keeps logging only (no dialog before the first screen).

### Done batch C1–C4 (T-028h)

- C1. **Windows: first switch to the dark theme** — texts switch, native backgrounds (menu pane, page, pickers) stay light;
  choosing the theme again fixes it. Two blind attempts failed (#174 native `RequestedTheme` + re-apply, #177 delayed
  re-apply). Needs a real investigation (MAUI Windows theme handling, WinUI `RequestedTheme` of the window content vs
  `Application.RequestedTheme`, styles using `StaticResource` colours).
- C2. **Help: wide tables** are squeezed to the page width and do not scroll sideways (T-028g asked for `overflow-x: auto`
  per table block; check the generated HTML/CSS in `HelpContentService` and the Android WebView).
- C3. **Imports → saved profiles (phone portrait):** the stacked Export / Delete buttons have different widths — stacked
  buttons must have the same (full) width.
- C4. **Performance (phone, Release, `Performance:` lines):** Overview 0.5–0.9 s on **every** appearance (reloads each
  time), Charges 0.9–1.2 s per switch, Reading history 0.4 s (269 items), startup to Overview 2.9 s (Windows: Overview
  1.3–1.7 s, startup 3.8 s). Ideas: reload only when data changed since the last load (`IDataChangeNotifier`), cache
  calculation inputs, profile `OverviewDataLoader`/`ChargeCalculator`; startup: show the Overview shell first.

### New findings (owner, 2026-11-01, after T-028a/T-028e)

8. **Done in T-034 (#222).** ~~Later (owner, 2026-11-02):~~ the notification small icon should be the **white house from the app icon** instead of
   the bell (`drawable/ic_stat_reminder.xml`, one colour + alpha). Do it with the real app icon (the current app icon and
   splash are placeholders — PROGRESS tech debt).
9. **Done in T-034 (#222).** Permanent signing key instead of the per-PC debug keystore (Google sign-in on Android
   checks the SHA-1; a different key also blocks updating over an installed build). Planned as T-034 together with 8.
   Until then the owner copies `%LocalAppData%\Xamarin\Mono for Android\debug.keystore` to other PCs.

5. **Tablet (portrait and landscape with the menu open):** list + editor two-pane leaves the list ~450 dp; the filter
   card is cramped (period buttons cut to "Вс/Го/Ме", the year "2026" wraps per digit). Owner decision: **one content
   pane** everywhere (variant A) → done in T-028f (#163).
6. **Scrolling stutters** on phone and tablet in all lists/panes — **in Release builds** (owner tests speed only in
   Release). Profile in T-028b: card templates (nested layouts, shadows), `CollectionView` item sizing, images.
7. **Reminder for a paid bill arrived once** — not reproduced in several tries (2026-11-02). Closed unless it recurs.

### T-028a findings (owner, real phone + tablet + Windows) → tasks T-028a (1, 2, 4), T-028d (3), T-028e (reminders)

1. **Bills, Payments (phone, 2026-10-31):** the filter block (property, period buttons + year, category/contract, status,
   totals) is fixed at the top and does not scroll with the list → little room for the cards in portrait; in **landscape
   no card is visible at all**. Filters must scroll away with the list (header of the list) or collapse into a compact
   filter row / dialog on narrow or low screens.
2. **Suppliers (phone landscape, 2026-10-31):** the two-pane layout shows the desktop footer (Save / Cancel / Delete supplier);
   the buttons overlap each other ("Cancel" under "Delete supplier"). Owner: Save is not the standard one and not at
   the top. On a phone in landscape use the phone editor commands (toolbar ✓ Save at the top, Back cancels, Delete in
   the menu / at the end of the form, UI_GUIDELINES §3), and a footer must never overlap (wrap or shrink). Check all
   two-pane editors, not only Suppliers.
3. **Settings (phone, 2026-10-31):** one long page; button text cut off ("Очистит" instead of "Очистить" in Error log —
   two buttons side by side on a narrow screen). Owner wants Settings as a **list of sections** (Language, Theme,
   Reminders, Backup, Imports, Error log, About…); tapping a section opens a page (phone) / right pane (tablet, desktop)
   with all its settings. Review where plain buttons can be replaced by drop-downs / choice rows as elsewhere in the app
   (UI_GUIDELINES §3a), and bring Settings to the same style as the other screens. Buttons must never cut their text
   (wrap or stack vertically on narrow screens).
4. **Property → Contracts (phone, 2026-10-31):** the label of the switch "Show ended and archived" does not fit (cut off).
   Owner: check this **everywhere** — every switch/check box label, button text and card line must wrap (or the control
   moves under the label) instead of being cut; go through all screens in Russian (the longer language) on a narrow phone.

- Not planned (owner decision 2026-10-30): closing the open remainder of a bill without a payment (e.g. equalized bill paid less by consumption). Workaround: treat such months as "by consumption" (plan starts later).



- Import: an installation-day row ignores the old-final value from a note (old final = last regular reading) — exact round trip only when they are equal (T-027a).

- Some texts do not wrap on narrow screens (seen in the emulator, place not noted) → device polish pass on a real phone/tablet before backup/sync.


- `ViewModelBase` uses `WeakReferenceMessenger.RegisterAll` (reflection) — check Android release trimming; switch to explicit `Register<T>` if it breaks.
- App icon and splash are placeholder SVGs (no MAUI template available offline).
- Cloud coding sessions cannot build the MAUI App; rely on CI for Android/Windows.
- SQLite decimals lose trailing zeros (`-12.10` → `-12.1`); numerically equal, UI always formats amounts.
- Snapshot `ChargeWarning.Args` come back as `JsonElement` (display only).
- Timestamps of items inside JSON columns (links, custom field definitions) are not stamped; the owner row is (sync is row-level).
- `AggregateMerger` adds new children via `db.Add` — UI must set the child's FK (e.g. `PropertyParameter.PropertyId`) before saving.
- Saving an id that exists only as a soft-deleted row is not supported (resurrect needed for sync, T-030).
- Flaky test fixed (shared static messenger in parallel VM tests → ViewModels.Tests run sequentially). CI now writes failed tests as check-run annotations (readable via API).
- Android "≡" floating button seen by the owner is probably the Android **Accessibility Menu** shortcut, not our app — owner to confirm.
- Link-list logic duplicated in supplier and contract editors (extract helper VM later).
- FlexLayout clips first item per row on Android — avoid it (UI_GUIDELINES §6a).
- Attachment files of soft-deleted rows are never removed (cleanup job later); image shrinking only on Android.

## Manual test log (owner)

| Date | Build / PR | What was tested | Result |
|---|---|---|---|
| 2026-10-19 | #1 | Start on Windows and Android emulator | OK (VS framework selection needed, fixed by #4) |
| | #21 | Database created + seeded on Windows / Android, no duplicates after restart | pending |
| 2026-10-20 | #24 | Start on Windows / Android | **Crash**: `StaticResource not found for key PrimaryLight` (AppShell built before App resources) — fixed in hotfix PR |
| 2026-10-20 | #27 | Restart with Russian selected | **Bug**: menu in English, Settings in Russian (LanguageService created lazily after Shell) — fixed in hotfix PR |
| 2026-10-20 | #28 + #29 | Shell, settings, Properties on Windows + Android | Works. **Usability**: identical buttons, unclear grouping, non-standard dialog texts; **Android bugs**: Kind picker empty, Back did not leave the editor (after archive toggle), stray "≡" button → T-012a |
| 2026-10-23 | #32 | Properties reworked on Windows + Android | Layout OK. **Bugs**: Android icons shown as empty boxes (icon font not bundled — `MauiFont` item missing); Windows empty editor visible with no selection (IsVisible binding on changed BindingContext); duplicate "New property" button → hotfix #35 |
| 2026-10-23 | #34 + #35 | Suppliers: Copy button | **Crash on Windows**: CommunityToolkit Toast unsupported in unpackaged app → hotfix (no toast on Windows) |
| 2026-10-23 | #37 | Suppliers, categories | **Crash** on purpose template `{Period:mm.yyyy}`; Android: Save closes supplier editor (owner wants to stay); colour swatches cut off → T-014a |
| 2026-10-23 | #43 | Colour swatches | first swatch of each row clipped (FlexLayout on Android) → #46 |
| 2026-10-24 | #38–#48 | Readings, meters, contracts, suppliers (Windows + Android) | Works. **Bug**: list selection highlight stays on previously tapped items (Windows) → hotfix. Request: accept `,` and `.` everywhere — already the case via `DecimalInput`, documented. |
| 2026-10-24 | #54 | T-016a: meter/reading photos, submit links on meter cards | OK |
| 2026-10-24 | #64 | T-017c backup | Sample data OK, delete all OK, Windows save OK. **Bug**: after Restore on Windows the window never appears (deadlock) → #66. Android: only share targets, no folder (no account in emulator) → save dialog in #66. Restore on Android pending |
| 2026-10-26 | — | Bill due date: false "due before issue date" seen twice, not reproducible → Sonnet helper. Heating line 0,233 MWh × 73,76 = 17,19 vs 17,22 on the bill (supplier bills unrounded quantity) → optional line amount from the bill → #95, #97 | pending |
| 2026-10-29 | #110 | T-023a report filters | Works, but: choice dialogs look foreign (Windows gap, Android centred list, period presets cut off), report buttons do not fit on phones, wheel over the table stops page scroll (Windows), Android spinner still turning → T-023b; UI_GUIDELINES §3a added |
| 2026-10-31 | #143 | T-028c: long lists, contract → All bills selection, gas import window warning, help links (Android) | OK. Overview settlement cap of 24 months accepted. Import "unpaid gas bills" was the owner's own experiments — no bug. **Bugs**: reminders listed but not delivered; Windows help link to `ru.md`/`en.md` opens the browser → #147 |
| 2026-10-31 | #147 | Reminders on a real phone (release build), help link on Windows | OK, reminders arrive ~1 min late (inexact alarm). Occasional "not handed" marks were a stale comparison; owner asked to drop quiet hours → #149 |
| 2026-11-01 | #157 + #158 | T-028a layouts/Back (phone, tablet, Windows); T-028e reminders scenarios a–d | Reminders OK (new, changed, restart, notifications off); **once** a paid bill still notified (not reproduced). Layouts OK on phone, Back OK. **Tablet**: two panes too cramped (filters: "Вс/Го/Ме", year "202/6" wrapped) → owner wants one content pane. Lock screen showed no text → #160. Scrolling stutters on phone and tablet in all lists. |
| 2026-11-01 | #163 | T-028f one content pane (tablet portrait/landscape, Windows, phone) | OK; 2-column editors look fine (keep). |
| 2026-11-01 | #160 | Reminder text on the lock screen | **Not shown** on the lock screen (sound only) → high-importance channel #166 |
| 2026-11-01 | #166 | Reminders pop-up / lock screen | Pop-up OK when unlocked; **locked: sound only**, notification is in the shade after unlocking. App settings correct (lock screen allowed, show content). Delayed test added (#168) to check. |
| 2026-11-02 | #168 + #170 | Reminders on the locked screen | **OK**: the screen lights up with the notification (also real reminders), bell icon. Cause was the coloured app icon used as the small icon. |
| 2026-11-02 | #174 + #175 (Windows) | Windows maximised; error log | Two-column editors: "Save as template"/Delete stretched as cells; dark theme first switch still mixed; WinUI crash in `ListColumns` on resize during `ItemsSync` → #177. Timings (Windows): Overview 1.3–1.7 s, startup to Overview 3.8 s, Charges ~0.35 s. |
| 2026-11-02 | #191 + #194 | T-028i, T-030 with the owner's `secrets.json` | Sign-in, check, disconnect/reconnect OK on Windows; phone sign-in + check OK, but reconnect after Disconnect said "access revoked" → #197. Theme switch on Windows **crashed** (FontImageSource with comma-separated font family) → #197. Second visits fast, Imports buttons OK. Airplane-mode check pending. |
| 2026-11-02 | #197 | Windows theme change; Android Disconnect → Connect | Android reconnect OK. Windows **still crashed**: every `FontImageSource` failed ("Unable to generate font image", relative URI) → #199. |
| 2026-11-03 | #203 | T-031 sync (PC first, phone "Replace", paid bill → PC, reading → phone, airplane mode) | OK; after the network came back the status stayed "Waiting for the network" until the app was resumed (screen off/on) → C10. (Connect "not working" on Windows was a build without `secrets.json`.) |
| 2026-11-04 | #213 | T-032: pause/resume, airplane mode, files in the cloud, meter photo phone → PC, restore + "replace the cloud", help | 1–4 and 6 OK. Step 5: restoring an older (pre-sync) backup gave the generic first-join question without "Replace the cloud", text cut off on Windows → T-032b. |
| 2026-11-04 | #217 | T-032b: restore of a pre-sync copy → two choices, "Replace the cloud" → phone asked and replaced; never-joined device → three choices | **OK** (all 6 steps). The owner had not noticed any sync snackbar yet → separate check of the C13 messages given. |
| 2026-11-04 | #219 | Sync messages (C13) | **No snackbar on PC or phone.** Cause: `SnackbarHost` had a constant `IsVisible="False"` since T-019 (never bound) — so Paid → Undo and the Windows reminder summary were never visible either → fixed in #220. |
| 2026-11-04 | #220 | Snackbar visible: Paid → Undo, start/Sync now/sent/received messages, waiting for the network once, switch off hides all, Windows reminder summary | **OK** (all 7 steps). |
| 2026-11-05 | #222 | T-034: own key + `signing.props` (`$(USERPROFILE)` paths), new Android OAuth client, reinstall + replace from the cloud, new icon/splash/notification icon | **OK**. Old debug-key Android client may be deleted. |
| 2026-11-04 | #203 | Same bill renamed differently on both devices (sync off on both, then on with "Merge") | OK — the later edit won on both devices. Found C14 (off/on is a new first join). |
| 2026-11-02 | #199 + #194 | Windows menu icons, theme change; sync sign-in in airplane mode | **OK**: icons visible, themes switch correctly at once (C5 solved — the root cause was the failing `FontImageSource` rendering), no crash; offline check shows a clear message. |
| 2026-11-02 | #181 + #184 | T-028h, T-029 | Help tables OK; automatic copies, copy before import, JSON export (~100 000 lines) and import on the emulator OK. Windows theme still broken (C5); Imports buttons (C6); sections still feel reloaded every time (C7). |
| 2026-11-02 | #174 + #175 + #177 (phone, Windows) | T-028g fixes, menu switching, scrolling, Windows buttons/theme | Phone: much faster, slight lag on fast menu switching, no hang; Imports help OK; payment row OK; help zoom works but tables are squeezed to the page width without sideways scrolling; Imports profile buttons have different widths in portrait. Windows: dark theme first switch **still broken** after #174 and #177. Tablet pending. |
| 2026-11-02 | #165 | T-028d Settings sections (phone, tablet, Windows) | OK except: Imports has no help; Windows first switch to Dark draws wrongly; Imports profile buttons cut; bill payment row squeezed by "Cancel payment" → T-028g. Paid-bill reminder: not reproduced (several tries). Fast menu switching slows down, ANR + one hang on the tablet → T-028b. Owner wishes pinch-zoom in Help → T-028g. |
| | #149 | Reminder time 22:30 kept; list without red marks after a bill change | pending |
| 2026-10-29 | #125 | T-027 import of the owner's sheets | Import successful. Request: total water consumption per month → #130. Owner: no validity dates for tariff components needed (old data is for information). |
| 2026-10-29 | #130 | Reports with real data | Gas missing in Unit cost / Calculated vs billed (no tariff, equalized months excluded, contract start, link start today) → #133, #134; 20 years froze the UI → #133 |
| | #124 | T-028 reminders | pending |
| 2026-10-29 | #116 + #117 | T-026 import wizard; report colours + calendar | Colours/calendar OK; Shift+wheel does not scroll the table. Import: dates are reading dates (not periods), one column per meter position with 6 replacements → T-027 |
| 2026-10-29 | #114 | T-023b reports polish | OK (requests: coloured property headers, calendar for custom period → #117) |
| | #111 | T-025 templates and drafts | pending |
| 2026-10-28 | #108 | T-024 charts | OK |
| 2026-10-28 | #105 | T-023 reports | 6 steps OK. **Usability**: filter buttons do not fit (Windows), want drop-downs with check boxes; multi-property columns hard to assign; Android spinner keeps turning on first open → T-023a |
| 2026-10-28 | #102 | T-022 overview | OK. Somewhere a text does not wrap (emulator) — to collect on a real phone/tablet in the polish pass |
| 2026-10-27 | #95 + #97 + #99 | T-021 equalized + tax; line amount from the bill (owner's heating bill matches to the cent) | OK. Android icons shown as boxes — stale local build; fixed by a fresh clone (no code issue) |
| | #93 | T-020a: heating preset with quantities from the bill (owner's real bill), VAT on total | pending |
| | #91 | Month filter on Bills and Payments | pending |
| 2026-10-26 | #89 | T-020 payments | OK |
| 2026-10-26 | #86 | T-019 bills, Paid + Undo, snackbar | OK. Request: month filter (plus whole year / all years) → Sonnet helper for Bills and Payments |
| 2026-10-25 | #81 + #82 | T-018 charges screen; VAT from rounded net | OK |
| 2026-10-25 | #79 | T-017e: VAT code reuse, arrows, double tap | OK |
| 2026-10-24 | #74 | T-017 tariffs + VAT rates | Presets, prices, VAT, steps, examples, help OK. **Bugs**: VAT code cannot be reused after delete (DbUpdateException, unique index incl. deleted rows); disabled reorder arrows look enabled and open the editor (Windows); double tap opens two editors (Android) → T-017e. VAT history check postponed to data import. |
| 2026-10-24 | #69 + #70 | Selection kept on return; in-app help | OK |
| 2026-10-24 | #66 | Restore on Windows and Android (save to Downloads, reinstall, restore incl. photos), bad file refused | OK |
| 2026-10-24 | #60 | T-017a navigation guard: menu → section list, Save changes? on menu / Back / window ✕ | OK, except: Windows wide, after contract → Readings → Objects the property is **not selected** any more (spec: stays selected) → known issue |
| | #38 + #43 + #45 + #46 + #48 | **Readings entry**, Meters, replacement wizard; Contracts; robustness (bad input in all fields), stay after Save, swatches, error log | pending |
