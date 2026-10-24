# Workflow

## Roles
- **Project lead** (Claude, planning session): architecture, data model, task files, PR review, merge.
- **Coding session** (Claude Code): implements one task, tests, opens a PR with a report. Rules: `CLAUDE.md`.
- **Owner** (Sergej): passes tasks to coding sessions, pulls `main`, tests the app manually, decides on
  functionality.

## Cycle of one task
1. Lead writes `docs/tasks/T-NNN-name.md` and sets the task to `Ready` in `docs/PROGRESS.md`.
2. Owner starts a coding session with the prompt:
   > Implement task `docs/tasks/T-NNN-name.md` following `CLAUDE.md`.
3. Coding session works on branch `task/T-NNN-...`, opens a PR; the PR description is the report.
4. CI runs (Linux + Windows). Owner tells the lead "T-NNN ready" (or sends the PR link).
5. Lead reviews code + CI. Either:
   - requests changes (a comment on the PR, which the owner passes to the coding session), or
   - squash-merges and tells the owner what to test manually.
   The review includes the user guide (`docs/user-guide/en.md` + `ru.md`): both updated, consistent, readable.
6. Owner pulls `main` in Visual Studio, tests, reports results. Bugs become new tasks;
   a broken merge is reverted.

## CI duration
Code changes: ~4 min (tests ~25 s; Android build ~3 min and Windows MAUI install + build ~3 min run in
parallel). Docs-only changes (`docs/**`, `*.md`): builds skipped, checks pass in seconds.

## Diagnosing CI failures
Failed tests appear as check-run annotations (`report_trx.py`):
`gh api repos/gromichek/HomeUtilities/check-runs/<id>/annotations`. Compiler errors appear there too.

## Task file template
```
# T-NNN: Title
Phase: N · Depends on: T-xxx · Size: S/M/L

## Goal
## Context (links to ARCHITECTURE / DATA_MODEL sections)
## Scope — do
## Scope — do NOT
## Acceptance criteria
## Tests required
## Manual test steps for the owner (if UI)
```
