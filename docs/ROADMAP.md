# Roadmap

Each phase is split into tasks small enough for one coding session. Task ids are assigned when the task
is written; the list below is the plan and will change.

## Phase 0 — Infrastructure
- T-001 Solution skeleton, central packages, empty MAUI shell with EN/RU resources, CI (Linux + Windows)

## Phase 1 — Domain core (no UI, no DB)
- T-002 Base types: `SyncEntity`, `YearMonth`, `Versioned<T>`, `MoneyMath`, `Currency`, `IClock`
- T-003 Properties, parameters, suppliers, links, contracts (entities + validation)
- T-004 Meters, registers, readings + `ConsumptionCalculator` (rollover, replacement, anomalies)
- T-005 VAT catalog, tariff components, rates, tiers
- T-006 `ChargeCalculator` for all bases (PerUnit, PerArea, PerResident, FixedMonthly, FromBill, NetExport)
- T-007 Bills, payments, allocations, balances, equalized plans

## Phase 2 — Persistence
- T-008 EF Core DbContext, configurations, first migration, `SyncEntity` interceptor (timestamps, soft delete)
- T-009 Repositories + seed data (VAT history, service kinds), Data tests on SQLite in-memory

## Phase 3 — First usable app (reference data + readings)
- T-010 App shell, navigation, DI, settings page with language switch
- T-011 Properties: list + edit (+ parameters)
- T-012 Suppliers and contracts: list + edit, links opening in browser
- T-013 Meters: list + edit, replacement wizard, verification due
- T-014 Readings entry (phone-first), history, warnings, meter photo

## Phase 4 — Tariffs and charges
- T-015 Tariff editor per contract (components, rates, VAT)
- T-016 Monthly charges screen: calculated charge per contract/period

## Phase 5 — Bills, payments, balance
- T-017 Bill entry (total + optional lines + attachment), difference vs calculation
- T-018 Payments, allocation, prepayment, balance
- T-019 Equalized plans and annual settlement view; property tax installments
- T-020 Dashboard: to-do (readings window, due bills, overdue, verification), balances

## Phase 6 — Reports and charts
- T-021 Report queries in Core (by property/service/period, multi-property)
- T-022 Charts (bar, grouped by property), year-over-year, unit cost; export to Excel

## Phase 7 — Templates and import
- T-023 Contract templates, recurring fixed bills
- T-024 Excel import framework (column mapping, preview, dry run)
- T-025 Import profile for the owner's legacy workbook (synthetic fixture in tests)

## Phase 8 — Reminders
- T-026 `INotificationService` for Android and Windows, reminder rules, scheduling

## Phase 9 — Backup and sync
- T-027 Local backup/restore, export/import full data as JSON
- T-028 Google sign-in per platform
- T-029 Sync engine (change logs in appDataFolder, merge, conflict tests)
- T-030 Attachments sync

## Phase 10 — iOS
- T-031 Enable `net10.0-ios`, macOS CI job, platform services for iOS
