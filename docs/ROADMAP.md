# Roadmap

Each phase is split into tasks small enough for one coding session. Task ids are assigned when the task
is written; the list below is the plan and will change.

## Phase 0 — Infrastructure
- T-001 Solution skeleton, central packages, empty MAUI shell with EN/RU resources, CI (Linux + Windows)

## Phase 1 — Domain core (no UI, no DB)
- T-002 Base types: `SyncEntity`, `YearMonth`, `Versioned<T>`, `MoneyMath`, `Currency`, `IClock`
- T-003 Properties, parameters, suppliers, links, contracts (entities + validation)
- T-004 Meters, registers, readings, `ContractMeter` + `ConsumptionCalculator` (rollover, replacement, anomalies)
- T-005 VAT catalog, tariff components, rates, tiers
- T-006 `ChargeCalculator` for all bases (PerUnit, PerArea, PerResident, FixedMonthly, FromBill, NetExport)
- T-007 User categories, payment details (IBAN, purpose template), comments on all entities
- T-008 Bills (incl. one-off, service period, custom fields), payments, allocations, balances

## Phase 2 — Persistence
- T-009 EF Core DbContext, configurations, first migration, `SyncEntity` interceptor (timestamps, soft delete)
- T-010 Repositories / unit of work, seed data (VAT history, built-in categories), App DI + migration at startup

## Phase 3 — First usable app (reference data + readings)
- T-011 App shell, navigation, DI, settings page with language switch, adaptive layout infrastructure (compact/medium/expanded)
- T-012 Properties: list + edit (+ parameters)
- T-013 Suppliers and contracts: list + edit, links opening in browser
- T-014 Meters: list + edit, replacement wizard, verification due
- T-015 Readings entry (phone-first, tablet two-pane), history, warnings, meter photo

## Phase 4 — Tariffs and charges
- T-016 Tariff editor per contract (components, rates, VAT)
- T-017 Monthly charges screen: calculated charge per contract/period

## Phase 5 — Bills, payments, balance
- T-018 Bill entry (total + optional lines + attachment), difference vs calculation
- T-019 Payments, allocation, prepayment, balance
- T-020 Equalized plans and annual settlement view; property tax installments
- T-021 Dashboard: to-do (readings window, due bills, overdue, verification), balances

## Phase 6 — Reports and charts
- T-022 Report queries in Core (by property/service/period, multi-property)
- T-023 Charts (bar, grouped by property), year-over-year, unit cost; export to Excel

## Phase 7 — Templates and import
- T-024 Contract templates, bill templates with schedules (once / monthly / every N months / yearly), drafts
- T-025 Excel import framework (column mapping, preview, dry run)
- T-026 Import profile for the owner's legacy workbook (synthetic fixture in tests)

## Phase 8 — Reminders
- T-027 `INotificationService` for Android and Windows, reminder rules, scheduling

## Phase 9 — Backup and sync
- T-028 Local backup/restore, export/import full data as JSON
- T-029 Google sign-in per platform
- T-030 Sync engine (change logs in appDataFolder, merge, conflict tests)
- T-031 Attachments sync

## Phase 10 — iOS
- T-032 Enable `net10.0-ios`, macOS CI job, platform services for iOS
