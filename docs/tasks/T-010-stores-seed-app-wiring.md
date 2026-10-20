# T-010: Data stores, seed data, database in the App
Phase: 2 · Depends on: T-009 · Size: L

## Goal
Give the rest of the app a clean way to load and save aggregates (stores behind Core interfaces), seed the
initial reference data (Latvian VAT history, built-in categories), and make the MAUI app create/migrate/seed
its SQLite database at startup — visible to the owner on the start page.

## Context
- `docs/ARCHITECTURE.md` §3 (dependencies), §6 (platform services), §7 (storage)
- T-009 PR #19: `HomeUtilitiesDbContext`, `SyncEntityInterceptor(IClock)` (must be added by the caller),
  `DatabaseMigrator`, soft-delete filter `SoftDelete`, `ClientCascade` on child collections.
- `LatvianVatDefaults.Create()`, `BuiltInCategories.Create()`, `ChargeInput`.

## Scope — do

### 1. Store interfaces in Core (`HomeUtilities.Core.Abstractions`)
One interface per aggregate root; all async with `CancellationToken`:
```csharp
public interface IStore<T> where T : SyncEntity
{
    Task<T?> GetAsync(Guid id, CancellationToken ct = default);          // full aggregate incl. children
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);    // non-deleted, aggregate incl. children
    Task SaveAsync(T aggregate, CancellationToken ct = default);         // insert or update whole graph
    Task DeleteAsync(Guid id, CancellationToken ct = default);           // soft delete (root + children)
}
```
Specialised interfaces add queries (keep them minimal, only what is listed):
- `IPropertyStore : IStore<Property>` — `ListAsync(bool includeArchived)`.
- `ICategoryStore : IStore<Category>`, `ISupplierStore : IStore<Supplier>`, `IVatRateStore : IStore<VatRate>`.
- `IContractStore : IStore<Contract>` — `ListByPropertyAsync(Guid propertyId)`;
  `ListContractMetersAsync(Guid contractId)`; `SaveContractMeterAsync(ContractMeter)`.
- `IMeterStore : IStore<Meter>` — `ListByPropertyAsync(Guid propertyId)` (incl. registers + readings).
- `ITariffStore : IStore<TariffComponent>` — `ListByContractAsync(Guid contractId)` (incl. rates).
- `IBillStore : IStore<Bill>` — `ListByContractAsync(Guid contractId)`, `ListByPropertyAsync(Guid propertyId,
  YearMonth? from, YearMonth? to)`.
- `IPaymentStore : IStore<Payment>` — `ListByContractAsync(Guid contractId)` (payments with that `ContractId`
  **or** with an allocation to one of its bills), `ListByPropertyAsync(Guid propertyId)`.
- `IChargeInputLoader` — `Task<ChargeInput?> LoadAsync(Guid contractId, YearMonth period, CancellationToken)`
  (contract, property, components, contract meters, all property meters, VAT catalog).

### 2. Implementations in Data (`HomeUtilities.Data.Stores`)
- Use `IDbContextFactory<HomeUtilitiesDbContext>`: **a new short-lived context per call** (MAUI-friendly,
  safe from UI threads). Reads use `AsNoTracking()` + `AsSplitQuery()` with the needed `Include`s.
- `SaveAsync` handles **detached graphs** coming from the UI: load the existing aggregate (tracked, including
  deleted children? no — only non-deleted), then `CurrentValues.SetValues` for root and each child matched by
  `Id`; children present in the incoming graph but not in the DB → add; children in the DB but missing from the
  incoming graph → remove (becomes soft delete via the interceptor). Implement once as a reusable helper
  `AggregateMerger` and use it in all stores. New aggregate (id not in DB) → `Add`.
- `DeleteAsync` → loads and removes the root (children follow via `ClientCascade` → soft-deleted).
- Order of returned lists: by `SortOrder` then `Name` where those exist; readings by `Date`; bills by `Period`;
  payments by `PaidOn`.

