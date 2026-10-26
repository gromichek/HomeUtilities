# Progress

Maintained by the project lead only (coding sessions do not edit it). Statuses: `Planned`, `Ready` (task file written), `In progress`,
`In review` (PR open), `Done` (merged), `Reverted`.

## Current

| Task | Title | Status | PR | Notes |
|---|---|---|---|---|
| T-021 | Equalized plans + settlement, tax installments | Ready | | |

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
| 2026-10-26 | — | Bill due date: false "due before issue date" seen twice, not reproducible → Sonnet helper. Heating line 0,233 MWh × 73,76 = 17,19 vs 17,22 on the bill (supplier bills unrounded quantity) → optional line amount from the bill (helper after the due-date fix) | |
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
