# Progress

Every task PR updates this file. Statuses: `Planned`, `Ready` (task file written), `In progress`,
`In review` (PR open), `Done` (merged), `Reverted`.

## Current

| Task | Title | Status | PR | Notes |
|---|---|---|---|---|
| T-005 | Reading-window fallback fix, VAT catalog, tariffs | In review | | 62 new tests (259 total in Core). Window fallback: late reading preferred over early one. |

## Done

| Task | Title | Merged | Notes |
|---|---|---|---|
| T-001 | Solution skeleton, MAUI shell, localization, CI | 2026-10-19, #1 | App not buildable in the cloud sandbox (Android SDK download blocked); Android + Windows builds verified by CI. |
| — | Docs: Android tablet, adaptive layouts | 2026-10-19, #2 | ARCHITECTURE §4.9 |
| T-002 | Domain base types | 2026-10-19, #3 | 52 new tests. `Versioned.At` returns the first item on duplicate ValidFrom; `Validate` flags it. |
| — | Windows first TFM on Windows hosts | 2026-10-19, #4 | VS defaults to Windows Machine |
| T-003 | Properties, suppliers, links, contracts | 2026-10-19, #6 | 57 new tests (109 total). Deleted contract link does not override supplier link. |
| T-004 | Meters, readings, consumption calculator | 2026-10-19, #8 | 88 new tests (197 total). Window fallback refined in T-005. Replacement meter does not copy Digits/SerialNumber (UI pre-fills). |

## Known issues / tech debt

- `MainViewModel` subscribes to `ILanguageService.LanguageChanged` and never unsubscribes while being
  registered as transient. Harmless with a single page; resolve when navigation is introduced (T-010),
  e.g. weak-reference messenger or `IDisposable` view models.
- App icon and splash are placeholder SVGs (no MAUI template available offline).
- Cloud coding sessions cannot build the MAUI App; rely on CI for Android/Windows.

## Manual test log (owner)

| Date | Build / PR | What was tested | Result |
|---|---|---|---|
| 2026-10-19 | #1 | Start on Windows and Android emulator | OK (VS framework selection needed, fixed by #4) |
