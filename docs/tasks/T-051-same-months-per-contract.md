# T-051: Overview — "the same months" per contract; category card subtitle "с начала года"
Phase: 9 · Depends on: T-050 (#319) · Size: S · Owner 2026-11-12

## Why
T-049 ends both years at the **last month of this year with any bill**. Contracts billed in the service month or in advance
(house maintenance, internet) already have a bill for the current month while most others arrive next month: one early
November bill moves the end to November, and last year is then counted with its full November (invented example: this year
Jan–Oct 2 600 € + one November bill 35 € vs last year Jan–Nov 2 830 €).

## Scope — do
1. **Per contract** (and per "no contract" group of one-off bills, see 3): this year = its bills from January to **its own
   last month with a counted bill this year** (capped at the current month); last year = the same contract's bills from
   January to that same month. The year line (`OverviewMoney` → `YearComparison`) sums these per property + currency; the
   change % follows. A contract without bills this year contributes nothing to either year (it may have ended — say in the
   guide).
2. **Category card** (`OverviewCharts.SpendingByCategory`): this year's slices = the bills counted by rule 1 (i.e. up to each
   contract's own last month — in practice all bills of this year up to the current month). Subtitle becomes
   **«с начала года, всего {сумма}»** / EN "since the start of the year, total {amount}" (resx; remove the month-range text if
   unused). `ThroughMonth` may stay for tests or be removed — say which.
3. **One-off bills without a contract** form one group with the T-049 rule (their own last month).
4. "Этот год" bars unchanged.
5. Guide EN + RU, Overview: "the same months for each contract: from January to the month of its last bill this year; last
   year the same months of that contract"; category card «с начала года».

## Tests
- Contract A (arrears) Jan–Oct this year, Jan–Dec last year; contract B (service month) Jan–Nov this year, Jan–Dec last
  year → this year A Jan–Oct + B Jan–Nov, last year A Jan–Oct + B Jan–Nov; % from those numbers.
- Contract without bills this year → in neither sum. One-off bills group.
- Drafts ignored; property filter; category subtitle text EN/RU.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; guide EN + RU.

## Manual test steps for the owner (short)
Enter the November bill of «Обслуживание дома» → Overview: «2025 за тот же период» grows only by last November's house
maintenance bill; the category card says «с начала года, всего …».
