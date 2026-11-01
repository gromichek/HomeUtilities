# Lead handover

Written by the project lead on 2026-10-30 so that a **new project-lead session** can take over with a clean
context. Read this file first, then `docs/PROGRESS.md`, `docs/ROADMAP.md`, `docs/WORKFLOW.md`, `CLAUDE.md`,
`docs/ARCHITECTURE.md` (incl. §11 decision log), `docs/DATA_MODEL.md`, `docs/UI_GUIDELINES.md`.
The lead keeps this file up to date when the owner makes a new standing decision.

Prompt for a new lead session:
> You are the project lead of HomeUtilities (github.com/gromichek/HomeUtilities). Read `docs/LEAD_HANDOVER.md`
> and the documents it lists, then continue.

## 1. People and communication
- **Owner: Sergej** (Latvia, hobbyist C# developer, used to WinForms). Chat with him **in Russian**; everything in
  the repo (code, comments, commit messages, docs, task files) **in English**. The only exception is
  `docs/user-guide/ru.md` and `AppResources.ru.resx`.
- He tests on **Windows (wide window)** and the **Android emulator**; a real phone/tablet pass comes later (T-028a).
- He runs coding sessions himself in the browser (cloud Claude Code) and **always picks Sonnet** for them.
  He starts a session with: "Implement task `docs/tasks/T-NNN-name.md` following `CLAUDE.md`."
- When a session is done he writes "T-NNN готова" (or sends the PR link). Then the lead reviews, fixes trivial
  blockers, merges and answers in Russian: what changed (short, plain words) + numbered **manual test steps** +
  open questions that need his decision.
- He reports bugs with screenshots and short descriptions. When he says "просто зафиксируй / пока ничего не
  делай", only record it (PROGRESS → Known issues), do not start work. When he says "дай задачу", write a task file.
- Keep answers short and concrete; explain technical causes in one or two plain sentences, no jargon dumps.
- He likes being asked before larger decisions, with a recommended option.

## 2. What the lead does (and does not do)
- Writes task files `docs/tasks/T-NNN-*.md` (template in WORKFLOW.md), with concrete synthetic numbers and
  manual test steps. One task = one coding session. Sets status in PROGRESS.
- Reviews PRs: code against the task, CLAUDE.md rules, licences of new packages (transitive too), tests with
  concrete numbers, `SampleDataGenerator` extended for new entities, user guide EN + RU updated and consistent,
  no personal data. Then CI must be green on **linux**, **windows** and **changes** jobs.
- **Squash-merges** (REST, see §2 commands), then a small docs PR that updates PROGRESS
  (Current → Done with a one-line note, Known issues, Manual test log). Coding sessions never edit PROGRESS.
- Maintains ROADMAP, PROGRESS, WORKFLOW, UI_GUIDELINES, DATA_MODEL, ARCHITECTURE decision log, CLAUDE.md.
- **Who fixes what** (owner decision 2026-10-24, WORKFLOW.md): trivial PR blockers and urgent breakage on `main`
  → the lead fixes directly; small fixes after the owner's tests → the lead may start **helper agents, always
  `model: sonnet`** (never the lead's own model — "the worker is cheaper than the lead"); everything else → a task
  file for a session.
- The lead's own sandbox cannot restore NuGet packages → **CI is the only build and test** for the lead.
  Coding sessions can run `dotnet test HomeUtilities.Tests.slnf` (Linux) but not the Windows App target.

### Practical commands
- Repo clone: `/home/claude/homeutilities` (the session's cwd `/home/claude` itself is not a git repo).
- `gh` works only through the **REST API** in these sessions (GraphQL is blocked, so `gh pr create/merge/checks`
  fail). Create a PR: `gh api repos/gromichek/HomeUtilities/pulls -f title=… -f head=<branch> -f base=main
  -F body=@body.md`; squash-merge: `gh api -X PUT repos/gromichek/HomeUtilities/pulls/<n>/merge
  -f merge_method=squash`. Deleting remote branches through the API is blocked by the proxy — leave them
  (the owner can clean up on GitHub).
- CI status: `gh api repos/gromichek/HomeUtilities/commits/<sha>/check-runs`; failed tests/compiler errors as annotations:
  `gh api repos/gromichek/HomeUtilities/check-runs/<id>/annotations` (written by `report_trx.py`).
- Code PR CI ≈ 4 min; docs-only PRs (`docs/**`, `*.md`) skip the builds.
- **Helper worktree:** `git worktree add .claude/worktrees/<name> -b fix/<name> origin/main` inside the repo
  (`.claude/worktrees/` is git-ignored — never commit it; a gitlink slipped in once, removed in #77). Give the
  helper the absolute worktree path, the task text and "follow CLAUDE.md, push the branch, open a PR".
  After merging: `git worktree remove …` and delete the local branch.
- **resx conflicts:** `.gitattributes` has `*.resx merge=union`, but GitHub's web merge ignores it. Resolve
  locally: merge `origin/main` into the PR branch, keep **both** sides, check for duplicate keys, push.
- **Parallel PRs** often break each other (missing `using`, new constructor arguments, lost
  `[ObservableProperty]` attribute after a merge) — check the merge result, not only each PR alone.
- **Flaky performance tests:** limits must catch gross slowdowns only (CI machines vary). Current limits:
  `LargeDataTests` 5 s, `ListPerformanceTests` 3 s.
- Wait for **all** checks (incl. windows) before merging — once a PR was merged before the Windows job ended.
- Commit/PR attribution lines are given by the environment; PR bodies from a file (`-F body=@file`).

## 3. Standing owner decisions (keep them in every task)
| Date | Decision |
|---|---|
| 2026-10-19 | .NET MAUI (.NET 10); Android mandatory, Windows (unpackaged), Android tablet; iOS later (T-033). |
| 2026-10-19 | Public repository → **no personal data** in the repo (names, addresses, account/customer numbers, meter serials, IBANs, logins, real bills, the owner's Excel workbook). Examples and fixtures are invented. |
| 2026-10-19 | Supplier portal passwords are never stored; links open in the system browser. |
| 2026-10-19 | UI languages EN + RU (extensible); all UI text in `.resx`. |
| 2026-10-19 | Bills entered as a total, lines optional; the app compares with its own calculation. |
| 2026-10-20 | Startup order rules (CLAUDE.md) after the two startup crashes. |
| 2026-10-23 | Stay in the editor after Save (no auto-close). |
| 2026-10-24 | Fix delegation as in §2; helpers only on Sonnet. |
| 2026-10-24 | Bilingual **user guide** `docs/user-guide/en.md` + `ru.md`, updated by every task with visible changes; shown in the app (Help, "?" opens the section). Diagrams: `.mmd` → `.svg` via `scripts/render-guide-diagrams.sh`. |
| 2026-10-24 | Every task with new entities extends `SampleDataGenerator` (Debug sample data is the owner's test bed). |
| 2026-10-24 | Menu always opens the section root; unsaved-changes dialog on every exit (menu, Back, window ✕). |
| 2026-10-25 | Only **free, permissive open-source** dependencies (MIT/Apache/BSD/MS-PL), transitive ones too. `SixLabors.*` stays at 1.x. Charts are drawn with Microsoft.Maui.Graphics; Excel via ClosedXML. |
| 2026-10-25 | VAT is computed from the **rounded net**. |
| 2026-10-26 | VAT on the bill total by default (`Contract.VatCalculation = OnTotal`, PerLine optional) — matches his heating bill. |
| 2026-10-26 | Quantity from the bill (heating) with an optional **line amount from the bill** (the supplier multiplies the unrounded quantity, so 0.233 × price ≠ printed amount). |
| 2026-10-26 | Bills and Payments have a period filter: all years / year / month. |
| 2026-10-29 | Choice dialogs per UI_GUIDELINES §3a (title top-left, mark before text, Cancel/OK bottom-right, full screen on phones). No animated spinners — text "Calculating…/Loading…". |
| 2026-10-29 | **Reminders:** Android with minimal battery use — no foreground service, no exact alarms, no periodic background work; Windows: an in-app summary when the app opens is enough. |
| 2026-10-29 | A later phase does memory and battery optimisation (T-028b) and a real-device polish pass (T-028a); the owner collects findings for T-028a first. |
| 2026-10-29 | Tariff components get **no validity dates** (old imported data is for information). |
| 2026-10-29 | A month is equalized iff an equalized plan covers it; months before the first plan are "by consumption". Equalized months still count the real (tariff) cost in reports. |
| 2026-10-29 | Export "like my workbook" (T-027a) — one sheet per contract from the import profile. |
| 2026-10-30 | Closing the remainder of a bill **without a payment** is not planned. Workaround: such months are treated as "by consumption" (plan starts later). |
| 2026-10-30 | Meter–contract links start at the meter's installation (not "today"); link dates are editable; repair under Settings → Imports. |
| 2026-10-31 | Reminders: **no quiet hours** — the configured time of day is used exactly. Inexact alarms (≈1 min late) are fine. |
| 2026-11-01 | A window **lower than 480 dp** (phone in landscape) uses the phone layout (single pane, ✓ Save at the top), whatever its width (T-028a). |

## 4. The owner's data (private — never in the repo)
- He keeps ~20 years (2004–2026) of history in an Excel workbook: one sheet per supplier, one row per month,
  3 properties, ~15 contracts. The lead analysed it locally only; task files use invented numbers of the same
  shape. Never copy values, names, serials or the file itself into the repo, an artifact or a PR.
- Meaning of the date column differs per sheet (details in T-027): water = reading date (1st → previous month in
  early years, later the 26th with a window 25–28); heating = 1st of the month after the service month;
  electricity = 1st = readings of the previous month; fixed-amount sheets = payment date on the 2nd for the
  previous month; internet became prepaid in 2023 (first prepaid row = two months + connection fee).
- Meter replacements (≈6 per position) appear in four patterns (T-027 §Background).
- Advice already given to him: set the **reading window 28–5** on the gas and electricity contracts (readings
  on the 1st belong to the previous month), use the meter-link repair, re-import gas after fixing plans.
- He also shared a real heating bill (PDF) — used only to verify the T-020a rules; never commit it.

## 5. Current state (2026-11-01)
- All tasks up to **T-028c** are merged, plus fixes #147 and #149 (reminders, help link on Windows, no quiet hours).
- **Ready** (owner runs the sessions): **T-028e** reliable reminders on Android (still broken after #149: a new bill's
  reminder is not handed over / every row "not handed"; cause not confirmed — task asks for an own AlarmManager
  scheduler), **T-028a** layouts + cut texts + Android system Back, **T-028d** Settings as a section list (after both).
- Owner findings from phone/tablet/Windows are in PROGRESS "T-028a findings". Tablet has the same UI issues as the phone.
- Phone Android 16, tablet Android 13. Back cause open: predictive back (targetSdk 36) or pushed pages on the phone
  (owner's view) — T-028a finds out. Owner started **T-028e** (2026-11-01).

## 6. Next steps (ROADMAP)
1. Review and merge T-028e, T-028a, T-028d as the owner finishes the sessions.
2. **T-028a** device polish (after the owner collects findings on a real phone/tablet; known: some texts do not
   wrap on narrow screens).
3. **T-028b** memory and battery optimisation.
4. **T-029** automatic local backups, JSON export/import (manual backup file exists since T-017c).
5. **T-030–T-032** Google sign-in, sync engine (change logs in Drive appDataFolder), attachments sync.
   Secrets per ARCHITECTURE §9. Note: saving an id that exists only as a soft-deleted row needs "resurrect" for sync.
6. **T-033** iOS. **T-025b** contract templates (optional, low priority).

## 7. Pitfalls learned (details in UI_GUIDELINES §6a and PROGRESS)
- Shell re-applies plain query dictionaries on Back → editors must apply a query once (`EditorPageBase._queryApplied`).
- Never sync-over-async on the UI thread (restore deadlock #66) — use `Task.Run(...).GetAwaiter().GetResult()` where
  a synchronous call is unavoidable at startup.
- Windows: CommunityToolkit Toast unsupported in unpackaged apps → the in-app snackbar (`ISnackbarService`).
- Selection highlight needs an explicit base stroke; FlexLayout clips items on Android; `MaximumHeightRequest` in a
  `*` row centres lists on Android.
- Ambiguous types on Windows builds (`Window`, `Grid.SetRow`) — CI windows job catches them, the lead's sandbox cannot.
- Android "boxes instead of icons" = stale local build (Clean, delete bin/obj, or fresh clone) — WORKFLOW.md.
- Long lists: never create more than ~100 row views at once; load off the UI thread.
