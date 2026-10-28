# T-025: Bill templates with schedules and draft bills
Phase: 7 · Depends on: T-024 · Size: L

## Goal
Recurring payments without a meter (internet/TV, insurance, chimney cleaning, a garage fee, …) and repeated one-off
purchases should not be typed from scratch. A **bill template** describes how such a bill looks and when it comes; the
app creates **draft bills** for each due period, the owner confirms (or adjusts) them with one tap. `Once` templates
appear in "New bill from template".
Contract templates (§8 ContractTemplate) are **not** part of this task (later, T-025b, if wanted).

## Context
- `docs/DATA_MODEL.md` §8 BillTemplate + Schedule (read all of it), §6 Bill (`TemplateId` exists), amount fields;
  `docs/PAYMENT_TYPES.md` (Internet/TV, Other recurring, One-off rows).
- Bill editor (T-019) with AmountEditor, lines, custom fields; Bills list; Overview `TodoBuilder` (T-022); reports
  (T-023) and balances (T-020) use bills.

## Decisions (lead)
- **Draft** = `Bill.IsDraft` (bool, migration). Drafts are shown in Bills (badge "Draft", muted) and on Overview (new
  to-do kind `DraftsToConfirm`, one item per property: "3 draft bills to confirm"), but are **excluded** from bill
  status/overdue, balances, Paid quick action, prepayment use, reports, charges comparison and Overview money until
  confirmed. Confirm = `IsDraft = false` (Save in the editor or **Confirm** on the row/in the editor).
- Generation is idempotent: the template keeps `LastGeneratedPeriod` (YearMonth?); `BillTemplateScheduler.DuePeriods
  (template, today)` returns the periods to create (in arrears: up to the previous month; in advance: up to the next
  month), never twice (also checked against existing bills with that `TemplateId` + period, including deleted ones —
  a deleted draft is not recreated). Runs when Overview or Bills load (cheap) — no background service yet (T-028).
- Draft content: period, property, category, contract/supplier, title, amounts (default any-two, may be empty →
  draft with Gross 0 shown as "amount to enter"; Gross 0 cannot be confirmed), lines (copied), custom field
  definitions → values empty, `IssuedOn` = the schedule's `DayOfMonth` in the issue month, `DueOn` = issue date +
  `DueDayOffset`, `TemplateId`.

## Scope — do
1. **Core**: `BillTemplate : SyncEntity` exactly as §8 (+ `LastGeneratedPeriod`), `Schedule` value (Kind Once /
   Monthly / EveryNMonths(N) / Yearly, StartMonth, EndMonth?, DayOfMonth?), validator (N 2–24, DayOfMonth 1–31,
   EndMonth ≥ StartMonth, Once has no period fields, name required, amounts via Amounts rules), `BillTemplateScheduler`
   (due periods; issue/due dates; clamps day 31 → month end), `DraftFactory.Create(template, period)`. Unit tests with
   concrete dates: monthly from 01.2026 on 15.10.2026 in arrears → 01…09; in advance → … 11; every 3 months from
   02.2026 → 02, 05, 08, 11; yearly from 03.2025 → 03.2025, 03.2026; end month; last generated period respected; day 31
   in February → 28/29.
2. **Data**: entity configuration (JSON for lines/custom fields like Bill), store, migration (`BillTemplates`,
   `Bills.IsDraft`), backup round trip; every existing bill query that feeds status/balance/reports/overview excludes
   drafts (go through all stores/loaders — list them in the PR — with tests).
3. **Templates screen**: Settings → **Bill templates** (list + editor, MasterDetail pattern): Name*, Property*,
   Category*, Contract (optional; filtered by property; pre-fills category/supplier/timing), Supplier (optional),
   Title, amounts (any two), Lines (child dialog like the bill's), Custom fields (definitions, like the contract
   card), Schedule (Kind picker; fields by kind), Timing, Due after N days, Remind N days before (stored for T-028),
   Active, Notes. Preview line: "Next drafts: 11.2026 (issued 05.11.2026, due 19.11.2026), 12.2026 …" (3 periods).
   Also entry points: contract editor Bills card → "＋ Template from this contract"; bill editor ⋯ → "Save as template".
4. **Bills**: drafts in the list (badge, filter Status gets "Drafts"), row action **Confirm** (only if Gross > 0,
   otherwise opens the editor); editor shows a banner "Draft from template «Internet + TV» — check and confirm" with
   **Confirm** (validates + saves + `IsDraft = false`) — Save on a draft keeps it a draft; "＋ New bill" gets a third
   choice **From template…** listing all active templates (Once and scheduled), creating a bill (not a draft) pre-filled
   from it for the period the user picks.
5. **Overview**: to-do `DraftsToConfirm` (Info) → opens Bills filtered to drafts of that property.
6. **Sample data**: templates for internet + TV (monthly, in advance, fixed amount — replace part of the generated
   internet bills by drafts of the last 2 months), home insurance (yearly in March), chimney cleaning (yearly, empty
   amount), "Water meter replacement" (Once). Tests: drafts appear and are excluded from balances/reports.
7. **Help/guide** EN + RU: new section **Bill templates and drafts** (when to use a template vs a contract with a
   tariff, schedules with examples, in advance/in arrears periods, drafts and confirming, what drafts do not affect,
   Once templates); Bills section (drafts, From template); Overview (new to-do). Remove "bill templates" from
   "Coming soon".

## Scope — do NOT
- No contract templates, no reminders/notifications (T-028), no automatic confirmation.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Sample data: Overview shows drafts to confirm; confirming a draft makes it a normal bill (status, balance, reports).
3. A draft never affects balances, statuses, Overview money or reports (tests per consumer).
4. Re-opening the app does not create duplicates; deleting a draft does not bring it back.

## Manual test steps for the owner
1. Fill with sample data → Overview: "draft bills to confirm" → Bills (drafts) → **Confirm** one → it becomes a normal
   bill; **Paid** now possible.
2. Settings → **Bill templates** → new "Insurance": yearly in March, 120,00, due after 14 days → preview shows next
   periods. Save → for a past March a draft appears in Bills.
3. Bill editor ⋯ → Save as template; contract → Template from this contract.
4. New bill → From template → Once template "Water meter replacement" → normal bill pre-filled.
5. Delete a draft → reopen the app → it does not return. Phone / wide / dark theme / Russian texts; "?".
