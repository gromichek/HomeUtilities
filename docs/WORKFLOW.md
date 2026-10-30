# Workflow

## Roles
- **Project lead** (Claude, planning session): architecture, data model, task files, PR review, merge.
  A new lead session starts from `docs/LEAD_HANDOVER.md`.
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

## Who fixes what (owner decision 2026-10-24)
- **Trivial blockers** of a PR (missing `using`, a test broken by merging two parallel PRs, a typo): the lead
  fixes them directly on the PR branch.
- **Urgent breakage** on `main` (the app does not start, data loss): the lead fixes it at once as a hotfix PR.
- **Everything else** (bugs from the owner's tests, anything that needs analysis and tests):
  - PR still open → the lead writes a review comment; the owner tells the same session
    "Fix the review comments on the PR";
  - already merged → the lead writes a small fix task (`T-NNNx`), the owner runs it in a new session, **or**
  - for small fixes the lead may start a helper agent itself, in its own worktree and branch, and reviews the
    result like any PR. **Helpers always run on Sonnet** (coding sessions use Sonnet too: the worker is cheaper
    than the lead). The lead never uses its own model for helpers.

## Owner troubleshooting
- **Android shows boxes instead of icons** (or other resources look old) although `main` is fine: a stale local
  build. Build → Clean Solution, close Visual Studio, delete `src/HomeUtilities.App/bin` and `obj`, rebuild. If that
  is not enough, a fresh clone helps (2026-10-27). Data on the device is kept unless the app is uninstalled — make a
  backup first (Settings → Backup).

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