### 3. Seeding — `DataSeeder.SeedAsync(HomeUtilitiesDbContext db, CancellationToken ct)`
- VAT: for each `LatvianVatDefaults` code — if no row with that code exists (`IgnoreQueryFilters`), insert;
  if it exists but is soft-deleted, **leave it** (the user deleted it on purpose); never overwrite user edits.
- Categories: same rule by `SystemKey`.
- Idempotent: running twice inserts nothing the second time.

### 4. DI extension in Data
`services.AddHomeUtilitiesData(string databasePath)` registers the context factory (SQLite, interceptor with
the `IClock` from DI), all stores, `IChargeInputLoader`, and an `IDatabaseInitializer` whose
`InitializeAsync()` = `DatabaseMigrator.MigrateAsync` + `DataSeeder.SeedAsync`.

### 5. App wiring
- Implement `IAppPaths` (ARCHITECTURE §6) in the App: `DatabasePath = Path.Combine(FileSystem.AppDataDirectory,
  "homeutilities.db")`; define the interface in ViewModels (`string DatabasePath { get; }`).
- `MauiProgram`: `AddHomeUtilitiesData(appPaths.DatabasePath)`.
- Start page: `MainViewModel` gets `IDatabaseInitializer`, `ICategoryStore`, `IVatRateStore`;
  `[RelayCommand] InitializeAsync` (called from `MainPage.OnAppearing` via a command — keep code-behind to
  one line) runs the initializer once and shows a status line:
  - success: localized `DatabaseReady` = "Database ready: {0} categories, {1} VAT rates" /
    "База данных готова: категорий {0}, ставок НДС {1}"
  - failure: localized `DatabaseError` = "Database error: {0}" / "Ошибка базы данных: {0}" (exception message).
  - while running: `DatabaseStarting` = "Opening database…" / "Открываю базу данных…".
  Texts refresh on language change like the others.
- No new pages, no navigation.

## Scope — do NOT
- No CRUD screens (from T-012). No sync. No changes to Core domain types other than the new interfaces.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (Android + Windows builds).
2. Starting the App on Windows and Android creates the database, seeds it, and shows
   "Database ready: 16 categories, 3 VAT rates" (16 = number of `ServiceKind` values — use the real count).
3. Restarting the App does not duplicate seed data.

## Tests required (Data.Tests; ViewModels.Tests)
- Store round-trips via the interfaces for every store: save new → get → equal fields; list order.
- `AggregateMerger`: update a property scalar; add a parameter; remove a parameter → soft-deleted row remains
  with `IsDeleted`; change a reading value inside meter → register → readings; detached graph from a different
  context instance works.
- `DeleteAsync`: root and children soft-deleted; `GetAsync` returns null afterwards; `ListAsync` excludes it.
- `IPaymentStore.ListByContractAsync` returns property-level payments allocated to that contract's bills.
- `IBillStore.ListByPropertyAsync` period range inclusive.
- `IChargeInputLoader`: loads everything needed so that `ChargeCalculator` on the loaded input gives the same
  result as on the in-memory fixture (reuse the water 55.25 € scenario).
- `DataSeeder`: first run inserts 3 VAT rates (with versions) and one category per `ServiceKind`; second run
  inserts nothing; a user-renamed built-in category keeps its name after seeding; a soft-deleted VAT code is
  not re-inserted.
- `MainViewModelTests`: initializer success → `DatabaseReady` text with counts; failure → `DatabaseError` text.

## Manual test steps for the owner
1. Pull, start the App on **Windows Machine**.
2. Expected: start page shows "Opening database…" briefly, then
   "Database ready: 16 categories, 3 VAT rates" (Russian if selected). Switch language → line is translated.
3. Close and start again → same numbers (no duplicates).
4. Repeat on the **Android emulator** (and a tablet emulator if you have one).
5. Optional: the database file is `%LOCALAPPDATA%\Packages\…` or `%LOCALAPPDATA%\HomeUtilities\…` on Windows
   (unpackaged app: see `FileSystem.AppDataDirectory`); deleting it and restarting recreates it.
