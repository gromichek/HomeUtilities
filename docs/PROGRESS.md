# Progress

Maintained by the project lead only (coding sessions do not edit it). Statuses: `Planned`, `Ready` (task file written), `In progress`,
`In review` (PR open), `Done` (merged), `Reverted`.

## Current

| Task | Title | Status | PR | Notes |
|---|---|---|---|---|
| T-012 | Properties — list, edit, parameters, shared UI patterns | Ready | | |

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

## Known issues / tech debt

- `ViewModelBase` uses `WeakReferenceMessenger.RegisterAll` (reflection) — check Android release trimming; switch to explicit `Register<T>` if it breaks.
- App icon and splash are placeholder SVGs (no MAUI template available offline).
- Cloud coding sessions cannot build the MAUI App; rely on CI for Android/Windows.
- `ContractValidator` does not yet validate `Contract.CustomFields` (use `CustomFieldValidator.ValidateAll`) — pick up in T-013.
- SQLite decimals lose trailing zeros (`-12.10` → `-12.1`); numerically equal, UI always formats amounts.
- Snapshot `ChargeWarning.Args` come back as `JsonElement` (display only).
- Timestamps of items inside JSON columns (links, custom field definitions) are not stamped; the owner row is (sync is row-level).
- `AggregateMerger` adds new children via `db.Add` — UI must set the child's FK (e.g. `PropertyParameter.PropertyId`) before saving.
- Saving an id that exists only as a soft-deleted row is not supported (resurrect needed for sync, T-030).
- Unique `VatRates.Code` / `Categories.SystemKey` include soft-deleted rows.

## Manual test log (owner)

| Date | Build / PR | What was tested | Result |
|---|---|---|---|
| 2026-10-19 | #1 | Start on Windows and Android emulator | OK (VS framework selection needed, fixed by #4) |
| | #21 | Database created + seeded on Windows / Android, no duplicates after restart | pending |
| 2026-10-20 | #24 | Start on Windows / Android | **Crash**: `StaticResource not found for key PrimaryLight` (AppShell built before App resources) — fixed in hotfix PR |
| 2026-10-20 | #27 | Restart with Russian selected | **Bug**: menu in English, Settings in Russian (LanguageService created lazily after Shell) — fixed in hotfix PR |
| | hotfix 2 | Shell menu, settings (language, theme), adaptive layout on Windows / phone / tablet | pending |
