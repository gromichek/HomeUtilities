# T-022: Overview — what to do now, money at a glance
Phase: 5 · Depends on: T-021 · Size: M–L

## Goal
The **Overview** menu item (today a placeholder: "Today is …") becomes the start screen the owner opens first: what
he has to do now (submit readings, pay bills, enter expected bills, verify meters, pay tax installments) and where his
money stands (debts, prepayments, expected equalized settlements, this year's costs). The to-do logic is reused later
by reminders (T-028), so it lives in Core.

## Context
- Existing logic to reuse: reading windows (`ReadingWindow`, Readings screen texts "open now / opens in N days"),
  `ChargePeriodDefaults`, `BillDefaults.DueDate`, `BillStatus.Of`, `ContractBalance.Of`, `EqualizedSettlement`,
  `Meter.GetVerificationDue()`, `QuickPayService` (Paid + Undo), snackbar, stores, `ChargeInputLoader`.
- `docs/UI_GUIDELINES.md` (all), adaptive layout (§4.9 ARCHITECTURE: compact / medium / expanded).

## Scope — do

### 1. Core: `TodoBuilder.Build(data, today)` → ordered list of `TodoItem { Kind, Severity (Overdue / Today / Soon /
Info), Date, PropertyId, ContractId?, MeterId?, BillId?, Amount?, Args }`
- **ReadingsWindow**: a contract's reading window is open today (Today) or opens within 3 days (Soon) and the
  property has no reading of that contract's meters inside this window yet; one item per property + window (several
  contracts with the same window → one item listing them). Closed today without readings → Overdue until the window
  end + 3 days.
- **BillDue**: open bills with `DueOn` ≤ today + 7 (Soon), = today (Today), < today (Overdue), amount = open amount.
- **BillMissing**: contract (not archived, active, BillingMode Actual/Fixed/Equalized) with `BillDay` set: the bill for
  its expected period (in arrears: previous month; in advance: next month) is not entered and today ≥ that period's
  bill day (+ 0 days) → Info ("Bill for 10.2026 from City Water not entered yet").
- **TaxInstallmentDue**: covered by BillDue (TaxInstallment bills) — just a different text.
- **VerificationDue**: meter (active) with verification due within 60 days (Soon) or past (Overdue).
- **SettlementExpected** (Info): equalized contracts with |expected settlement| ≥ 1,00 €.
- Deterministic order: Overdue → Today → Soon → Info, then by date, then property name. Tests with concrete dates for
  each kind and boundary (window 25–28: on 22 Oct → Soon "opens in 3 days", 25 Oct → Today, 28 Oct with readings →
  nothing, 30 Oct without → Overdue, 1 Nov → gone; due date tomorrow / today / yesterday; bill day 10, period 09.2026,
  on 9 Oct → nothing, 10 Oct → item).

### 2. Overview screen
- Header: date + property filter (All + each; remembered).
- **To do** card: rows with icon by kind, coloured by severity, text, date/amount; each row has its action:
  readings → open Readings (property, today); bill due → open the bill + inline **Paid** (quick action + snackbar Undo);
  bill missing → **Enter bill** (pre-filled contract + period); verification → open the meter; settlement → open the
  settlement page. Empty state: "Nothing to do — all readings submitted and bills paid." (localized).
- **Money** card: per property — open bills total, overdue total, prepayments (unallocated), sum of contract balances
  ("Debt 45,00 € / Paid in advance 12,30 €"); equalized contracts' expected settlements; tap a line → Bills / Payments
  / contract filtered accordingly.
- **This year** card: per property total billed this year (gross), and the same months of last year for comparison
  ("2026: 1 245,30 € · 2025 same period: 1 180,10 € (+5,5 %)"); tap → Bills filtered to the year. (Charts come in T-024.)
- Layout: one column on phones; on medium/expanded To do on the left (wider), Money + This year on the right.
- Refresh on appearing; pull-to-refresh on Android (RefreshView) if simple.
- Performance: load data once per refresh through the stores; no N+1 per contract if avoidable (a loader in Data like
  `ChargeInputLoader`, e.g. `OverviewDataLoader`, tested).
- Make Overview the start page (already the first flyout item — check it opens first after start and after restore).

### 3. Sample data, help, guide
- Sample data already produces most situations; make sure at least one of each to-do kind appears relative to the
  generation date (window open or soon, overdue bill, bill due in a few days, missing bill, verification overdue,
  equalized settlement). Test: `TodoBuilder` over the sample data yields every kind.
- `HelpTopic.Overview` + "?"; user guide EN + RU: section **Overview** first among the screens (what each to-do means,
  when it appears and disappears, the money card); update "How things fit together" step list if needed; remove
  "overview of what to do" from "Coming soon".

## Scope — do NOT
- No notifications/reminders (T-028), no charts (T-024), no settings for the thresholds (constants in Core; list them
  in the guide).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Sample data: Overview shows each to-do kind with working actions; Paid from Overview + Undo works.
3. Money and This year numbers equal the Bills/Payments screens for the same filters (VM tests with numbers).

## Manual test steps for the owner
1. Settings → Test data → Delete all data → reopen → Fill with sample data → app opens on **Overview**.
2. Each to-do row → its action opens the right screen; Back returns to Overview, the list refreshes (e.g. after
   entering readings the readings item disappears).
3. An overdue bill → **Paid** → disappears; Undo → back.
4. Money card: debt/prepayment per property matches the contracts' Balance cards.
5. Property filter; phone / tablet / wide; dark theme; Russian texts fit; "?" opens Overview help.
