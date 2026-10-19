# T-002: Domain base types
Phase: 1 · Depends on: T-001 · Size: S

## Goal
Add the foundational value types and base classes to `HomeUtilities.Core` that every later entity and
calculation builds on. Pure C#, no new packages, fully unit-tested.

## Context
- `docs/DATA_MODEL.md` §1 (base types)
- `docs/ARCHITECTURE.md` §4 (principles 1, 6, 7)

## Scope — do

Folder layout in Core: `Common/` for the types below. Namespace `HomeUtilities.Core.Common`.
Move nothing that exists (`IClock`, `SystemClock` stay where they are).

### 1. `SyncEntity` (abstract class)
- `Guid Id { get; set; }` — initialised to `Guid.CreateVersion7()` in the constructor.
- `DateTimeOffset CreatedAt`, `DateTimeOffset UpdatedAt` (UTC), `bool IsDeleted`.
- Public setters are fine (EF Core will need them), but provide helper methods:
  - `void Touch(IClock clock)` — sets `UpdatedAt = clock.UtcNow`; sets `CreatedAt` too if it is `default`.
  - `void MarkDeleted(IClock clock)` — `IsDeleted = true` + `Touch`.
- Throw `ArgumentException` from `Touch` if `clock.UtcNow.Offset != TimeSpan.Zero` (enforces UTC).

### 2. `Currency` enum and conversion
- `enum Currency { EUR = 0, LVL = 1 }`.
- `static class CurrencyConverter`:
  - `const decimal LvlPerEur = 0.702804m;`
  - `decimal ToEur(decimal amount, Currency from)` — EUR unchanged; LVL → `amount / LvlPerEur`,
    **not rounded** (callers round).

### 3. `MoneyMath` (static)
- `decimal Round(decimal value)` → 2 decimals, `MidpointRounding.AwayFromZero`.
- `decimal RoundQuantity(decimal value, int decimals = 3)` → same rule, for m³/kWh.
- `(decimal Net, decimal Vat, decimal Gross) FromNet(decimal net, decimal vatPercent)`:
  `Vat = Round(net * vatPercent / 100)`, `Gross = Round(net) + Vat`, `Net = Round(net)`.
- `(decimal Net, decimal Vat, decimal Gross) FromGross(decimal gross, decimal vatPercent)`:
  `Gross = Round(gross)`, `Net = Round(gross / (1 + vatPercent / 100))`, `Vat = Gross - Net`
  (so Net + Vat == Gross exactly).
- Negative values must work symmetrically (refunds, solar export): `Round(-0.005m) == -0.01m`.

### 4. `YearMonth` (readonly record struct)
- `int Year`, `int Month` (validated: Year 1900–2200, Month 1–12, else `ArgumentOutOfRangeException`).
- `static YearMonth From(DateOnly date)`, `DateOnly FirstDay`, `DateOnly LastDay`,
  `YearMonth AddMonths(int n)`, `int MonthsUntil(YearMonth other)` (other − this, may be negative).
- Implements `IComparable<YearMonth>` and comparison operators `< <= > >=`.
- `ToString()` → `"yyyy-MM"`; `static YearMonth Parse(string)` / `TryParse(string, out YearMonth)` for
  exactly that format (invariant, no culture).
- `bool Contains(DateOnly date)`.

### 5. Time-versioned values
- `interface IVersioned { DateOnly ValidFrom { get; } }`.
- `static class Versioned`:
  - `T? At<T>(IEnumerable<T> items, DateOnly date) where T : class, IVersioned` — item with the greatest
    `ValidFrom <= date`; `null` if none.
  - `bool TryAt<T>(IEnumerable<T> items, DateOnly date, out T? value)` — same, bool result.
  - `IReadOnlyList<string> Validate<T>(IEnumerable<T> items) where T : IVersioned` — returns error codes
    (see §6) when two items share the same `ValidFrom`. Empty list = valid.
- Ignore items whose type is a `SyncEntity` with `IsDeleted == true` in `At`/`TryAt`/`Validate`.

### 6. Validation result
Domain validation everywhere will return error **codes**, not messages (UI localizes them later).
- `sealed record ValidationError(string Code, string? Field = null, object?[]? Args = null)`.
- `sealed class ValidationResult` with `IReadOnlyList<ValidationError> Errors`, `bool IsValid`,
  `static ValidationResult Ok`, `static ValidationResult Fail(params ValidationError[] errors)`,
  `ValidationResult Merge(ValidationResult other)`.
- Error codes are string constants in `static class ErrorCodes` (e.g.
  `public const string DuplicateValidFrom = "versioned.duplicate_valid_from";`).
  `Versioned.Validate` returns `ValidationResult` (adjust §5 signature accordingly).

### 7. Fake clock for tests
- `tests/HomeUtilities.Core.Tests/Fakes/FakeClock.cs`: settable `UtcNow`, `Today` derived from it
  (or settable separately). Use it in the tests below.

## Scope — do NOT
- No domain entities (Property, Meter, …) — those are T-003/T-004.
- No EF Core attributes or references.
- No `Money` struct (we pass `decimal` + `Currency` separately — keep it simple).

## Acceptance criteria
1. All types above exist in `HomeUtilities.Core.Common` with XML doc comments on public members.
2. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings, CI green.
3. Core still has **no package references**.

## Tests required (Core.Tests, one test class per type)
- `MoneyMathTests` (use `[Theory]` with inline data):
  - `Round(2.345m) = 2.35`, `Round(2.344m) = 2.34`, `Round(-2.345m) = -2.35`, `Round(0.005m) = 0.01`
  - `FromNet(7.05m, 21) = (7.05, 1.48, 8.53)` — "5 m3 × 1.41 = 7.05; VAT 21% = 1.48; total 8.53"
  - `FromNet(10m, 12) = (10.00, 1.20, 11.20)`
  - `FromGross(1.56m, 21) = (1.29, 0.27, 1.56)` — the legacy "1.29 + 21% = 1.56" water tariff
  - `FromGross(-12.10m, 21) = (-10.00, -2.10, -12.10)`
  - invariant: for 50 generated values `Net + Vat == Gross` for both methods
- `CurrencyConverterTests`: `ToEur(0.702804m, LVL) = 1m`; `ToEur(9.32m, LVL)` rounded = `13.26`;
  EUR unchanged.
- `YearMonthTests`: From/FirstDay/LastDay (incl. February 2024 = 29 days, Feb 2026 = 28),
  AddMonths across year boundary (2026-11 + 3 = 2027-02, 2026-01 − 1 = 2025-12), MonthsUntil,
  comparisons, Parse/ToString round-trip, TryParse rejects `"2026-13"`, `"2026-1"`, `"abc"`,
  constructor rejects month 0 and 13.
- `VersionedTests`: rates valid from 2009-01-01 (21), 2011-01-01 (22), 2012-07-01 (21):
  At(2010-06-15)=21 (first), At(2011-01-01)=22 (boundary inclusive), At(2012-06-30)=22, At(2026-10-19)=21 (third),
  At(2008-12-31)=null; unordered input gives same results; deleted item ignored; duplicate ValidFrom →
  `DuplicateValidFrom`.
- `SyncEntityTests`: new entity has non-empty version-7 Guid (`Id.Version == 7`); two entities have
  different ids; Touch sets CreatedAt once and UpdatedAt every time; MarkDeleted; non-UTC clock throws.
- `ValidationResultTests`: Ok is valid; Fail is invalid; Merge concatenates errors.

## Manual test steps for the owner
n/a (no UI changes).
