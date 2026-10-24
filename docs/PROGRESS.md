# Progress

Maintained by the project lead only (coding sessions do not edit it). Statuses: `Planned`, `Ready` (task file written), `In progress`,
`In review` (PR open), `Done` (merged), `Reverted`.

## Current

| Task | Title | Status | PR | Notes |
|---|---|---|---|---|
| T-016a | Attachments infrastructure + meter/reading photos | Ready | | |

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
- No "Copied" confirmation on Windows (toast disabled there) until the in-app snackbar (T-019).
- Link-list logic duplicated in supplier and contract editors (extract helper VM later).
- FlexLayout clips first item per row on Android — avoid it (UI_GUIDELINES §6a).
- Unique `VatRates.Code` / `Categories.SystemKey` include soft-deleted rows.

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
| | #38 + #43 + #45 + #46 + #48 | **Readings entry**, Meters, replacement wizard; Contracts; robustness (bad input in all fields), stay after Save, swatches, error log | pending |
