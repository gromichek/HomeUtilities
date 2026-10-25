# T-020: Payments — list and editor, allocation to bills, prepayment, contract balance
Phase: 5 · Depends on: T-019 · Size: M–L

## Goal
Besides the one-tap "Paid", the owner needs to record real payments as they happened: one bank transfer that pays
several bills, a payment made before the bill arrived (prepayment), a partial payment, a refund. He wants to see per
contract whether he owes money or has paid in advance.

## Context
- `docs/PAYMENT_TYPES.md` (payment status, undo rules), `docs/DATA_MODEL.md` §6 "Payment", "Contract balance".
- Core: `Payment`, `PaymentAllocation`, `PaymentValidator`, `PaymentAllocator.AutoAllocate` (oldest open bills first),
  `BillStatus.Of`, `ContractBalance.Of` → `Balance(Billed, Paid, Value)`, `PaymentPurpose`.
  ViewModels: `QuickPayService` (T-019), bill editor Payment card, `AmountEditor`, snackbar.
- Menu item **Payments** exists as a placeholder page.

## Scope — do

### 1. Payments screen (menu **Payments**), list + editor (MasterDetail pattern)
- Filters: Property (All + each), Year (‹ year › / all), Contract (All + contracts of the chosen property). Remembered
  per session like the Bills filters.
- Row: date, amount (negative = refund, shown as "−12,00 € refund"), contract title (or property / "several
  contracts"), method, what it pays ("Water 09.2026, Water 10.2026" — up to 2 bills, then "+N"), and a badge
  **Prepayment 12,30 €** when part of it is not allocated. Quick-action payments show a small "quick" marker.
  Totals of the filtered list: paid, of it unallocated (prepayment).
- "＋ New payment".

### 2. Payment editor
1. **General**: Contract (picker: property → contract; or "No contract" → Property picker, for a payment that covers
   bills of several contracts), Paid on* (today), Amount* (`DecimalInput`, negative allowed = refund), Currency (from
   contract, else EUR), Method (Bank transfer default), Reference (with **Fill from contract** = payment purpose of
   the contract via `PaymentPurpose`, using the allocated bill's period/number/amount when there is exactly one),
   Notes.
2. **Bills paid** (allocations): rows "Water · 10.2026 · bill 46,02 € — 46,02 €" (tap → edit amount in a dialog,
   trash removes the allocation); ＋ opens a picker of **open bills** (of the contract; for "No contract" the
   property's; same currency and sign; each with its open amount); the amount defaults to
   min(open amount of the bill, unallocated part of the payment). **Allocate automatically** fills the unallocated
   part with `PaymentAllocator.AutoAllocate`. Footer line: "Allocated 58,32 € · Prepayment 12,30 €" (or "Over-
   allocated by …" as a validation error).
3. Validation: `PaymentValidator` (allocations' sum ≤ amount, same sign, no bill twice, …) mapped to fields;
   an allocation may not exceed the bill's open amount **plus** what this payment already allocated to it.
4. Save keeps the page open; Delete = soft delete with the standard dialog; its allocations go with it (bills become
   open again — say so in the dialog: "Bills it paid become unpaid again.").
5. A quick-action payment opened here is a normal payment: editing its date/amount keeps `CreatedByQuickAction`;
   if it ends up allocated to more than one bill, or its amount differs from its allocation, clear
   `CreatedByQuickAction` (it is no longer "the Paid tap of one bill"). Unit-test this rule.

### 3. Bill editor (T-019) Payment card
- **Add payment** (next to Paid) → payment editor pre-filled: contract, today, amount = open amount, allocation to this
  bill. Payment rows open the payment editor (push; Back returns to the bill, which refreshes its status).
- **Use prepayment 12,30 €** when the contract has unallocated money: allocates from the oldest payment(s) with
  unallocated money to this bill (up to the open amount), snackbar "Prepayment used · Undo". Logic in a service next
  to `QuickPayService` (`PrepaymentService`), unit tested.

### 4. Contract balance
- Contract editor: new card **Balance** (below Bills): Billed · Paid · result "Debt 45,00 €" (red) / "Paid in advance
  12,30 €" (green) / "Settled" — `ContractBalance.Of` today; "Unallocated payments: 12,30 €" line with **Use for open
  bills** (`PaymentAllocator.AutoAllocate` for each payment with unallocated money, oldest first) and a card list of the
  last 6 payments + "All payments" (Payments screen filtered to the contract) + ＋ (new payment for this contract).
- Bills list (T-019) totals unchanged; Payments list shows prepayments as above.

### 5. Sample data, help, guide
- `SampleDataGenerator`: add one bank transfer paying two bills of different contracts of the flat (no contract,
  property set), one prepayment for internet (unallocated, next month's bill not yet entered), one partial payment,
  one refund (negative, allocated to a negative Settlement/Correction bill — create that bill), keep the existing
  quick payments. Extend its tests (validators; every contract balance computed; at least one debt and one
  prepayment).
- `HelpTopic.Payments` + "?" on the Payments screen and payment editor (anchor test).
- User guide EN + RU: new section **Payments** (quick Paid vs a real payment; one payment for several bills;
  prepayment and "Use prepayment"; partial payments; refunds; balance of a contract: what "debt" / "paid in advance"
  mean; deleting a payment makes its bills unpaid). Update the Bills section (Add payment / Use prepayment) and
  Contracts (Balance card). Remove "payments and balances" from "Coming soon".

## Scope — do NOT
- No equalized plans / annual settlement view / tax installments (T-021), no dashboard (T-022), no bank statement import.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner: one payment of 58,32 € allocated to two bills → both Paid ✓; delete it → both Unpaid again.
3. Payment of 70,00 € for a 57,70 € bill → "Prepayment 12,30 €"; next bill of the contract → **Use prepayment** →
   that bill partly paid by 12,30 €; Undo works.
4. Contract balance shows Debt / Paid in advance correctly for the sample data.

## Tests required
- `PaymentEditViewModelTests`: defaults (from contract, from a bill), any open-bill picker filtering (contract /
  property, currency, sign), allocation amount default, auto-allocate, over-allocation error, allocation above the
  bill's open amount refused, refund (negative) flow, Fill from contract (purpose text with one bill, without bill),
  quick flag cleared by rule §2.5, delete message, CanLeaveAsync, save keeps page open.
- `PaymentsViewModelTests`: filters, row texts ("+N"), prepayment badge, totals.
- `PrepaymentServiceTests`: uses oldest unallocated money first, never more than the open amount, several payments,
  Undo, statuses after each step.
- Contract balance card: e.g. billed 3 × 46,02 = 138,06; paid 120,00 → "Debt 18,06 €"; paid 150,00 → "Paid in
  advance 11,94 €"; Use for open bills.
- Data/sample tests as in §5; `InputHardeningTests` for the payment editor and allocation dialog.

## Manual test steps for the owner
1. Settings → Test data → Delete all data → reopen → Fill with sample data.
2. Menu **Payments** → filters; the transfer for two bills shows both; the internet prepayment shows
   "Prepayment …".
3. New payment without contract for the flat: 58,32 € → ＋ two open bills → both Paid ✓ in Bills. Delete the payment →
   both Unpaid.
4. Contract → **Balance** card: debt / paid in advance; **Use for open bills**.
5. Bill → **Add payment** (partial amount) → Partially paid; **Use prepayment** on another bill → snackbar Undo.
6. Refund: negative amount → shown as refund. Nonsense input everywhere → messages only. Phone / wide / dark theme /
   Russian texts; "?" opens Payments.
