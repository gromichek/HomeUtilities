# T-049: Overview — compare the same months: up to the last month that already has bills
Phase: 9 · Depends on: T-048 (#313) · Size: S · Owner 2026-11-11

## Why
The "Этот год" line (`OverviewMoney` → `YearComparison`) and the subtitle of "Расходы за {год} по категориям"
(`OverviewCharts.SpendingByCategory`, `ThroughMonth`) count **January – the current month**. Bills of a month arrive in the
next month, so in November this year has bills only up to October while last year is counted January – November: one month
more. Owner's numbers (invented here): this year Jan–Oct 2 600 €, last year Jan–Nov 2 830 € → "−8 %", while Jan–Oct of
last year was 2 550 € → "+2 %".

## Scope — do
1. **Comparison end month** = the last month of this year with at least one counted bill (same bill filter as today:
   non-draft, the Overview property filter, primary currency), capped at the current month. No bill this year → as today
   (card hidden / empty text). Last year is counted January – that same month.
2. The same end month for the category card subtitle («январь – октябрь, всего …») and its totals; the "Этот год" bars stay
   as they are (12 months, future empty).
3. Texts unchanged otherwise ("2025 за тот же период …"); the change % follows. If the end month is earlier than the current
   month, nothing extra is shown (no new text) — optional: the subtitle of the year line may name the months
   («январь – октябрь») if it fits; say what you did.
4. Guide EN + RU, Overview: "the same months: from January to the last month that already has bills".

## Tests
Today 11 Nov, bills this year Jan–Oct, last year Jan–Dec → this year Jan–Oct vs last year Jan–Oct, % from those; a bill
for November entered → both through November; drafts do not count; property filter; categories subtitle «январь – октябрь».

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; guide EN + RU.

## Manual test steps for the owner (short)
Overview → "Этот год": last year's figure equals January – October of last year; categories card says «январь – октябрь».
