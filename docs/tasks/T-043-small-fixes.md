# T-043: Small fixes — Overview "all paid" text, two buttons with the same Russian label, flaky tests
Phase: 9 · Depends on: T-042a (#286, #289, #290) · Size: S

Collected items from `docs/PROGRESS.md` ("Collected for the next session task" and "Tech debt"). Nothing else.

## Scope — do
1. **Overview → Money card.** `OverviewMoneyEmpty` ("Счетов и платежей пока нет." / EN) is shown whenever nothing is open,
   overdue or prepaid and the balance is 0 — also when bills exist and all are paid (owner's screenshot 2026-11-07).
   New resx key (EN + RU), e.g. "Всё оплачено — долгов и предоплат нет." / "Everything is paid — no debts or prepayments.",
   shown when bills or payments exist (for the Overview property filter); the old text only when there are really no bills
   and no payments. The decision in the ViewModel, unit-tested.
2. **Two buttons with the same Russian text** (T-040 finding): `SettingsBackupSave` (create a new backup file) and
   `BackupCopySave` (save one automatic copy elsewhere) both say «Сохранить копию…». Give them distinct texts, e.g.
   `SettingsBackupSave` «Создать резервную копию…» and `BackupCopySave` «Сохранить как…» (check EN for the same problem);
   update every text that names the button (`SettingsBackupAutoHint` names «Сохранить копию…») and the guide EN + RU.
   Buttons stacked full width if the longer text no longer fits (CLAUDE.md UI checklist, Russian at 360 dp).
3. **Flaky tests.**
   - `SnackbarServiceTests.TimerOfAReplacedMessage_DoesNotHideTheNewOne` (timing, failed once in six local runs): make it
     deterministic — a fake timer / time provider or an awaitable hook instead of real delays. No behaviour change of the
     service.
   - Look through the ViewModels tests for the same pattern as the fixed `DraftBillsTests` (a fire-and-forget command
     `Execute` racing an awaited call or an assertion): list them in the PR; fix those that are clearly racy (await
     `ExecuteAsync` of the async command), at most a handful.
   - Run each changed test class 20 times locally and say so in the PR.

## Not in this task
No other UI changes, no performance work.

## Tests
Money text: no bills/payments → empty text; bills all paid, balance 0 → "all paid" text; with property filter. Existing tests green.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist (only the Backup buttons and the Money
card); guide EN + RU.

## Manual test steps for the owner (short)
1. Overview with all bills paid → "Всё оплачено — долгов и предоплат нет.".
2. Settings → Backup: the new button texts; an automatic copy → «Сохранить как…»; texts fit on the phone in portrait.
