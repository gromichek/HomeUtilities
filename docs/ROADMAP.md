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
- T-012a Apply UI guidelines (platform icons, button hierarchy, standard dialogs, Back), rework Properties
- T-013 Suppliers (links open in browser, payment details with copy) and categories screens
- T-014 Contracts: list per property + edit (supplier, category, timing, reading window, custom fields, links, payment details override)
- T-014a Robustness: no crashes on bad input, global error handler + error log, stay in editor after Save, Android layout fixes
- T-015 Meters: list + edit, replacement wizard, verification due
- T-016 Readings entry (phone-first, tablet two-pane), history, warnings, submit-readings link, mark submitted
- T-016a Attachments infrastructure (entity, file storage, migration) + meter/reading photos (camera on Android, file on Windows) + submit links on meter cards

## Phase 4 — Tariffs and charges
- T-017 Tariff editor per contract (components, rates, VAT)
- T-017a Navigation guard: menu always opens section root; unsaved-changes dialog on every exit (menu, back, window close)
- T-017c Backup to a file / restore (share sheet / save dialog), sample data generator for testing (Debug only)
- T-017d Hotfix: two-pane screens keep the selected item when coming back via the menu
- T-017b In-app help: the user guide (docs/user-guide) shown in the app, "?" on screens opens the matching section
- T-018 Monthly charges screen: calculated charge per contract/period

## Phase 5 — Bills, payments, balance
- T-019 **In-app snackbar/toast control** (both platforms, also replaces the Windows no-op toast; needed for "Undo"), bill entry (total + optional lines + attachment), difference vs calculation, status badges, "Paid" quick action with Undo / Cancel payment (+ `Payment.CreatedByQuickAction` + migration)
- T-020 Payments, allocation, prepayment, balance
- T-021 Equalized plans and annual settlement view; property tax installments
- T-022 Dashboard: to-do (readings window, due bills, overdue, verification), balances

## Phase 6 — Reports and charts
- T-023 Report queries in Core (by property/service/period, multi-property)
- T-024 Charts (bar, grouped by property), year-over-year, unit cost; export to Excel

## Phase 7 — Templates and import
- T-025 Contract templates, bill templates with schedules (once / monthly / every N months / yearly), drafts
- T-026 Excel import framework (column mapping, preview, dry run)
- T-027 Import profile for the owner's legacy workbook (synthetic fixture in tests); `***` → payment = bill total

## Phase 8 — Reminders
- T-028 `INotificationService` for Android and Windows, reminder rules, scheduling

## Phase 9 — Backup and sync
- T-029 Automatic local backups, export/import full data as JSON (manual backup file exists since T-017c)
- T-030 Google sign-in per platform
- T-031 Sync engine (change logs in appDataFolder, merge, conflict tests)
- T-032 Attachments sync

## Phase 10 — iOS
- T-033 Enable `net10.0-ios`, macOS CI job, platform services for iOS
