# T-019: Bills — entry, comparison with the calculation, payment status, "Paid" with Undo; in-app snackbar
Phase: 5 · Depends on: T-018 · Size: L

## Goal
The owner enters the bills he receives (or any one-off expense), sees at once how far each bill is from the app's
calculation, and marks a bill as paid with one tap — with Undo, because a wrong tap must never cost data.
A small in-app snackbar is needed for "Undo" (and finally gives Windows the "Saved"/"Copied" feedback).

## Context
- `docs/PAYMENT_TYPES.md` (all — especially "Payment status of a bill" and undo rules), `docs/DATA_MODEL.md` §6
  (Amount fields, Bill, BillLine, custom fields, Payment), §5 "Charge calculation" (snapshot), `docs/UI_GUIDELINES.md`.
- Core already has: `Bill`, `BillLine`, `BillKind`, `BillValidator`, `BillChecks` (non-blocking warnings),
  `BillComparison.Compare`, `BillStatus.Of` → `BillState`, `Amounts.Complete` (any two of Net / VAT % / Gross),
  `Payment`, `PaymentAllocation`, `PaymentValidator`, `CustomFieldDefinition/Value/Validator`, `ChargeCalculator`,
  `ChargePeriodDefaults`. Stores: `IBillStore`, `IPaymentStore`, attachments (`AttachmentOwnerKind.Bill`, T-016a).
- Menu item **Bills** exists as a placeholder page.

## Scope — do

### 1. In-app snackbar (both platforms) — do this first
- `ISnackbarService` (ViewModels): `Task<bool> ShowAsync(string message, string? actionText = null,
  TimeSpan? duration = null)` → true if the action button was tapped before it disappeared. Default 3 s, with an
  action 6 s. A new message replaces the current one (the replaced call returns false).
- App: a non-modal bar at the bottom of the window (above the FAB area, full width on phones, max ~560 px centred on
  wide windows), theme colours, accessible. No system notifications, no CommunityToolkit Toast/Snackbar (they crash the
  unpackaged Windows app — UI_GUIDELINES §6a). Suggested: a page `ControlTemplate` applied to `PageBase` with a
  `ContentPresenter` and a snackbar host bound to a singleton; any other robust approach is fine.
- Replace `IToastService` usages ("Saved", "Copied", readings saved…) by the snackbar on both platforms and remove the
  Windows no-op. Update UI_GUIDELINES §6a pitfall text → propose the new wording in the PR (lead edits the guideline).

### 2. Bills screen (menu **Bills**), list + editor (MasterDetail pattern like Properties)
- Filters at the top: Property (All + each), Year (‹ year ›), Status (All / Open / Overdue / Paid). Remember the
  filters per session.
- Row: period (MM.yyyy), contract title or one-off title, supplier, category dot, **Total**, status badge
  (Unpaid grey · Partially paid yellow with the open amount · Overdue red · Paid ✓ green · Prepaid ✓), and the
  difference to the calculation when there is one ("+2,15 € (+3,4 %)", red above +5 % / green below −5 %, muted
  otherwise). Totals of the filtered list: billed, paid, open.
- Row action: `IconPaid` (Material `task_alt` / Segoe `E73E`) **Paid** on open bills — the quick action (§4).
- "＋ New bill" (FAB on phones / button on wide): first choice **For a contract** (property → contract pickers) or
  **One-off expense**.

### 3. Bill editor
Cards:
1. **General**: property; contract (read-only once saved; one-off: none) ; **Period** (month picker; default for a
   contract = `ChargePeriodDefaults` for its property, for `InAdvance` contracts the next month; one-off: current
   month); Kind (default Regular; one-off → OneOff); Title (required for one-off); Number; Issued on (today);
   Due on (from contract `PaymentDueDay` in the month after Period, editable); Service from / to (optional,
   "for a bill that covers several months"); Category (default from contract; required for one-off); Supplier
   (default from contract; optional for one-off).
2. **Amounts**: Amount excl. VAT / VAT % / VAT / Total — the user enters any two of Net / VAT % / Total, the rest is
   derived with `Amounts.Complete` (Total alone allowed). VAT % default from the contract's first component VAT or
   empty. Currency from the contract.
3. **Comparison** (contracts with tariff components only): calculated total for the period (`ChargeCalculator`),
   difference € and %, "Quantity on the bill" field (`ConsumptionOnBill`) vs metered quantity → "Water losses /
   difference: 0,350 m³"; link **Show charges** (Charges screen for that property/month, contract expanded). On save
   the current calculation is stored as `CalculatedSnapshot`; if the current calculation later differs from the
   snapshot, show "The calculation changed since the bill was entered (snapshot 45,10 € → now 46,02 €)" with
   **Update snapshot**. Comparison uses the snapshot.
4. **Lines** (optional, collapsed by default): child rows with dialog: Description*, Quantity, Unit, Unit price,
   amounts (any two), Category (optional override), Note; line-level custom fields. Sum check from `BillChecks`
   as a warning (not an error).
5. **Custom fields**: the contract's definitions with `AppliesTo = Bill` (values typed by kind; Money/Number via
   `DecimalInput`, Date via date picker).
6. **Attachment**: PDF or photo of the bill (reuse T-016a: camera/gallery on Android, file on Windows; PDF opens
   with the system viewer via `Launcher`).
7. **Payment**: status badge, paid / open amounts, payments allocated to the bill (date, amount, method, "quick"
   marker); buttons **Paid** (if open) and per payment **Remove payment** (§4). Detailed payments come in T-020 — no
   payment editor here.
