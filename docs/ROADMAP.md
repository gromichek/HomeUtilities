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
- T-020a "Quantity from the bill" basis (heating distributed by the supplier), VAT on the total
- T-021 Equalized plans and annual settlement view; property tax installments
- T-022 Dashboard: to-do (readings window, due bills, overdue, verification), balances

## Phase 6 — Reports and charts
- T-023 Report queries in Core (by property/service/period, multi-property)
- T-023a Reports usability: drop-down filters, grouped multi-property table, Android spinner fix
- T-023b Reports polish: choice dialogs per UI_GUIDELINES §3a, report picker, wheel scrolling, Android spinner, narrower menu
- T-024 Charts (bar, grouped by property), year-over-year, unit cost; export to Excel

## Phase 7 — Templates and import
- T-025 Bill templates with schedules (once / monthly / every N months / yearly), draft bills
- ~~T-025b Contract templates~~ — dropped (owner, 2026-11-05: no real use)
- T-026 Excel import framework (column mapping, preview, dry run)
- T-027 Import for the owner's workbook: reading dates and period rules, meter positions, automatic replacements, fallback totals
- T-027a Export "like my workbook" (one sheet per contract from the import profile; round trip)

## Phase 8 — Reminders
- T-028 `INotificationService` for Android and Windows, reminder rules, scheduling

## Phase 8a — Device polish
- T-028c Fixes from real-data testing (performance of long lists, selection, reminders delivery, help links, import window)
- T-028a Pass on a real phone and tablet: text wrapping, touch targets, layouts per width, performance with real data (owner collects findings first)
- T-028b Memory and battery optimisation: profile startup, memory (images, lists, report data), database queries; Android battery usage with reminders on (owner request 2026-10-29)

## Phase 9 — Backup and sync
- T-029 Automatic local backups, export/import full data as JSON (manual backup file exists since T-017c)
- T-030 Google sign-in per platform
- T-031 Sync engine (change logs, merge, conflict tests) behind `ISyncStorage`; Google Drive adapter
- T-032 Sync polish (pause/resume, restore and sync, network, messages, cloud files) and attachment files sync
- T-032a (optional, postponed by the owner 2026-11-05) More storage providers. Notes: OneDrive needs an Entra tenant
  (Azure account) to register the app — owner declined; Dropbox = App Console, app folder, PKCE, Development status is
  enough for personal use (≤ 50 users without review) — possible later. Google Drive stays the only provider.
- T-034 Release identity: own permanent Android signing key (used on every PC and later in CI; its SHA-1 registered as the
  Android OAuth client), real app icon + splash, notification icon = white house from the app icon (owner requests
  2026-11-02). Owner keeps the keystore and passwords outside the repo; migration from the debug key needs one reinstall
  (backup → uninstall → install → restore).

- T-035 Release builds made locally by `scripts/publish-release.ps1`: date-based version (display `yyyy.M.d`, build =
  minutes since 2026-01-01), signed release APK, self-contained Windows build + Inno Setup installer (per user, no admin,
  data kept); no auto-update, files copied by hand (owner, 2026-11-05).

- T-036a/b/c UI review 2026-11-05 (`docs/UI_REVIEW_2026-11-05.md`, UI_GUIDELINES revision 1): a = bugs, theme, styles,
  formatting, Charges, texts; b = R-21 (owner 2026-11-05: Properties in a maximised Windows window show **one** column — `ListColumns`
  does not work on Windows; try keeping one `GridItemsLayout` and changing only its `Span` instead of replacing
  `ItemsLayout`, read the MAUI Windows handler first), choice field everywhere, field look, dialog frame on the phone, date lists in one
  column, compact-by-height on Android only, Delete at the end of the form + delete with Undo, segmented control;
  c = screen-by-screen usability (bill editor order, meter, readings, sync layout, period switcher in the filter line).

- T-037 Database clean-up (owner, 2026-11-05): purge soft-deleted rows + orphan attachment files, VACUUM, backup first;
  with sync on → replace the cloud or NeedsRejoin.

## Phase 10 — iOS (dropped)
- ~~T-033 iOS~~ — dropped (owner, 2026-11-05: no Mac to develop on). Windows + Android only.
