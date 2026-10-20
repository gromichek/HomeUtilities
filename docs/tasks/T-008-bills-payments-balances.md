# T-008: Bills, custom fields, payments, allocations, balances
Phase: 1 · Depends on: T-007 · Size: L

## Goal
Model what the user enters every month — supplier bills (incl. one-off expenses and prepaid services), their
optional lines and custom fields — and payments with allocation to bills; derive open amounts, overdue /
prepaid status, contract balances and the difference to the app's own calculation.
Pure C# in Core, unit-tested. No persistence, no UI. (Equalized plans and annual settlement: T-020.)

## Context
- `docs/PAYMENT_TYPES.md` (agreed behaviour — read it fully)
- `docs/DATA_MODEL.md` §6 (Amount fields, Bill, BillLine, CustomFieldDefinition/Value, Payment, balance),
  §3 Contract `BillingTiming`
- Existing: `Contract`, `Category`, `CalculatedCharge`, `MoneyMath`, `YearMonth`, `Currency`, `SyncEntity`,
  `ValidationResult`, `ErrorCodes`, notes length rule from T-007.

## Scope — do
Namespace `HomeUtilities.Core.Billing` (folder `Billing/`), enums with explicit int values.

### 1. Enums
`BillKind { Regular, Equalized, Settlement, Correction, TaxInstallment, OneOff }`,
`BillingTiming { InArrears, InAdvance }`, `PaymentMethod { BankTransfer, Card, Cash, DirectDebit, Other }`,
`CustomFieldKind { Text, Number, Money, Date }`, `CustomFieldTarget { Bill, BillLine }`.
Add `Contract.BillingTiming` (default `InArrears`) and `Contract.CustomFields` (`List<CustomFieldDefinition>`).

### 2. Amounts — `Amounts.Complete`
```csharp
public sealed record AmountSet(decimal? Net, decimal? VatPercent, decimal? Vat, decimal Gross);
public static (AmountSet? Result, ValidationError? Error) Complete(decimal? net, decimal? vatPercent, decimal? gross);
```
- gross + vat% → `MoneyMath.FromGross`; net + vat% → `FromNet`; net + gross → `Vat = gross − net`,
  `VatPercent = Round(Vat / Net × 100, 2)` (null if Net = 0); gross only → `(null, null, null, gross)`;
  all three given → must be consistent with `FromNet(net, vat%)` (gross within ±0.01), else error
  `amount.inconsistent`; net only or vat% only or nothing → error `amount.gross_required`.
- Negative values allowed (refunds, corrections).

### 3. Custom fields
- `CustomFieldDefinition : SyncEntity` — `OwnerId` (contract or bill template), `Name` (≤ 60), `Kind`,
  `Target`, `SortOrder`.
- `sealed record CustomFieldValue(Guid? DefinitionId, string Name, CustomFieldKind Kind, string Value)`.
  - `static CustomFieldValue From(CustomFieldDefinition d, object value)` and typed getters
    `decimal? AsDecimal()`, `DateOnly? AsDate()`.
  - Storage format is invariant: Number/Money → `decimal` invariant (`"12.5"`), Date → `yyyy-MM-dd`.
- `CustomFieldValidator`: definition name required/unique per owner+target (case-insensitive); value parses
  for its kind (`custom_field.invalid_value`); text ≤ 500.

### 4. Bill and BillLine (`SyncEntity`)
Fields exactly per DATA_MODEL §6 (`Bill`: PropertyId, CategoryId, ContractId?, SupplierId?, Period,
ServiceFrom?, ServiceTo?, Kind, Title?, Number?, IssuedOn?, DueOn?, Net?, VatPercent?, Vat?, Gross, Currency,
Lines, CustomFields, ConsumptionOnBill?, CalculatedSnapshot?, TemplateId?, AttachmentIds, Notes;
`BillLine`: BillId, Description, Quantity?, Unit?, UnitPrice?, Net?, VatPercent?, Vat?, Gross, CategoryId?,
CustomFields, Note?, SortOrder).
- `static Bill CreateFor(Contract contract, YearMonth period, IClock clock)` — copies PropertyId,
  CategoryId, SupplierId, Currency; Kind = `Equalized` if contract `BillingMode.Equalized`, `TaxInstallment`
  if `Assessed`, else `Regular`.
