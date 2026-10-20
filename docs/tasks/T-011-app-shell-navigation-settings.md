# T-011: App shell, navigation, settings, adaptive layout infrastructure
Phase: 3 · Depends on: T-010 · Size: L

## Goal
Turn the single start page into the real application frame that every later screen plugs into: Shell with
sections, navigation service, ViewModel base class and page lifecycle, adaptive layout modes
(phone / tablet / desktop), basic styles with light/dark theme, a Settings page, and dialogs.
No data screens yet — sections other than Overview and Settings show a localized placeholder.

## Context
- `docs/ARCHITECTURE.md` §4.8–4.9 (thin UI, adaptive layouts), §5 (localization), §6 (platform services)
- Known issue in `docs/PROGRESS.md`: `MainViewModel` never unsubscribes from `LanguageChanged`.

## Scope — do

### 1. ViewModels infrastructure (`HomeUtilities.ViewModels`, no MAUI references)
- `abstract class ViewModelBase : ObservableObject` — `[ObservableProperty] bool IsBusy`, `string Title`,
  virtual `Task OnAppearingAsync()`, `Task OnDisappearingAsync()`; `RefreshTexts()` virtual, called on language
  change. Language change notification via `WeakReferenceMessenger` message `LanguageChangedMessage` (sent by
  `LanguageService`) — **no direct event subscriptions** from view models (fixes the known issue; remove the
  old event usage).
- `INavigationService` — `Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)`,
  `Task GoBackAsync()`. Route names as constants in `static class Routes` (`Overview`, `Properties`,
  `Readings`, `Bills`, `Payments`, `Reports`, `Settings`).
- `IDialogService` — `Task AlertAsync(string title, string message)`, `Task<bool> ConfirmAsync(string title,
  string message, string accept, string cancel)`, `Task<string?> ChooseAsync(string title, IReadOnlyList<string> options)`.
- Layout modes:
  `enum LayoutMode { Compact, Medium, Expanded }`, `static class LayoutModes { LayoutMode FromWidth(double widthDp) }`
  with thresholds `< 600` Compact, `600–1000` Medium (1000 inclusive → Medium), `> 1000` Expanded;
  `ILayoutModeService` — `LayoutMode Current`, message `LayoutModeChangedMessage` on change.
- `ShellViewModel` — localized titles of all sections (refresh on language change), `LayoutMode` (from the
  service), `bool IsFlyoutLocked => LayoutMode == Expanded`.
- `OverviewViewModel` — the former `MainViewModel` (database status line) renamed; keep its tests.
- `SettingsViewModel` — language list (display names localized: "English", "Русский"), selected language
  (applies immediately via `ILanguageService`), theme (`System`, `Light`, `Dark`) via new `IThemeService`
  (`AppTheme Current`, `Set(AppTheme)`; enum `AppThemeChoice` in ViewModels), read-only info: app version
  (`IAppInfo` interface: `Version`), database path (`IAppPaths`).
- `PlaceholderViewModel` — shows "Coming soon" text for a section.

### 2. App (`HomeUtilities.App`)
- `AppShell`: `FlyoutItem`s for all sections with icons from a simple built-in font or text glyphs (no icon
  package); titles bound to `ShellViewModel`. `FlyoutBehavior`: `Flyout` in Compact/Medium, `Locked` in
  Expanded (bind/trigger on `IsFlyoutLocked`). Remove the old `MainPage` route.
- Pages: `OverviewPage`, `SettingsPage`, `PlaceholderPage` (used for Properties, Readings, Bills, Payments,
  Reports with a route parameter or separate thin subclasses — your choice, list it in "Decisions made").
- `PageBase : ContentPage` (or behavior) that calls `OnAppearingAsync/OnDisappearingAsync` of the
  ViewModel — code-behind of pages stays at constructor + `BindingContext`.
- Services: `ShellNavigationService : INavigationService` (Shell.GoToAsync), `DialogService`,
  `LayoutModeService` (listens to the main `Window` size changes, converts to dp, publishes message only on
  mode change), `ThemeService` (`Application.Current.UserAppTheme`, persisted in `Preferences`),
  `AppInfoService`.
- `Resources/Styles/Colors.xaml` + `Styles.xaml`: a small palette (primary, surface, text, muted, error) with
  `AppThemeBinding` for light/dark; implicit styles for `Label`, `Button`, `Entry`, `Picker`,
  `ContentPage` background. Keep it minimal and neutral.
- DI registrations for all new services, view models (transient) and pages (transient).
- All new UI text in `AppResources.resx` + `.ru.resx` (section titles, settings labels, "Coming soon",
  theme names, "Version", "Database file").

### 3. Responsive sample
On `SettingsPage` show the current layout mode (localized "Layout: phone / tablet / desktop") — this is the
manual check that the adaptive infrastructure works; resize the Windows window to see it switch.

## Scope — do NOT
- No data screens (T-012+). No custom controls library. No icon fonts download.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. App starts on the Overview page with the database status line (as before).
3. Flyout lists 7 sections; Settings changes language and theme immediately, both persist across restarts.
4. Windows: widening the window above ~1000 px locks the flyout open; narrowing below unlocks it; the layout
   line on Settings changes accordingly. Android phone = phone; tablet portrait = tablet; tablet landscape = desktop
   (depending on emulator width).
5. Known issue "MainViewModel subscribes … never unsubscribes" resolved.

## Tests required (ViewModels.Tests)
- `LayoutModesTests`: 0, 599.9 → Compact; 600, 1000 → Medium; 1000.1, 1920 → Expanded.
- `ShellViewModelTests`: titles localized; refresh after `LanguageChangedMessage`; `IsFlyoutLocked` follows
  `LayoutModeChangedMessage`.
- `SettingsViewModelTests`: selecting language calls `ILanguageService.SetLanguage`; theme selection calls
  `IThemeService.Set`; version and database path shown.
- `OverviewViewModelTests`: former MainViewModel tests still pass after rename; texts refresh via messenger.
- A test that a view model no longer receives messages after it is garbage-collected is **not** required
  (weak messenger guarantees it).

## Manual test steps for the owner
1. Pull, run on **Windows Machine**: Overview shows "Database ready: 16 categories, 3 VAT rates".
2. Open the menu (☰): 7 sections. Open Properties → "Coming soon".
3. Settings: switch to English/Русский → all titles change at once; switch theme Light/Dark/System → colours change.
4. Restart → language and theme are kept.
5. Make the window wide (> 1000 px): menu stays open on the left; narrow it: menu collapses to ☰. Settings shows
   "Layout: desktop / tablet / phone" accordingly.
6. Android phone emulator: menu via ☰, Settings shows "phone". Tablet emulator: rotate → "tablet" / "desktop".
