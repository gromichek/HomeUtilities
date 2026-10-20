# T-009: EF Core persistence — DbContext, mappings, soft delete, first migration
Phase: 2 · Depends on: T-008 · Size: L

## Goal
Persist the whole Core domain in SQLite via EF Core 10, without changing Core's public API: one
`HomeUtilitiesDbContext`, Fluent API mappings in `HomeUtilities.Data`, automatic sync timestamps and soft delete,
the first checked-in migration, and round-trip tests on real SQLite (in-memory connection).
Repositories, seed data and wiring into the App come in T-010.

## Context
- `docs/ARCHITECTURE.md` §7 (storage), §8 (sync consequences), §4.6
- `docs/DATA_MODEL.md` (all sections) — entity shapes are already implemented in Core
- `CLAUDE.md`: Core must not reference EF Core → **no attributes in Core**, mapping only in Data.

## Scope — do

### 1. Packages (add to `Directory.Packages.props`, reference from Data)
`Microsoft.EntityFrameworkCore.Sqlite` 10.x, `Microsoft.EntityFrameworkCore.Design` 10.x (Data, `PrivateAssets=all`).
Local tool manifest `.config/dotnet-tools.json` with `dotnet-ef` 10.x. New test project
`tests/HomeUtilities.Data.Tests` (xUnit, refs Data + Core), added to `.sln` and `HomeUtilities.Tests.slnf`.

### 2. `HomeUtilitiesDbContext` (namespace `HomeUtilities.Data`)
- Constructor takes `DbContextOptions<HomeUtilitiesDbContext>`; `IClock` is used by the interceptor (§4).
- One configuration class per aggregate in `Data/Configurations/` (`IEntityTypeConfiguration<T>`),
  applied via `ApplyConfigurationsFromAssembly`.
- Table per entity (plural names): `Properties`, `PropertyParameters`, `Categories`, `Suppliers`, `Contracts`,
  `ContractMeters`, `Meters`, `MeterRegisters`, `MeterReadings`, `VatRates`, `VatRateVersions`,
  `TariffComponents`, `TariffRates`, `Bills`, `BillLines`, `Payments`, `PaymentAllocations`.
- Relationships: use the existing FK properties (`PropertyParameter.PropertyId`, `MeterRegister.MeterId`,
  `MeterReading.RegisterId`, `VatRateVersion.VatRateId`, `TariffRate.ComponentId`, `BillLine.BillId`,
  `PaymentAllocation.PaymentId`, …) and the existing `List<>` navigations. FKs to other aggregates
  (`Contract.PropertyId`, `Contract.SupplierId`, `Bill.ContractId`, `PaymentAllocation.BillId`, …) are real
  FKs **without** navigation properties. `DeleteBehavior.Restrict` everywhere (we never hard-delete).

### 3. Column mapping rules
| Type | Mapping |
|---|---|
| `Guid` | default (TEXT) |
| `decimal` | default SQLite TEXT (exact). **No SQL arithmetic/ordering on decimals** — aggregate in memory. |
| `DateOnly` | default (TEXT `yyyy-MM-dd`, orderable) |
| `DateTimeOffset` | value converter → `long` UTC ticks (orderable; required for sync queries) |
| `YearMonth` | value converter → `int` `Year*100+Month` (e.g. 202610; orderable) |
| enums | `int` |
| `PaymentDetails` (Supplier, Contract) | complex/owned type, columns prefixed `Pay_` (nullable as a whole) |
| `Link` lists (Supplier, Contract), `Contract.CustomFields`, `TariffRate.Tiers`, `TariffComponent.RegisterKinds/MeterIds`, `Bill.CustomFields`, `BillLine.CustomFields`, `Bill.AttachmentIds` | **JSON column** on the owner (System.Text.Json, invariant). Items keep their own `Id`/timestamps inside the JSON. |
| `Bill.CalculatedSnapshot` | JSON column; `ChargeLine.Consumption` is **not** persisted (empty list after load) — use a converter with custom serializer options |
| strings | max lengths from validators (`Name` 100, `Notes` 2000, …) via `HasMaxLength` |

