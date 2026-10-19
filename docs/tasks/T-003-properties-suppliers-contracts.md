# T-003: Properties, suppliers, links, contracts
Phase: 1 · Depends on: T-002 · Size: M

## Goal
Add the reference-data entities of the domain — properties with time-versioned parameters, suppliers,
web links and contracts — with domain validation, to `HomeUtilities.Core`. Pure C#, no persistence, no UI.

## Context
- `docs/DATA_MODEL.md` §2 (Properties), §3 (Suppliers and contracts) — **except `ContractMeter`**
  (it needs meters; it comes in T-004).
- Base types from T-002: `SyncEntity`, `IVersioned`, `Versioned`, `ValidationResult`, `ErrorCodes`,
  `Currency`, `IClock`.

## Scope — do

Namespace / folder: `HomeUtilities.Core.Domain` (`src/HomeUtilities.Core/Domain/`).
One file per type. Enums in `Domain/Enums/` (same namespace).

### 1. Enums
- `PropertyKind { House, Apartment, Cottage, Garage, Land, Other }`
- `PropertyParameterKind { TotalArea, HeatedArea, DeclaredResidents, LandArea, CadastralValueBuilding, CadastralValueLand, Custom }`
- `LinkKind { Website, CustomerPortal, SubmitReadings, Tariffs, PayOnline, Other }`
- `ServiceKind` — exactly the list in DATA_MODEL §3.
- `BillingMode { Actual, Equalized, Fixed, Assessed }`
- Give every enum member an explicit integer value (they will be persisted). Never reorder later.

### 2. Entities (all derive from `SyncEntity`; mutable properties with public setters)
- `Property` — fields per §2. Collection `List<PropertyParameter> Parameters`.
  Method `decimal? GetParameter(PropertyParameterKind kind, DateOnly date)` — uses `Versioned.At`
  over parameters of that kind (custom parameters additionally matched by `Name`; add overload
  `GetParameter(string customName, DateOnly date)`).
- `PropertyParameter : SyncEntity, IVersioned` — `PropertyId`, `Kind`, `Name` (string?, required only for
  `Custom`), `ValidFrom`, `Value` (decimal), `Note`.
- `Supplier` — fields per §3, `List<Link> Links`.
- `Link : SyncEntity` — `OwnerId` (Guid; supplier or contract id), `Kind`, `Title?`, `Url`, `SortOrder`.
  Method `Uri? TryGetUri()` → absolute `http`/`https` URI or null.
- `Contract` — fields per §3 (incl. `PortalLogin`; **no password field**), `List<Link> Links`.
  - `bool IsActiveOn(DateOnly date)` — `ValidFrom <= date && (ValidTo is null || date <= ValidTo)` and not deleted.
  - `IReadOnlyList<Link> GetEffectiveLinks(Supplier supplier)` — contract links first, then supplier
    links whose `Kind` is not already present among the contract links (contract overrides per kind;
    `Other` links are never deduplicated). Deleted links excluded. Ordered by `SortOrder` inside each group.

### 3. Validation
Each entity gets a static validator class (`PropertyValidator`, `SupplierValidator`, `LinkValidator`,
`ContractValidator`) with `ValidationResult Validate(TEntity entity)`. Rules (add codes to `ErrorCodes`,
format `entity.rule`, e.g. `property.name_required`):
- Property: `Name` not blank, max 100 chars; parameters valid (each by `PropertyParameterValidator`);
  no duplicate `ValidFrom` per (`Kind`, `Name`) — reuse `Versioned.Validate` per group.
- PropertyParameter: `Value >= 0`; `DeclaredResidents` must be a whole number; `Custom` requires `Name`.
- Supplier: `Name` not blank, max 100; all links valid.
- Link: `Url` must parse as absolute `http`/`https` URI (`link.url_invalid`); `Title` max 100.
- Contract: `PropertyId` and `SupplierId` not empty; `Title` not blank, max 100;
  `ValidTo >= ValidFrom` if set; day fields (`ReadingDayFrom`, `ReadingDayTo`, `BillDay`, `PaymentDueDay`)
  in 1–31; `ReadingDayFrom` and `ReadingDayTo` both set or both null; all links valid.
  A reading window that wraps the month end (e.g. 26 → 3) is **allowed**.
- `Field` in `ValidationError` = property name (`nameof`), for nested items prefix with index:
  `Parameters[2].Value`, `Links[0].Url`.

### 4. Reading window helper
`Contract.GetReadingWindow(YearMonth period)` → `(DateOnly From, DateOnly To)?`:
- null if days not set.
- Normal window (From ≤ To): both dates in `period`; days beyond month length are clamped to the last day.
- Wrapping window (From > To): from day `From` of `period` to day `To` of the next month.
This is needed later for reminders and for picking readings for a period.

## Scope — do NOT
- No `ContractMeter`, meters, tariffs, bills.
- No EF Core attributes, no persistence, no UI.
- No password storage of any kind (decision 2026-10-19).

## Acceptance criteria
1. Entities, enums, validators and helpers exist as described, with XML doc comments on public members.
2. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
3. Core still has no package references.

## Tests required (Core.Tests, `Domain/` folder)
Use invented data only (e.g. "Test apartment", `https://example.com/portal`).
- `PropertyTests`: residents 3 from 2020-01-01, 2 from 2024-06-01 → `GetParameter(DeclaredResidents, 2024-05-31) = 3`,
  `(2024-06-01) = 2`, `(2019-12-31) = null`; area unaffected by residents; custom parameter by name; deleted
  parameter ignored.
- `PropertyValidatorTests`: blank name; name 101 chars; negative area; residents 2.5; custom without name;
  duplicate ValidFrom for same kind → error, same ValidFrom for different kinds → OK; field paths correct.
- `LinkTests`/`LinkValidatorTests`: `https://example.com` OK; `ftp://x`, `example.com`, `""`, `javascript:alert(1)` invalid.
- `ContractTests`:
  - `IsActiveOn` boundaries (ValidFrom inclusive, ValidTo inclusive, open-ended, deleted → false).
  - `GetEffectiveLinks`: supplier {Website, CustomerPortal, Other}, contract {CustomerPortal, Other} →
    contract.CustomerPortal, contract.Other, supplier.Website, supplier.Other (4 links).
  - `GetReadingWindow`: days 20–26, period 2026-10 → 2026-10-20..2026-10-26; days 25–31 in 2026-02 →
    2026-02-25..2026-02-28; wrapping 26–3 in 2026-12 → 2026-12-26..2027-01-03; unset → null.
- `ContractValidatorTests`: each rule above has at least one failing and one passing case.

## Manual test steps for the owner
n/a (no UI changes).
