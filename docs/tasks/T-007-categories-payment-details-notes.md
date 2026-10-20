# T-007: User categories, payment details, comments on all entities
Phase: 1 · Depends on: T-006 · Size: M

## Goal
Owner's requests of 2026-10-20:
1. Payment categories the user creates himself (e.g. Kindergarten, Insurance), used by reports instead of the
   fixed `ServiceKind` list.
2. Bank payment details (recipient, IBAN, purpose) on suppliers/contracts with a purpose-text template, so
   paying in the internet bank is copy-paste.
3. A free-text comment on every editable entity.

Pure C# in Core, unit-tested. No persistence, no UI.

## Context
- `docs/DATA_MODEL.md` §1 "Comments on everything", §3 `Category`, `PaymentDetails`, Contract/Supplier fields,
  §5 TariffComponent/TariffRate `Notes`, `CategoryId`
- `docs/ARCHITECTURE.md` decision log 2026-10-20

## Scope — do

### 1. Categories (`HomeUtilities.Core.Domain`)
- `Category : SyncEntity` — fields per §3.
  - `bool IsBuiltIn => SystemKey is not null`.
- `static class BuiltInCategories`:
  - `IReadOnlyList<Category> Create()` → one new category per `ServiceKind` value, `SystemKey` set,
    `Name = null`, distinct default colours (fixed palette in code, deterministic), `SortOrder` = enum order.
  - `Guid? FindFor(ServiceKind kind, IEnumerable<Category> categories)` → id of the non-deleted built-in
    category with that key.
- `CategoryValidator`: user category (`SystemKey` null) requires `Name` (not blank, ≤ 100); built-in may have
  null name; `Color` must match `^#[0-9A-Fa-f]{6}$`; `Notes` ≤ 2000.
- `Contract.CategoryId` (Guid). `ContractValidator`: not empty (`contract.category_required`).
- `TariffComponent.CategoryId` (Guid?).
- `ChargeLine` gets `Guid CategoryId` = component's `CategoryId ?? Contract.CategoryId`
  (update `ChargeCalculator` + tests accordingly).

### 2. Payment details (`HomeUtilities.Core.Payments`)
- `sealed record PaymentDetails(string? RecipientName, string? RecipientRegistrationNumber, string? Iban,
  string? Bic, string? BankName, string? PurposeTemplate)` with `static PaymentDetails Empty`.
- `Supplier.PaymentDetails`, `Contract.PaymentDetails` (both `PaymentDetails?`).
- `static PaymentDetails Effective(Contract contract, Supplier supplier)` — per field: contract value if not
  null/blank, else supplier value.
- `static class Iban`:
  - `string Normalize(string input)` — remove spaces, upper-case.
  - `bool IsValid(string input)` — ISO 13616: length 15–34, country letters, mod-97 = 1.
  - `string Format(string iban)` — groups of 4 separated by spaces.
- `static class PaymentPurpose`:
  `string Render(string template, PaymentPurposeContext ctx)` where
  `record PaymentPurposeContext(string? CustomerNumber, string? ContractNumber, string? BillNumber,
  YearMonth? Period, decimal? Amount, string? PropertyName)`.
  Placeholders per DATA_MODEL §3; `{Period:<fmt>}` formats `Period.FirstDay` with invariant culture;
  missing values render as empty string; unknown placeholders and unmatched braces are left as typed.
- `PaymentDetailsValidator`: IBAN valid if present (`payment.iban_invalid`); BIC null or 8/11 chars
  `^[A-Z]{6}[A-Z0-9]{2}([A-Z0-9]{3})?$`; all strings ≤ 140 (bank purpose limit). Called from Supplier and
  Contract validators with field prefix `PaymentDetails.`.

### 3. Comments
- Add `Notes` (string?) to `TariffComponent` and `TariffRate`; add `Notes` to `VatRate`.
- All validators of entities with `Notes`/`Note`: max 2000 chars (`common.notes_too_long`, one shared code).
  Covered: Property, PropertyParameter, Supplier, Contract, Meter, MeterReading, TariffComponent (+ its
  rates), VatRate, Category.
- Bill/Payment comments come with those entities in T-008 — nothing to do here.

## Scope — do NOT
- No persistence, no UI, no clipboard code (UI later).
- Do not rename existing `Note`/`Notes` properties.

## Acceptance criteria
1. As specified, XML doc comments on public members; existing tests updated where `CategoryId` became required.
2. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.

## Tests required
- `BuiltInCategoriesTests`: one per `ServiceKind`, unique colours, `FindFor` works, deleted ignored.
- `CategoryValidatorTests`: user category without name fails; built-in without name OK; bad colour.
- `IbanTests` (use well-known **public example** IBANs, not real accounts): `LV80BANK0000435195001` valid,
  `GB82 WEST 1234 5698 7654 32` valid after normalize, changed check digits invalid, too short invalid;
  `Format("LV80BANK0000435195001") = "LV80 BANK 0000 4351 9500 1"`.
- `PaymentPurposeTests`: `"Abon. Nr. {CustomerNumber}, {Period:MM.yyyy}"` with `12345`, 2026-10 →
  `"Abon. Nr. 12345, 10.2026"`; `{Period}` default format; `{Amount}` 59.49 → `"59.49"` regardless of current
  culture (run under `ru-RU` and `lv-LV`); missing value → empty; `{Unknown}` kept; `"{"` kept.
- `PaymentDetailsTests`: effective merge per field (contract IBAN overrides, supplier recipient kept;
  blank contract value does not override).
- `PaymentDetailsValidatorTests`: invalid IBAN, bad BIC, 141-char purpose.
- Notes length: one test per validator listed in §3 (2000 OK, 2001 fails).
- `ChargeCalculatorTests`: line category = component category when set, else contract category.

## Manual test steps for the owner
n/a (no UI changes).
