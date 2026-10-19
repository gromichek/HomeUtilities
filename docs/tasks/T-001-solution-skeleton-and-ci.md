# T-001: Solution skeleton, MAUI shell, localization, CI
Phase: 0 · Depends on: — · Size: M

## Goal
Create the empty but complete solution structure so that every later task only adds code:
projects, central package management, a MAUI app that starts and shows a localized (EN/RU) start page,
one smoke test, a dev setup script, and a GitHub Actions CI that builds and tests on Linux and Windows.

## Context
- `docs/ARCHITECTURE.md` §2 (stack), §3 (solution structure), §5 (localization), §10 (CI)
- `CLAUDE.md` (rules)

## Scope — do

### 1. Repository files
- `Directory.Build.props` (repo root): `Nullable=enable`, `ImplicitUsings=enable`,
  `LangVersion=latest`, `TreatWarningsAsErrors=true`, `Deterministic=true`,
  `<ManagePackageVersionsCentrally>` is in `Directory.Packages.props`.
- `Directory.Packages.props`: central versions for everything used in this task
  (MAUI controls come from the workload; add CommunityToolkit.Mvvm, CommunityToolkit.Maui,
  Microsoft.Extensions.Logging.Debug, xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk,
  coverlet.collector). Use current stable versions compatible with .NET 10.
- `global.json`: pin SDK major 10 with `rollForward: latestFeature`.
- `.editorconfig`: standard C# conventions (file-scoped namespaces, `var` when type is apparent,
  4 spaces, `_camelCase` private fields, `PascalCase` public members).
- `.gitignore`: keep existing VisualStudio template, **append**:
  ```
  # Local data and personal files — never commit
  *.db
  *.db-shm
  *.db-wal
  /private/
  secrets.json
  *.xlsx
  !tests/**/Fixtures/*.xlsx
  ```
- `scripts/setup-dev.sh`: installs the .NET 10 SDK on Ubuntu if `dotnet` is missing
  (`apt-get install -y dotnet-sdk-10.0`; fallback: official `dotnet-install.sh` into `~/.dotnet`),
  then prints `dotnet --info`. Idempotent. Mark executable.

### 2. Projects (`src/`, `tests/`)
| Project | Type | TFM | References |
|---|---|---|---|
| `src/HomeUtilities.Core` | class library | `net10.0` | — |
| `src/HomeUtilities.Data` | class library | `net10.0` | Core |
| `src/HomeUtilities.ViewModels` | class library | `net10.0` | Core; CommunityToolkit.Mvvm |
| `src/HomeUtilities.App` | MAUI app | see below | Core, Data, ViewModels; CommunityToolkit.Maui |
| `tests/HomeUtilities.Core.Tests` | xUnit | `net10.0` | Core |
| `tests/HomeUtilities.ViewModels.Tests` | xUnit | `net10.0` | ViewModels |

Do **not** create Data.Tests, Import or Sync yet.

App target frameworks:
```xml
<TargetFrameworks>net10.0-android</TargetFrameworks>
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
<!-- iOS will be added in phase 10: net10.0-ios -->
```
- `ApplicationTitle`: HomeUtilities; `ApplicationId`: `io.github.gromichek.homeutilities`.
- Windows: unpackaged (`<WindowsPackageType>None</WindowsPackageType>`) so it runs with F5 from
  Visual Studio without MSIX certificates.
- Android: `SupportedOSPlatformVersion` 26 (Android 8.0) minimum.
- Default MAUI template icon/splash are fine for now.

Solution files:
- `HomeUtilities.sln` with all projects, grouped in solution folders `src` and `tests`.
- `HomeUtilities.Tests.slnf`: solution filter with everything **except** `HomeUtilities.App`
  (this is what builds on any OS without MAUI workloads).

### 3. Minimal content
- **Core**: `IClock` interface (`DateOnly Today`, `DateTimeOffset UtcNow`) + `SystemClock` implementation.
  That's all.
- **ViewModels**: `ILocalizer` interface (`string this[string key]`, `string Get(string key, params object[] args)`)
  and `MainViewModel` (ObservableObject) with properties `Title` (from localizer key `AppTitle`) and
  `Today` (from `IClock`), plus a `[RelayCommand] ToggleLanguage` that calls an injected
  `ILanguageService.SetLanguage(string cultureCode)` switching between `en` and `ru`.
  Define `ILanguageService` (`IReadOnlyList<string> SupportedLanguages`, `string CurrentLanguage`,
  `void SetLanguage(string code)`, event `LanguageChanged`) in ViewModels.
  `MainViewModel` refreshes `Title` on `LanguageChanged`.
