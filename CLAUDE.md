# CLAUDE.md — rules for coding sessions

You are a coding session working on **HomeUtilities**, a .NET MAUI app (Windows + Android, iOS later)
for tracking utility meters, bills, payments and property taxes across several properties.

The project is run by a project lead (a separate Claude session) who writes tasks and reviews PRs,
and by the owner (Sergej) who tests the app manually. You implement **exactly one task** per session.

## Before you start

1. Read, in this order: `docs/PROGRESS.md`, the task file you were given (`docs/tasks/T-NNN-*.md`),
   `docs/ARCHITECTURE.md`, and the sections of `docs/DATA_MODEL.md` the task references.
2. If the task contradicts these documents, or something is unclear enough that you would have to
   guess an architectural decision, **stop and write the question into the PR description**
   (section "Open questions") instead of guessing. Small, local decisions you make yourself and list
   them under "Decisions made".

## Workflow

- Branch: `task/T-NNN-short-name` from the latest `main`. If your environment assigns a branch name
  (e.g. `claude/...`), use it — but the PR title must start with `T-NNN:`.
- Commit in small logical steps. Commit messages in English, imperative mood
  (`Add meter register entity`).
- When done: push the branch and open a PR titled `T-NNN: <task title>`.
  Fill the PR description using `.github/pull_request_template.md` — that description **is your report**.
- **Do not edit `docs/PROGRESS.md`** — the project lead maintains it (avoids merge conflicts). Put notes and
  known issues into the PR description instead.
- **Never merge** your own PR and never push to `main`. The project lead merges.

## Scope discipline

- Do only what the task asks. No drive-by refactoring, no renaming outside the task, no new
  dependencies unless the task allows them. If you see something worth fixing, list it under
  "Suggestions" in the PR.
- Do not edit `docs/ARCHITECTURE.md`, `docs/DATA_MODEL.md`, `docs/PAYMENT_TYPES.md`, `docs/ROADMAP.md`,
  `docs/PROGRESS.md` or other task files.
  Propose changes in the PR description instead.

## Code rules

- **All code, comments, identifiers, log messages, commit messages and docs are in English.**
  User-facing UI text goes only into resource files (`.resx`), never hard-coded in XAML or C#.
- C# latest, nullable reference types enabled, warnings as errors (see `Directory.Build.props`).
- Money is always `decimal`. Never `double`/`float` for money, quantities of readings or tariffs.
- Dates: `DateOnly` for business dates (reading date, bill date, valid-from), `DateTimeOffset` (UTC)
  for technical timestamps (`CreatedAt`, `UpdatedAt`).
- Every persisted entity derives from `SyncEntity` (GUID id, UTC timestamps, soft delete).
  Never hard-delete user data; set `IsDeleted = true`.
- `HomeUtilities.Core` must not reference EF Core, MAUI or any UI/platform package.
- `HomeUtilities.ViewModels` must not reference MAUI. Platform features go through interfaces
  defined in Core/ViewModels and implemented in `HomeUtilities.App`.
- XAML pages stay thin: bindings and layout only. Logic lives in ViewModels.
- **UI tasks follow `docs/UI_GUIDELINES.md`** (platform icons, button hierarchy, dialogs, Back behaviour).
- Use CommunityToolkit.Mvvm source generators (`[ObservableProperty]`, `[RelayCommand]`).
- App startup order (bugs found by the owner, 2026-10-20): never inject pages or `AppShell` into the `App`
  constructor — resolve them in `CreateWindow`, after `InitializeComponent()` has loaded the resource
  dictionaries. Services with startup side effects (language/culture, theme) are resolved in the `App`
  constructor **before** anything reads localized text. CI only builds the App; it never runs it.
- Package versions are managed centrally in `Directory.Packages.props`.

## User guide (EN + RU)

- `docs/user-guide/en.md` and `docs/user-guide/ru.md` describe the app for the owner. **Every task that adds or
  changes something the user sees updates both files in the same PR** (new screen, field, button, rule, warning).
  Describe what the feature is for and how it relates to the other records (property – supplier – contract – meter),
  not only which buttons exist. Move items out of "Coming soon" when they are done.
- Use exactly the UI wording from `AppResources.resx` / `AppResources.ru.resx`. The Russian file is the only
  place where non-English text is allowed in `docs/`; write natural Russian, not a word-by-word translation.
- Examples are invented (no real names, numbers or IBANs). Tasks without visible changes: write "n/a" in the PR.

## Tests

- Every piece of domain or calculation logic gets unit tests (xUnit) in the matching test project.
- Calculation tests use concrete numbers with the expected result written out, e.g.
  `5 m3 * 1.41 EUR = 7.05; VAT 21% = 1.48; total 8.53`.
- All tests must pass locally before you open the PR: `dotnet test HomeUtilities.Tests.slnf`.
- UI cannot be tested in the Linux sandbox. For UI tasks, list in the PR exactly what the owner should
  click and what he should see ("Manual test steps").

## Build environment

- .NET 10 SDK. If `dotnet` is missing in your environment, run `scripts/setup-dev.sh` (Linux).
- On Linux you can build and test everything except the Windows target of the MAUI app.
  The Windows target is built by CI (`.github/workflows/ci.yml`). Mention in the PR what you could not
  build locally.

## Privacy — this repository is PUBLIC

- Never commit real personal data: names, addresses, customer/account numbers, meter serial numbers,
  IBANs, logins, passwords, real bills or the owner's Excel files.
- Test data must be invented. Fixture files go to `tests/**/Fixtures/` and must be synthetic.
- Never commit API keys, OAuth client secrets or tokens. Use the mechanisms described in
  `docs/ARCHITECTURE.md` (section "Secrets").
