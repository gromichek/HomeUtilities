# Progress

Every task PR updates this file. Statuses: `Planned`, `Ready` (task file written), `In progress`,
`In review` (PR open), `Done` (merged), `Reverted`.

## Current

| Task | Title | Status | PR | Notes |
|---|---|---|---|---|
| T-003 | Properties, suppliers, links, contracts | Ready | | |

## Done

| Task | Title | Merged | Notes |
|---|---|---|---|
| T-001 | Solution skeleton, MAUI shell, localization, CI | 2026-10-19, #1 | App not buildable in the cloud sandbox (Android SDK download blocked); Android + Windows builds verified by CI. |
| — | Docs: Android tablet, adaptive layouts | 2026-10-19, #2 | ARCHITECTURE §4.9 |
| T-002 | Domain base types | 2026-10-19, #3 | 52 new tests. `Versioned.At` returns the first item on duplicate ValidFrom; `Validate` flags it. |
| — | Windows first TFM on Windows hosts | 2026-10-19, #4 | VS defaults to Windows Machine |

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
