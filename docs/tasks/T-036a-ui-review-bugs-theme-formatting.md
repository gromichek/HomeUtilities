# T-036a: UI review part 1 — bugs, theme and styles, formatting, Charges, texts
Phase: 9 · Depends on: T-035 · Size: L

Input: `docs/UI_REVIEW_2026-11-05.md` (findings R-NN — read the finding for the details, the code places and the proposed
fix) and `docs/UI_GUIDELINES.md` **revision 1** (agreed with the owner 2026-11-05 — the rules you implement against).
The review is split into three tasks; this is the first. **Do only the findings listed here**; the others are T-036b/c
(listed at the end so you do not start them).

## Scope — do

### 1. Bugs (wrong or hidden information)
- **R-01** Toolbar colours: set `Shell.BackgroundColor` / `ForegroundColor` / `TitleColor` (and the status bar) for
  both themes in `AppShell`; toolbar icon sources in the toolbar foreground colour (§8). Check ✓, ?, ☰, ← in both themes.
- **R-04 + R-06** `IDialogService.ChooseAsync` → the app's own `SingleChoiceDialogView` (radio rows, **current value
  marked**), returning the **index / Id** of the chosen item, not its text. Change all ~27 call sites so the record is
  taken by index/Id (`FirstOrDefault(o => o.Name == picked)` must disappear). The system action sheet stays only for
  commands (Camera / Gallery). Unit-test a case with two records of the same name.
- **R-02** No `DatePicker.Format` text: show the ViewModel's localized month text, open the picker on tap (all places
  listed in R-02).
- **R-35, R-36, R-37, R-38** Charges: `—` for a contract that could not be calculated (≈ for a partial sum, §9); bind the
  existing Net/VAT/Total labels, one line per service; separate "no readings for {month} — last {date}" warning with a
  link to Readings; price text per R-37; one "Открыть тариф" per tariff; header status "✓ Рассчитано" aligned with ⚠;
  one action name with Bills.
- **R-43** Sync details: localized short messages from resx (UI text in code is against CLAUDE.md); file names / ids only
  in the error log; section collapsed by default. **R-44** "Отключить аккаунт Google…".
- **R-22, R-25, R-26** "Отметить оплаченным" instead of "Оплачено" as button text; hint only with the button; the payment
  row never shows "кнопка «Оплачено»" (use "быстрая оплата" or nothing).
- **R-28** `—` for an empty computed VAT. **R-18** hide a 0,00 recalculation line. **R-33** hot-water hint only for hot
  water. **R-39** report group cell 2 lines. **R-41** ≈ footnote only when a ≈ cell exists. **R-46** empty texts per §5.2
  (no "button below"). **R-45** meter history / footer texts.

### 2. Theme and styles (one place each — `Styles.xaml`, `Colors.xaml`, `Icons.xaml`)
- **R-05** Android touch targets ≥ 48 dp (buttons, icon buttons, ‹ ›, ✓ on Overview) via `OnPlatform`; glyph size unchanged.
- **R-17 + §1** Material Icons on both platforms for text icons too (no Segoe glyphs left); `IconPaid` ≠ `IconSave`.
- **R-12** every delete icon red (`DestructiveIconButton`). **R-08** switch placement per §6, `OnColor = Primary`.
- **R-15** status colours per §8 (unpaid normal text, due soon amber, overdue red, text + icon); default category
  palette without the status green/red (new categories only — never change the user's existing colours).
- **R-16** `IconChevron` on every row that opens a page; `IconOpenLink` only for web links.
- **R-20** list cards with the `Card` background. **R-51** outline / dark card colours per R-51 (check contrast ≥ 3 : 1).
- **R-07 (part)** `TextButton` without horizontal padding. (The bordered field look is T-036b.)

### 3. Formatting (§9, ViewModel helpers + resx)
- **R-10** non-breaking spaces in amounts and "label amount" pairs. **R-11** own 3-letter month names (resx, EN + RU)
  for Overview ranges and chart axes.

### 4. Lists on the phone
- **R-19** 88 dp footer spacer under Bills / Payments (and every list with a FAB). **R-13** full-width action buttons on
  compact width ("Добавить платёж", "Заменить счётчик…"). **R-52** "Загрузка…" while `!IsLoaded` (Overview, Bills,
  Payments, Charges; not shown under 300 ms).

### 5. Snackbar for Undo (prepares T-036b)
- Owner 2026-11-05: the snackbar must be easy to hit — bar ≥ 56 dp high on Android, the action ≥ 48 × 48 dp (whole right
  part of the bar), above the FAB (§5.1). Apply to the existing Undo uses (Paid, prepayment). Delete-with-Undo itself is
  T-036b.

### 6. Guide
User guide EN + RU: changed texts (button names, Charges warnings, Sync details, empty texts). Exact resx wording.

## Not in this task (T-036b / T-036c — do not start)
T-036b: R-03 + choice field everywhere (no native `Picker`), R-07 field look, R-29 dialog frame, R-14, R-30, R-09,
R-21 + date lists in one column, compact-by-height on Android only, R-27, Delete only at the end of the form + delete
with Undo, R-40, R-24. T-036c: R-23, R-31, R-32, R-34, R-42, R-47–R-50.

## Tests
ChooseAsync by index/Id (two records with the same name), Charges card texts (`—`, ≈, labels, the no-readings warning
with concrete dates), month short names EN/RU, money formatting with non-breaking spaces, sync detail texts localized.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist of CLAUDE.md (new version) in the PR.
2. Owner: toolbar icons readable (light + dark, Android + Windows), choice dialogs mark the current value, Russian months
   on Windows, Charges readable, Sync details in Russian.

## Manual test steps for the owner (keep them short in the PR)
1. Android, light and dark theme: open a bill — ✓, ?, ← clearly visible.
2. New payment → contract choice: own dialog, current value marked; with two contracts of the same name the chosen one is
   saved.
3. Windows (Russian UI): Bill editor and Charges show "ноябрь 2026", not "November 2026".
4. Charges for a month without readings: `—` and "нет показаний за …"; expanded card has labels.
5. Sync → Подробности: Russian short lines.
6. Bills on the phone: the last card can be scrolled above ＋; "Отметить оплаченным" → snackbar with a big "Отменить".
