# Architecture

Status: **agreed baseline** (2026-10-19). Changes go through the project lead.

## 1. Goals and constraints

- Track several properties (house, cottage, apartment), their meters, readings, supplier contracts,
  tariffs, calculated charges, supplier bills, payments, balances and property taxes.
- Platforms: **Windows and Android are mandatory**, iOS later (code must stay iOS-compatible).
- Data is entered on more than one device (phone at the meter, PC for analysis), so **sync is a core
  concern from day one**, even though the sync engine itself is built late.
- UI languages: English (base) and Russian; adding a language must require only a new resource file.
- Offline first: the app works fully without network; Google Drive is used for sync and backup.
- Long history must be importable (the owner's Excel goes back to 2004, amounts in LVL before 2014).

## 2. Technology stack

| Concern | Choice | Notes |
|---|---|---|
| Runtime | .NET 10 (LTS) | |
| UI | .NET MAUI | `net10.0-android`, `net10.0-windows10.0.19041.0`; `net10.0-ios` added later |
| MVVM | CommunityToolkit.Mvvm | source generators |
| UI helpers | CommunityToolkit.Maui | |
| Storage | SQLite via EF Core 10 (`Microsoft.EntityFrameworkCore.Sqlite`) | migrations in `HomeUtilities.Data` |
| Charts | LiveChartsCore (SkiaSharp, MAUI) | bar charts, multi-property series |
| Excel import/export | ClosedXML | |
| Notifications | behind `INotificationService` | Android/iOS: Plugin.LocalNotification; Windows: Windows App SDK app notifications |
| Google Drive | Google.Apis.Drive.v3 | scope `drive.appdata` (hidden app folder only) |
| Tests | xUnit, plain `Assert` | no FluentAssertions (license) |
| CI | GitHub Actions | Linux: core + tests + Android build; Windows: Windows build |

Package versions live in `Directory.Packages.props` (central package management).

## 3. Solution structure

```
HomeUtilities.sln
Directory.Build.props          common settings (nullable, warnings as errors, LangVersion)
Directory.Packages.props       central package versions
src/
  HomeUtilities.Core/          domain model, calculation engine, interfaces. net10.0, no dependencies on EF/MAUI
  HomeUtilities.Data/          EF Core DbContext, configurations, migrations, repositories. net10.0
  HomeUtilities.ViewModels/    screen logic (MVVM), navigation abstractions. net10.0, no MAUI
  HomeUtilities.Import/        Excel import/export (ClosedXML). net10.0           (added in phase 7)
  HomeUtilities.Sync/          Google Drive sync engine. net10.0                    (added in phase 9)
  HomeUtilities.App/           MAUI app: XAML pages, resources, platform services
tests/
  HomeUtilities.Core.Tests/
  HomeUtilities.Data.Tests/        (SQLite in-memory)
  HomeUtilities.ViewModels.Tests/
HomeUtilities.Tests.slnf       solution filter: everything that builds on Linux without MAUI
```

Dependency direction (arrows = "references"):

```
App ──► ViewModels ──► Core
 │          ▲           ▲
 └──► Data ─┘───────────┘
 └──► Import, Sync ─────► Core
```

`Core` knows nothing about storage or UI. `ViewModels` depend on repository and service **interfaces**
from Core; `App` wires implementations via dependency injection (`MauiProgram.cs`).

## 4. Key design principles

1. **Everything is time-versioned.** Tariffs, VAT rates, area, declared residents, equalized payment
   amounts have a `ValidFrom` date. Calculations for a past period always use the values valid then.
2. **Calculated charge ≠ supplier bill ≠ payment.** These are three separate entities. The app computes
   its own expected charge, the user enters the supplier's bill total (line details optional), and the
   difference is shown and kept for reports. Payments are allocated to bills; unallocated money is a
   prepayment on the contract balance.
3. **Contract balance** = payments − bills. Positive = overpayment (prepayment), negative = debt.
4. **Meters belong to a property, not to a contract.** One physical meter may feed several contracts
   (hot water meter → water supplier *and* heat supplier for water heating).
5. **Meters have registers** (total / day / night / export) so multi-tariff and solar export are the
   same mechanism.
6. **Sync-ready identity.** GUID ids generated on the device, UTC `CreatedAt`/`UpdatedAt`, soft delete.
7. **Money:** `decimal`, currency per amount (EUR, historical LVL). Rounding to 0.01 using
   `MidpointRounding.AwayFromZero`, applied per charge line (configurable per contract later if a
   supplier rounds differently).
8. **Thin UI.** Pages are bindings + layout.
9. **Adaptive layouts for three form factors:** Android phone (portrait, narrow), Android tablet
   (portrait and landscape) and Windows desktop (resizable window). Layout is chosen by **available
   width, not by platform**: compact < 600 dp (single column, phone), medium 600–1000 dp
   (tablet portrait: list + detail or two columns), expanded > 1000 dp (tablet landscape / desktop:
   list + detail side by side, wider charts). Use `AdaptiveTrigger`/`VisualStateManager` or an
   `ILayoutModeService` exposed to ViewModels — no `DeviceIdiom` checks in pages. Rotation must
   not lose entered data. Every UI task lists manual checks for phone and tablet.

## 5. Localization

- `HomeUtilities.App/Resources/Strings/AppResources.resx` (English, neutral) and `AppResources.ru.resx`.
- ViewModels never contain display strings; they expose keys/enums, and the UI layer resolves text via
  a `ILocalizer` service (implemented with `ResourceManager`). This keeps ViewModels testable.
- Language is chosen in Settings (default: system language if supported, otherwise English) and is
  applied by setting `CultureInfo.CurrentUICulture`. Number/date formats follow the chosen culture.
- Adding a language = add `AppResources.<code>.resx` + add the code to the supported-languages list.

## 6. Platform services (interfaces in Core/ViewModels, implementations in App)

| Interface | Purpose |
|---|---|
| `IClock` | current date/time (testable) |
| `ILocalizer` | UI strings |
| `IDialogService` | alerts, confirmations |
| `INavigationService` | page navigation (Shell routes) |
| `ILauncherService` | open supplier website / portal in the default browser |
| `INotificationService` | schedule / cancel local reminders |
| `IFilePickerService` | pick Excel / attachment files |
| `ISecureStore` | store OAuth tokens (MAUI `SecureStorage`, never synced) |
| `IAppPaths` | database, attachments, backups folders |

## 7. Data storage

- One SQLite file in the app data folder (`FileSystem.AppDataDirectory`).
- EF Core migrations are applied at startup.
- Attachments (bill PDFs, meter photos) are files in an `attachments/` folder, referenced by
  `Attachment` records; file name = attachment GUID + extension.
- Automatic local backup (copy of the DB) on startup if the last backup is older than N days; keep last K.

## 8. Synchronization (design now, build in phase 9)

- Google Drive **appDataFolder** (scope `drive.appdata`): files are invisible in the user's Drive UI and
  the app cannot see other files. Minimal permissions.
- Each device has a stable `DeviceId`. Each device uploads its own **change log** files
  (`changes/<deviceId>/<sequence>.json`) containing changed entities (full row JSON incl. `UpdatedAt`).
- On sync a device downloads change logs of other devices it has not yet applied and merges
  per entity: **last writer wins by `UpdatedAt`**; soft-deleted rows win over older edits.
- Periodic compacted snapshot (`snapshot.json.gz`) so a new device does not replay the full history.
- Attachments are uploaded as separate files, referenced by GUID.
- Never sync the raw SQLite file (risk of corruption and lost edits).
- Consequence for all phases: every write goes through repositories that set `UpdatedAt` and never
  hard-delete. The `SyncEntity` base class enforces this.

## 9. Secrets

- Google OAuth client id: per platform. Desktop/installed-app client ids are not truly secret, but are
  still kept out of the repo: provided via a git-ignored `secrets.json` / build property and injected at
  build time. Details in the sync phase task.
- Supplier portal passwords are **never stored** by the app. Links open in the system browser, whose
  password manager fills credentials. Only an optional login name may be kept on the contract.

## 10. CI

`.github/workflows/ci.yml`, on every PR and push to `main`:
- **linux** job: restore, build `HomeUtilities.Tests.slnf`, run tests; build the App for Android.
- **windows** job: build the App for Windows.
- Later: **macos** job for iOS build (free for public repositories).

`main` is protected: merging requires green CI.

## 11. Decision log

| Date | Decision | Reason |
|---|---|---|
| 2026-10-19 | .NET MAUI on .NET 10 instead of WinForms | Android is mandatory, iOS desired |
| 2026-10-19 | Bills entered as total (variant B), lines optional | fast entry, difference still visible |
| 2026-10-19 | Supplier website / portal links on supplier and contract | user request |
| 2026-10-19 | UI languages EN + RU, extensible via resx | user request |
| 2026-10-19 | Multi-register meters (day/night/export) | dual-tariff meters and solar panels |
| 2026-10-19 | Sync via change logs in Drive appDataFolder | multi-device edits, no DB file sync |
| 2026-10-19 | Public repository, no personal data in repo | free CI incl. macOS |
| 2026-10-19 | No portal passwords stored; links open in system browser | browser password manager fills them |
| 2026-10-19 | Android tablet is a target form factor; width-based adaptive layouts | user request |
| 2026-10-20 | User-defined payment categories (ServiceKind stays technical) | non-utility payments, e.g. kindergarten |
| 2026-10-20 | Payment details (IBAN, purpose template) with copy buttons | faster internet-bank payments |
| 2026-10-20 | Free-text comment on every editable entity | user request |
| 2026-10-25 | Only free open-source dependencies with permissive licences (MIT/Apache/BSD…) | owner decision; see CLAUDE.md |
| 2026-10-20 | Bill period = service/readings month; prepaid services via `BillingTiming` | owner's billing practice |
| 2026-10-20 | Bills without contract (one-off), BillTemplate with schedule (once / monthly / every N months / yearly) | non-monthly and one-off payments |
| 2026-10-20 | Uniform amount fields Net / VatPercent / Vat / Gross | consistency |
| 2026-10-19 | Coding sessions run in the cloud (Linux) | Windows target is verified only by CI and the owner |