- **App**:
  - `Resources/Strings/AppResources.resx` (en) and `AppResources.ru.resx` with keys:
    `AppTitle` ("Home utilities" / "Коммунальные платежи"), `StartPageHint`
    ("Nothing here yet." / "Здесь пока пусто."), `SwitchLanguage` ("Русский" / "English"),
    `TodayIs` ("Today is {0:d}" / "Сегодня {0:d}").
  - `ResxLocalizer : ILocalizer`, `LanguageService : ILanguageService` (sets
    `CultureInfo.CurrentCulture`/`CurrentUICulture`, `DefaultThreadCurrent*`, persists choice in
    `Preferences`, raises `LanguageChanged`). Default: system language if `ru`, else `en`.
  - `AppShell` with one route `MainPage`. `MainPage.xaml` shows Title, `TodayIs` text, hint and a
    button bound to `ToggleLanguageCommand` with text `SwitchLanguage`. Texts must update immediately
    after switching (no restart).
  - `MauiProgram.cs`: register `IClock`, `ILocalizer`, `ILanguageService`, `MainViewModel`, `MainPage`
    in DI; `UseMauiCommunityToolkit()`.
- **Tests**:
  - Core.Tests: `SystemClockTests` — `Today` equals `DateOnly.FromDateTime(DateTime.Now)` (tolerate
    midnight edge by comparing against values captured before/after).
  - ViewModels.Tests: `MainViewModelTests` with fake `ILocalizer`, fake `IClock`, fake
    `ILanguageService`: (a) Title comes from localizer; (b) ToggleLanguage switches en→ru→en;
    (c) Title is refreshed after `LanguageChanged`.

### 4. CI — `.github/workflows/ci.yml`
Triggers: `pull_request` to `main`, `push` to `main`, `workflow_dispatch`.
Concurrency: cancel in-progress runs for the same ref.

Job `linux` (`ubuntu-latest`):
1. checkout; `actions/setup-dotnet` with `global-json-file: global.json`
2. `dotnet restore HomeUtilities.Tests.slnf`, `dotnet build HomeUtilities.Tests.slnf -c Release --no-restore`
3. `dotnet test HomeUtilities.Tests.slnf -c Release --no-build --logger trx --results-directory TestResults`
4. upload `TestResults` as artifact (always)
5. `actions/setup-java` (Temurin 17), `dotnet workload install maui-android`,
   `dotnet build src/HomeUtilities.App -c Release -f net10.0-android`

Job `windows` (`windows-latest`):
1. checkout; setup-dotnet from global.json
2. `dotnet workload install maui-windows` (use `maui` if `maui-windows` is unavailable)
3. `dotnet build src/HomeUtilities.App -c Release -f net10.0-windows10.0.19041.0`

Cache NuGet packages (`actions/setup-dotnet` cache or `actions/cache` on `~/.nuget/packages`, keyed by
`Directory.Packages.props`).

### 5. README
Replace README with: one-paragraph description, "Status: early development", how to build
(Visual Studio 2022 17.14+ / 2026 with the .NET MAUI workload, open `HomeUtilities.sln`, select
`HomeUtilities.App` and target "Windows Machine"), how to run tests (`dotnet test HomeUtilities.Tests.slnf`),
links to `docs/ARCHITECTURE.md`, `docs/ROADMAP.md`, `docs/PROGRESS.md`, licence line.

### 6. PROGRESS.md
Set T-001 to `In review` with the PR number.

## Scope — do NOT
- No domain entities, EF Core, database, navigation framework beyond the single Shell route.
- No iOS target, no macOS CI job.
- No custom styles/themes beyond the template defaults.

## Acceptance criteria
1. `dotnet build HomeUtilities.Tests.slnf` and `dotnet test HomeUtilities.Tests.slnf` succeed on Linux
   with zero warnings; all tests green.
2. CI workflow exists and **both jobs are green on the PR**. If the Android or Windows build fails for
   environment reasons you cannot fix, describe it precisely in the PR.
3. The App project builds for Windows in Visual Studio and starts showing the start page (verified by
   the owner).
4. Switching language changes all four texts at once, and the choice survives an app restart.
5. No UI text is hard-coded in XAML/C#; all comes from `AppResources*.resx`.
6. `.gitignore` additions are present; no personal data in the repo.

## Tests required
See §3 "Tests". Minimum 4 tests total.

## Manual test steps for the owner
1. `git pull`, open `HomeUtilities.sln` in Visual Studio.
2. Set `HomeUtilities.App` as startup project, target **Windows Machine**, press F5.
3. Expected: a window with the title "Home utilities" (or Russian, if Windows is in Russian), today's
   date, the hint text and a language button.
4. Click the button → all texts switch language immediately.
5. Close and restart the app → the chosen language is kept.
6. Optional: run on an Android emulator or phone (target **Android Emulator** / your device).