- `static Bill CreateOneOff(Guid propertyId, Guid categoryId, string title, YearMonth period, IClock clock)`.
- `static YearMonth DefaultPeriod(Contract contract, DateOnly today)` — `InArrears` → previous month,
  `InAdvance` → next month.

`BillValidator.Validate(Bill bill, Contract? contract)`:
- PropertyId, CategoryId not empty.
- `ContractId == null` ⇔ `Kind == OneOff`; OneOff requires `Title`.
- With contract: `bill.ContractId == contract.Id`, `PropertyId` equals contract's, `Currency` equals contract's.
- `ServiceFrom`/`ServiceTo` both or none, `From ≤ To`; `DueOn ≥ IssuedOn` when both set.
- Amounts: when Net/VatPercent/Vat given, `Amounts.Complete` must accept them (same codes).
- `Gross < 0` only for `Settlement` / `Correction` (`bill.negative_not_allowed`).
- Lines: each line amounts valid like the bill; description required.
- Custom field values valid. Notes/Note length rule.
- `Title`, `Number` ≤ 100.

`BillChecks.Warnings(Bill bill)` (non-blocking, codes in `BillWarningCodes`):
`LinesTotalMismatch` when lines exist and |Σ line Gross − Gross| > 0.01; `NoAttachment` is **not** a warning.

### 5. Comparison with the calculation — `BillComparison`
`Compare(Bill bill, CalculatedCharge? calculated, decimal? meteredQuantity)` →
`record BillComparisonResult(decimal? Difference, decimal? DifferencePercent, decimal? QuantityDifference)`:
- `Difference = Gross − calculated.TotalGross` (null without calculation or when it is incomplete);
  `DifferencePercent = Round(Difference / calculated.TotalGross × 100, 1)`;
- `QuantityDifference = ConsumptionOnBill − meteredQuantity` when both known (water losses).
- FromBill-only calculations (all lines `IsFromBill`) → `Difference` null.

### 6. Service-period spread — `BillSpread.ByMonth(Bill bill)`
→ `IReadOnlyList<(YearMonth Month, decimal Gross)>`: without ServiceFrom/To → `[(Period, Gross)]`; with them →
split by number of days per month, `MoneyMath.Round` each share, rounding remainder added to the **last**
month so the sum equals `Gross` exactly.

### 7. Payments (`SyncEntity`)
- `Payment` — ContractId?, PropertyId?, PaidOn, Amount, Currency, Method, Reference?, Allocations, Notes.
- `PaymentAllocation` — PaymentId, BillId, Amount, Note?.
- `PaymentValidator.Validate(Payment p, IReadOnlyDictionary<Guid, Bill> bills)`: `Amount ≠ 0`; allocation
  amounts non-zero with the same sign as `Amount`; `Σ|allocations| ≤ |Amount|`; allocated bill exists, not
  deleted, same currency; no two allocations to the same bill; Notes length.
- `PaymentAllocator.AutoAllocate(Payment payment, IEnumerable<Bill> candidates, Func<Guid, decimal> openAmountOf)`
  → new allocations for the **unallocated** part: candidates = non-deleted bills with open amount of the
  same sign; if `payment.ContractId` set → only that contract; else if `PropertyId` set → that property;
  order by `DueOn ?? IssuedOn ?? Period.LastDay`, then `Period`; fill oldest first; never over-allocate a bill.

