# T-036c: UI review part 3 — screen-by-screen usability and the remaining details
Phase: 9 · Depends on: T-036b (#258), T-037a (#260) · Size: M

Input: `docs/UI_REVIEW_2026-11-05.md` (R-NN), `docs/UI_REVIEW_T-036a_CHECK.md` (C-NN), `docs/UI_GUIDELINES.md` revision 1
(with the T-036b additions). Read each finding for the details and code places. Last part of the UI review; do only
what is listed.

## Scope — do

### 1. Screens
- **R-23 Bill editor:** for an existing bill the Payment card right after Amounts; "Отметить оплаченным" also in the ⋮
  menu (it is not a delete, so the ⋮ rule allows it).
- **R-31 Meter title:** location + serial (or location + register/kind) so two meters in one room can be told apart in the
  editor, the reading history and the reading dialog (`MeterNames.Title`).
- **R-32 Meter → Contracts card:** only linked contracts, a "Связать с договором…" action (choice dialog) instead of
  switches for all contracts of the property; no repeated second line.
- **R-34 Meter → Readings card:** ＋ in the section header opens the reading dialog for this meter.
- **R-42 Sync page:** status first; the long explanation collapsed ("Как это работает"); one "last sync" line only.
- **R-47** note per register as a small icon in the field row (the note field appears when used); **R-48** "Передать
  онлайн" only once (banner, or the meter card when the meter has its own link); **R-49** `ReturnType="Next"` moves to the
  next value field, history as an icon button.
- **R-50 Bills / Payments filter on the phone:** the collapsed filter line itself carries ‹ month › (one tap to the
  previous month); the expanded filter collapses again after a choice.

### 2. Details
- **C-05** Overview "Этот год": no "2025 за тот же период: 0,00 €" line when there are no data for the previous year (§9).
- **C-08** card outlines ≥ 3 : 1 against the page in both themes.
- **C-12** "Скоро срок" status also in the bill editor (same rule as the Bills list).
- **C-14** sync texts and **C-15** meter history texts per the findings; **C-18** non-breaking space before units (m³,
  kWh) — extend the T-036a formatting helper.
- **C-16** default palette for **new** categories per the finding (never change existing user colours).

### 3. Delete with dependants (from T-036b, §5.1)
Property, contract, supplier and meter keep the confirmation, but with the **list of dependants** instead of the old
"Запись будет скрыта, история сохранится": e.g. "Договор «Газ» будет удалён вместе с: 12 счетов, 3 платежа, 1 тариф.
Это можно отменить сразу после удаления." — and offer Undo (the T-036b recorder) after the delete as well, if the
recorder covers all rows of that delete; otherwise say that it cannot be undone except from a backup. Wording EN + RU in
resx; propose the §5.1 text in the PR.

### 4. Guide (owner 2026-11-06)
- Templates and contracts: explain where custom fields appear — "Относится к: Счёт" → the bill's "Дополнительные поля"
  card; "Строка счёта" → inside every bill line. Add a one-line hint under "Относится к" in the field editor.
- Everything this task changes, EN + RU.

### 5. Optional (only if simple and safe)
Windows: the calendar pop-up of `DatePicker` follows the Windows UI language (English on the owner's PC). Try setting the
native control's `Language` to the app culture in a Windows-only handler mapping. If it needs more than a small mapping,
leave it and say so in the PR.

## Tests
Meter title rule; Overview previous-year line hidden without data; due-soon in the editor; unit formatting with NBSP;
dependants count text with concrete numbers; reading dialog focus order (ViewModel part).

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; UI checklist of CLAUDE.md in the PR. Owner: the
screens above behave as described.

## Manual test steps for the owner (short, in the PR)
Bill editor order; two meters in one room; meter Contracts / Readings cards; Readings on the phone (Next key, note icon);
Sync page; Bills filter ‹ › on the phone; delete a contract (dependants listed); custom field hint and guide.
