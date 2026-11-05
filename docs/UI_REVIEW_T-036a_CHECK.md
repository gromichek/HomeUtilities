# Check of T-036a (UI review part 1) — findings

Checked: `main` at b1876f9 (T-036a merged as #239) against `docs/tasks/T-036a-ui-review-bugs-theme-formatting.md`,
`docs/UI_GUIDELINES.md` (revision 1) and `docs/UI_REVIEW_2026-11-05.md` (R-NN). Code review only: CI (linux + windows)
is green on b1876f9; the tests could not be run in the review sandbox (NuGet blocked by its proxy); nothing was run on a
device. Findings are numbered **C-01…C-18**.

**Severity:** **Bug** — wrong or hidden information, a rule of the task not met · **Flaw** — inconvenient or untidy ·
**Wish** — improvement. The proposed fix follows each finding.

## Confirmed done
`ChooseAsync` → own `SingleChoiceDialogView`, current value marked, index returned, same-name test (R-04, R-06) ·
toolbar colours 6.9 : 1 in both themes (R-01) · Material Icons on both platforms; all 56 glyphs exist in the bundled
`MaterialIconsOutlined-Regular.otf`; `IconPaid` ≠ `IconSave` (R-17) · month text from the ViewModel in all 6 places
(R-02) · `MonthFormat` + `MonthShort_1…12` (R-11) · non-breaking space before the currency (R-10) · Charges `—` / `≈`,
bound labels, "no readings for …" warning, "Рассчитано" (R-35…R-38) · sync details localized from
`SyncDetailKind` (R-43) · empty texts "Нажмите ＋, чтобы добавить" (R-46) · 88 dp list footers on all 7 list pages
(R-19) · `ListCard` background (R-20) · "Загрузка…" after 300 ms (R-52) · snackbar 56 dp (§5) · red delete icons
(R-12) · hot-water hint only for hot water (R-33) · zero recalculation line hidden (R-18) · ≈ footnote
only with ≈ cells (R-41) · user guide EN + RU updated.

## Bugs

**C-01 · R-05 only half done — icon buttons are 40 dp wide on Android · Bug**
`Styles.xaml` `IconButton` (lines 134–138): the height got `MinimumHeightRequest = TouchSize` (48), but
`WidthRequest="40"` and `MinimumWidthRequest="40"` stayed → 40 × 48: row delete icons, ‹ ›, ＋ in section headers,
✓ on Overview.
*Fix:* `WidthRequest` and `MinimumWidthRequest` = `{StaticResource TouchSize}`.

**C-02 · R-39 made worse — report group header clipped vertically · Bug**
The group cell got `MaxLines: 2`, but the group row has a fixed height: `ReportTableView.GroupHeight = 30`
(row definition at line 76). Two bold lines do not fit, so instead of "Моя кварт…" half of a second line is shown.
The column header row (`HeaderHeight = 64`, `MaxLines: 3`) is also too low at a larger Android font size.
*Fix:* `GridLength.Auto` for both header rows (or heights from the line count: ~44 / ~80 dp).

**C-03 · "Загрузка…" can stay forever after a failed first load · Bug**
`ViewModelBase.RunLoadAsync`: when the first load throws (not a cancellation) in under 300 ms, `finally` sets
`ShowLoading = false` (line 131); then the delayed `ShowLoadingHintAsync` finds the token not cancelled and
`IsLoaded == false` and sets `ShowLoading = true` (line 93). The page shows "Загрузка…" under the error dialog until
the next load. Overview, Bills, Payments, Charges.
*Fix:* stop the hint task when the load ends — a separate `CancellationTokenSource` for the hint, cancelled in
`finally`, or a "load finished" flag checked before setting `ShowLoading`. Unit test: load throws after 10 ms →
`ShowLoading` false after 400 ms.

**C-04 · Android status bar on dialog pages — verify on the device · Bug (probable)**
`AppShell.ApplyStatusBar` (line 59) sets `LightContent` (white status icons) app-wide in the light theme. Phone dialogs
are modal `EditorDialogPage`s without the toolbar and with a light background, so time and battery are likely
white on white while any choice dialog or editor dialog is open. Also, with target SDK 36 Android 15+ enforces
edge-to-edge, where `StatusBar.SetColor` may have no effect.
*Check:* phone (Android 16), light theme, open any choice dialog. *Fix (with R-29 in T-036b):* the dialog page sets
the status bar style on appearing and restores it on disappearing, or gets the toolbar-coloured title bar of §3.4.

**C-05 · Overview "Этот год": "2025 за тот же период: 0,00 €" without data · Bug (rule §9)**
When there are no bills in the previous year, `OverviewYearLine` / `OverviewYearLineNoChange` still print
`Money(c.LastYear)` = 0,00 € (`OverviewViewModel` ~line 572). §9: zero only when the value really is zero.
*Fix:* no previous-year data → "—" or a line without the comparison.

## Flaws and wishes

**C-06 · Every choice now takes two taps (rule problem, §3a) · Flaw**
Before T-036a a choice was one tap (action sheet); now it is "tap the row, tap OK" for property, contract, category,
supplier, period… This follows §3a ("Cancel + OK") — the rule, proposed in the review, is the cause.
*Proposal for the guideline:* in a single-choice dialog a row **without** extra inputs selects and closes at once;
OK is shown only when an option has extra inputs ("Custom…" with dates). Cancel / Back stay.

**C-07 · "＋ Новый поставщик…" is an ordinary radio row · Flaw**
`ContractEditViewModel.ChooseSupplierAsync` appends it as the last option text, so the user selects it and presses OK.
§3a: last row, visually separated, `IconAdd` instead of a radio mark, acts at once.
*Fix:* an "action row" option in `SingleChoiceDialogViewModel` (or a separate button under the list).

**C-08 · Card outlines still below 3 : 1 · Flaw**
T-036a §2 asked for contrast ≥ 3 : 1 (R-51). Field outlines are fine (`FieldOutline*`: 3.2 / 3.5 : 1), card outlines
are not: `OutlineLight` #B8BFCA on white 1.85 : 1, `OutlineDark` #4A505A on #2B2F35 1.66 : 1. In the light theme the card
(#FFFFFF) and the page (#F6F7F9) are almost the same, so the outline is the only boundary.
*Fix:* `OutlineLight` ≈ #959CA8 (≈ 3 : 1) or a darker page background; dark: `OutlineDark` ≈ #6A717C.

**C-09 · The month field gives no hint that it can be tapped · Flaw**
The invisible `DatePicker` lies over a bold label ("Ноябрь 2026"); the old underline and calendar icon are gone.
*Fix:* `IconExpand` or a calendar icon next to the text, or the bordered choice-field look (T-036b).

**C-10 · Full dates still in the system format; mixed date formats · Flaw (§9)**
Full-date `DatePicker`s ("Выставлен", "Оплатить до", reading date…) show the Windows date format. In code, Charges
warnings use the UI culture (`ToString("d", CurrentCulture)`, `ChargeCardBuilder` lines 372 and 425 → "3/15/2023" in the
English UI) while `BillText.Date` always gives "15.03.2023".
*Fix:* one date formatter (in `BillText` / `MonthFormat`), and the date-picker pattern of R-02 for full dates.

**C-11 · About 30 action buttons are still natural width on the phone · Flaw (§3.3)**
Only the two buttons named in the task got `Fill`. Still `HorizontalOptions="Start"` (32 places), e.g. "Все
показания", "Подтвердить", "Показать начисления", "Распределить автоматически", buttons in the contract editor and the
import wizard.
*Fix:* include them in T-036b (one pass over `HorizontalOptions="Start"` on Secondary/Primary buttons; keep the
full-row "Удалить …" / "Сохранить как шаблон" exception).

**C-12 · "Due soon" exists only in the Bills list · Flaw**
`BillBadgeKind.DueSoon` is computed only in the list (it needs today's date); the bill editor's Payment card shows
"Не оплачен" for the same bill. *Fix:* pass today's date to the editor's badge as well.

**C-13 · User guide ru.md line 418: old button name · Flaw**
"Вместо **Оплачено** у него кнопка **Подтвердить**" → "Вместо **Отметить оплаченным** …" (EN is correct).

**C-14 · Sync texts · Flaw**
- Disconnect dialog title is "Отключить" (`SyncDisconnect`, used as title and button) — make the title a question:
  "Отключить аккаунт Google?".
- Details show only `HH:mm` (`SyncDetailText.Line`) — lines from several days cannot be told apart; add the date for
  lines not from today.
- "Конфликт при объединении данных." says neither what happened nor what to do.

**C-15 · Meter history texts not aligned · Flaw**
`MeterHistoryReplacedBy` "Заменён счётчиком {0} {1:d}" vs the new `MeterHistoryReplaced` "Замена: снят счётчик № {0} ·
{1:d}". *Fix:* "Замена: установлен счётчик № {0} · {1:d}".

**C-16 · New default category palette · Wish**
#FDD835 (yellow) as a 14 dp dot on a white card is ~1.4 : 1 — practically invisible; #FFB300 and #FB8C00 are close to the
amber of "due soon". *Fix:* darker tones (e.g. #C49000 instead of #FDD835, #8D6E63 instead of #FFB300), checked ≥ 3 : 1 on the card.

**C-17 · Dark theme toolbar · Wish**
The toolbar is now light blue #7FB0E0 in the dark theme — very bright at night. Usual dark-theme practice: a dark
toolbar (≈ #25282D) with light icons; the accent stays for buttons and selection. Needs the owner's opinion.

**C-18 · Units with a normal space · Wish**
"1,267 м³", "54,7 м²", "1 мес." (`ChargeCardBuilder` lines 233–241, `TariffDescriber`) may wrap between number and unit.
*Fix:* non-breaking space, as for money.

## Suggested handling
- **Now (small, before the owner's test of T-036a):** C-01, C-02, C-03, C-13 — one-line or few-line fixes.
- **With T-036b:** C-04 (dialog frame), C-06 + C-07 (choice dialog; update §3a first), C-09, C-10, C-11.
- **With T-036c or later:** C-05, C-08, C-12, C-14, C-15, C-16, C-18; C-17 after the owner decides.