### 8. Derived status — `BillStatus` and `ContractBalance`
- `BillStatus.Of(Bill bill, IEnumerable<Payment> payments, DateOnly today)` → `record BillState(decimal Paid,
  decimal Open, bool IsPaid, bool IsOverdue, bool IsPrepaid)`:
  `Paid` = Σ non-deleted allocations to the bill from non-deleted payments; `Open = Gross − Paid`;
  `IsPaid = Open == 0`; `IsOverdue = Open > 0 && DueOn < today`;
  `IsPrepaid = IsPaid && bill.Period > YearMonth.From(today)`.
- `ContractBalance.Of(Guid contractId, IEnumerable<Bill> bills, IEnumerable<Payment> payments, DateOnly date)`
  → `record Balance(decimal Billed, decimal Paid, decimal Value)`:
  `Billed` = Σ Gross of non-deleted bills of the contract with `IssuedOn ?? Period.FirstDay ≤ date`;
  `Paid` = Σ Amount of payments with `ContractId == contractId` and `PaidOn ≤ date` + Σ allocations to this
  contract's bills from payments **without** `ContractId` (`PaidOn ≤ date`);
  `Value = Paid − Billed` (positive = overpayment / prepayment, negative = debt).

## Scope — do NOT
- No equalized plans / settlement logic (T-020), no bill templates or schedules (T-024), no persistence/UI.

## Acceptance criteria
1. Implemented as specified, XML doc comments on public members.
2. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.

## Tests required (invented ids; arithmetic in comments)
- `AmountsTests`: gross 59.49 + 21% → net 49.17, VAT 10.32; net 10 + 12% → 1.20 / 11.20; net 49.17 + gross
  59.49 → VAT 10.32, 20.99%; gross only; net only → error; inconsistent triple → error; negative −12.10 + 21%.
- `CustomFieldTests`: decimal round-trip under `ru-RU` culture stays `"12.5"`; date `2026-10-20`; invalid number
  rejected; duplicate definition name (case-insensitive) rejected.
- `BillTests`: `CreateFor` copies contract data and picks Kind by billing mode; `DefaultPeriod` on
  2026-10-20 → InArrears 2026-09, InAdvance 2026-11; `CreateOneOff`.
- `BillValidatorTests`: every rule failing + passing (one-off without title, contract mismatch, currency
  mismatch, negative regular bill, half service period, due before issue, bad line).
- `BillChecksTests`: lines 34.32 + 20.93 vs gross 55.25 → no warning; vs 59.49 → `LinesTotalMismatch`.
- `BillComparisonTests`: bill 59.49 vs calculated 55.25 → +4.24, +7.7%; ConsumptionOnBill 21.0 vs metered
  20.114 → +0.886; incomplete calculation → null; FromBill-only → null.
- `BillSpreadTests`: insurance 120.00 for 2026-01-01..2026-12-31 → 12 shares, Jan 10.19 (31/365×120 = 10.19),
  sum exactly 120.00; quarter 2026-10-01..2026-12-31 of 22.95 → 7.73 / 7.48 / 7.74 (remainder in December);
  no service period → whole amount to Period.
- `PaymentValidatorTests`: zero amount, wrong-sign allocation, over-allocation, duplicate bill, unknown bill.
- `PaymentAllocatorTests`: payment 100 to bills 30 (due 09-15), 50 (due 10-15), 40 (due 11-15) → 30, 50, 20;
  partially paid bill uses its open amount; property-level payment covers two contracts of the property;
  contract-level payment ignores other contracts.
- `BillStatusTests`: open/paid/overdue on boundary dates; prepaid internet bill for 2026-11 paid on 2026-10-20 →
  `IsPrepaid`; deleted payment not counted.
- `ContractBalanceTests`: bills 55.25 + 59.49, payments 60 + 60 → +5.26; property-level payment allocated
  to this contract counts only its allocation; bills issued after `date` excluded.

## Manual test steps for the owner
n/a (no UI changes).