8. **Notes**.
Validation: `BillValidator` + `CustomFieldValidator` mapped to fields. Save stays in the editor (guidelines §4).
Delete → standard dialog; deleting a bill also soft-deletes allocations of quick-action payments to it (and the
quick payment itself if it covered only this bill); other payments keep their money as prepayment — say so in the
dialog message when such payments exist.

### 4. "Paid" quick action, Undo, Remove payment (PAYMENT_TYPES "Cancel payment")
- New `Payment.CreatedByQuickAction` (bool, default false) + EF migration (generated with `dotnet ef`).
- Paid: one payment `{ ContractId, PropertyId, PaidOn = today, Amount = open amount, Currency, Method = BankTransfer,
  Reference = payment purpose of the contract (PaymentPurpose with Period/Amount/BillNumber), CreatedByQuickAction }`
  + one allocation to the bill, saved in one transaction. Then snackbar **"Bill marked as paid" · Undo** (6 s):
  Undo soft-deletes that payment and its allocation. Logic in a Core/ViewModels service (`QuickPayService`), unit tested.
- **Remove payment** (bill editor, per payment): quick payment
  covering only this bill → soft-delete payment; payment covering several bills → remove only this bill's
  allocation, message "The rest stays on the contract as prepayment." Dialog title "Remove payment of {amount}
  from {date}?", buttons **Remove payment** / **Cancel** (RU: «Отменить оплату» / «Отмена»).

### 5. Links from other screens
- **Charges** (T-018) card: when a bill exists for that contract and month → "Bill: 46,02 € · +0,92 € · Paid ✓" (tap
  opens the bill); otherwise **Enter bill** → new bill pre-filled with contract, period and the calculated total as a
  suggestion (Total field placeholder, not a value).
- **Contract editor**: card **Bills** with the last 6 bills (period, total, status) + "All bills" (Bills screen
  filtered to the property, contract highlighted) + ＋ (new bill for this contract).

### 6. Sample data, help, guide
- `SampleDataGenerator`: bills for the flat's contracts for the last 12 months (totals near the calculation, a few
  with differences, water with `ConsumptionOnBill`), most paid via quick action, one partially paid (manual payment),
  one overdue, one prepaid internet bill for next month, one one-off expense ("Boiler repair"), one bill with lines
  and a custom field value. Extend its tests (validators, statuses: paid/partial/overdue/prepaid all present).
- `HelpTopic.Bills` + "?" on the Bills screen and bill editor (anchor test).
- User guide EN + RU: new section **Bills** (when a bill belongs to which month — service month, not issue month;
  in advance / in arrears; any two amounts; comparison and water losses; snapshot; lines and custom fields;
  attachment; status badges; Paid + Undo + Remove payment; one-off expenses). Update "Charges" (bill link) and
  "Contracts" (Bills card); diagram: add Bill (contract → bills) and Payment (payment → bill). Remove "bills with the
  Paid quick action" from "Coming soon".

## Scope — do NOT
- No payment editor / payments screen / allocation of one payment to several bills / prepayment balance (T-020).
- No bill templates or drafts (T-025), no equalized settlement (T-021), no reminders.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner (sample data and his own contract): enter a water bill → difference to the calculation and water losses
   shown; Paid → green ✓ + snackbar with Undo; Undo → back to Unpaid; Paid again → Remove payment → Unpaid.
3. Snackbar visible on Windows and Android ("Saved", "Copied", "Bill marked as paid · Undo"); nothing crashes.
4. One-off expense without contract can be entered and appears in the list.

## Tests required
- Snackbar service logic (replacement returns false, action result, durations) with a fake host.
- `QuickPayServiceTests`: amount = open amount (partial bill → rest), reference text, allocation, Undo soft-deletes,
  Remove payment for single-bill quick payment vs multi-bill payment, nothing hard-deleted, status after each step
  (`BillStatus.Of`), double tap on Paid creates one payment.
- `BillEditViewModelTests`: defaults per contract (period for in-arrears / in-advance, due date, category, currency,
  VAT %), any-two amounts (e.g. Net 10,00 + VAT 21 % → VAT 2,10, Total 12,10; Total only), one-off requires title +
  category, comparison and losses (e.g. calculated 45,10, bill 46,02 → +0,92 € (+2,0 %); bill 12,500 m³ vs metered
  12,150 → 0,350 m³), snapshot stored on save and "changed" detection, lines sum warning, custom fields, delete with
  quick payments, CanLeaveAsync, save keeps page open.
- `BillsViewModelTests`: filters, totals, badges, difference colouring thresholds, Paid from the list.
- Data: migration applies; sample data tests; `InputHardeningTests` for the bill editor and line dialog.

## Manual test steps for the owner
1. Settings → Test data → Delete all data → reopen → Fill with sample data.
2. Menu **Bills** → year filter, status filter; badges green/yellow/red; totals.
3. An open bill → **Paid** → snackbar "Bill marked as paid · Undo" → Undo → Unpaid again. Paid again → open the bill
   → Payment card → **Remove payment** → Unpaid.
4. New bill for the water contract of the current/previous month → enter Total only → comparison shows the difference;
   enter "Quantity on the bill" → losses. Attach a photo/PDF. Save → stays in the editor.
5. One-off expense: title, category, property, total → appears in the list.
6. Charges → a contract with a bill shows the bill line; one without → **Enter bill** pre-fills contract and month.
7. "Saved" / "Copied" now visible on Windows. Phone / tablet / wide; dark theme; Russian texts fit; "?" opens Bills.
