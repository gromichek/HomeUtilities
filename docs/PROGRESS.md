# Progress

Maintained by the project lead only (coding sessions do not edit it). Statuses: `Planned`, `Ready` (task file written), `In progress`,
`In review` (PR open), `Done` (merged), `Reverted`.

## Current

| Task | Title | Status | PR | Notes |
|---|---|---|---|---|
| T-008 | Bills, custom fields, payments, allocations, balances | Ready | | |

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

## Known issues / tech debt

- `MainViewModel` subscribes to `ILanguageService.LanguageChanged` and never unsubscribes while being
  registered as transient. Harmless with a single page; resolve when navigation is introduced (T-011),
  e.g. weak-reference messenger or `IDisposable` view models.
- App icon and splash are placeholder SVGs (no MAUI template available offline).
- Cloud coding sessions cannot build the MAUI App; rely on CI for Android/Windows.

## Manual test log (owner)

| Date | Build / PR | What was tested | Result |
|---|---|---|---|
| 2026-10-19 | #1 | Start on Windows and Android emulator | OK (VS framework selection needed, fixed by #4) |