JSON-column collections need value comparers so EF detects in-place list changes.

### 4. `SyncEntityInterceptor : SaveChangesInterceptor` (takes `IClock`)
For every tracked `SyncEntity` (also children):
- `Added` → `CreatedAt = UpdatedAt = clock.UtcNow` (keep an already set `CreatedAt`, e.g. from sync later — add
  a flag `PreserveTimestamps` on the context, default false, that skips all stamping);
- `Modified` → `UpdatedAt = clock.UtcNow`;
- `Deleted` → change state to `Modified`, set `IsDeleted = true`, stamp `UpdatedAt` (soft delete, also for
  children removed from a collection — make removed children soft-deleted, not orphan-deleted);
- when a JSON-column item list changes, the owner row is `Modified` → stamped.

### 5. Query filter
Global query filter `!IsDeleted` on every root entity (named filter `SoftDelete` if available in EF 10, so it can
be disabled selectively); `IgnoreQueryFilters()` gives deleted rows (needed for sync). Child collections
(`Include`) must also exclude deleted rows by default.

### 6. Indexes
- `UpdatedAt` on every table (sync).
- Unique: `VatRates.Code`; `Categories.SystemKey` (filtered: not null).
- `MeterReadings (RegisterId, Date)`, `Bills (ContractId, Period)`, `Bills (PropertyId, Period)`,
  `PaymentAllocations (BillId)`, `Contracts (PropertyId)`, `Meters (PropertyId)`, `TariffRates (ComponentId, ValidFrom)`.

### 7. Migration and design time
- `DesignTimeDbContextFactory` (SQLite file `design.db`, never committed) for `dotnet ef`.
- First migration `InitialCreate` in `Data/Migrations/`, generated with the tool (not hand-written).
- `static class DatabaseMigrator { Task MigrateAsync(HomeUtilitiesDbContext db, CancellationToken ct) }`
  — applies migrations; also sets SQLite pragmas `journal_mode=WAL`, `foreign_keys=ON`.
- Document in `README.md` (section "Database migrations") the command to add a migration.

## Scope — do NOT
- No repositories, unit of work, seed data, App/DI wiring (T-010). No sync logic. No UI.
- Do not change Core types except: if EF absolutely needs a parameterless constructor or a settable
  property, add the minimum (`private`/`protected` ctor) and list it under "Decisions made".

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green (incl. new Data.Tests), zero warnings; CI green.
2. `dotnet ef migrations has-pending-model-changes` reports none (also as a test:
   `db.Database.HasPendingModelChanges() == false`).
3. Core has still no package references.

## Tests required (`HomeUtilities.Data.Tests`, SQLite `DataSource=:memory:` with an open connection per test,
migrations applied via `DatabaseMigrator`; invented data)
- **Round-trip** (save in one context, read in a new one) for: Property + parameters; Supplier with links and
  payment details; Contract with links, custom field definitions, payment details, reading window, timing;
  Meter with 2 registers and readings incl. `Digits`; ContractMeter; VatRate with versions; TariffComponent with
  register kinds, meter ids, 2 rates, one with tiers; Bill with lines, custom fields, attachment ids, service
  period and a calculated snapshot; Payment with allocations; Category built-in and user.
- **Precision**: decimals `6.5597`, `0.702804`, `413.816`, `-12.10` and `12345678.123456` come back exactly.
- **YearMonth / DateOnly / DateTimeOffset** round-trip; `Bills` ordered by `Period` in SQL is chronological.
- **Interceptor**: with a fake clock — insert stamps both; update stamps only `UpdatedAt`; `Remove(entity)` →
  row still exists with `IsDeleted = true`; removing a reading from `register.Readings` → soft-deleted;
  changing a link URL inside the JSON list stamps the contract; `PreserveTimestamps` keeps given values.
- **Filters**: deleted property not returned; returned with `IgnoreQueryFilters()`; deleted reading not in
  `Include(r => r.Readings)`.
- **Constraints**: duplicate VAT code → `DbUpdateException`; FK to a non-existent property → `DbUpdateException`.
- **Model**: no pending model changes.

## Manual test steps for the owner
n/a (no UI changes). The App is not affected.
