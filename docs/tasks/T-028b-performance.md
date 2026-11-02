# T-028b: Performance — menu switching, list scrolling, memory, battery
Phase: 8a · Depends on: T-028f, T-028d · Size: L

All findings are from **Release** builds (owner tests speed only in Release), phone Android 16, tablet Android 13,
Windows; owner's real data (~20 years, 3 properties, ~15 contracts) plus the Debug sample data.

## Findings (owner, 2026-11-01 / 11-02)
1. **Fast switching between menu items** makes the app slower and slower, "as if it keeps processing the previous request
   in the background". Phone copes better; on the **tablet** Android offered to close the app (**ANR**) during the tests
   and **once the app hung completely**.
2. **Scrolling stutters** in all lists and panes on phone and tablet.
3. Known: `CollectionView` resets to the top when `Items` is replaced (T-028f note); 29 pages are transient (a new page and
   ViewModel for every menu tap).

## Scope — do
1. **Measure first.** Add a lightweight timing log (Stopwatch) around page appearing → first data shown and around every
   list load; entries slower than 300 ms go to the error log as "Performance: <page/VM> <ms> ms (<items> items)" so the
   owner can copy and send them (Settings → Error log). Keep it on in Release (cheap). Report the numbers before/after in
   the PR (sample data on CI/sandbox is fine for relative numbers).
2. **Navigation never blocks or piles up work.**
   - Every page load runs off the UI thread (`Task.Run` for DB + calculations), only the final assignment on the UI thread.
   - Every load takes a `CancellationToken` that is cancelled in `OnDisappearing` / when a newer load starts
     (latest-wins); cancelled loads never touch the UI. EF queries pass the token.
   - No `.Result`/`.Wait()`/`GetAwaiter().GetResult()` on the UI thread (UI_GUIDELINES pitfalls; the startup exception
     stays as documented).
   - Decide page lifetime: section root pages (menu targets) as **singletons** or cached by Shell with a refresh on
     appearing, instead of building page + XAML + VM on every menu tap — measure both, choose, explain in the PR.
     Editors stay transient.
   - Rapid test: tapping 20 menu items as fast as possible must not freeze, must not produce an ANR, and the last tapped
     section must show its data within ~1 s after the taps stop.
3. **Scrolling.**
   - Card templates: flatten nested layouts (one `Grid` per card instead of Border → StackLayout → StackLayout …), no
     shadows on cards, avoid `IsVisible` toggles that re-measure, no converters where a precomputed string property does,
     compiled bindings (`x:DataType`) everywhere (check the build warnings for non-compiled bindings).
   - `CollectionView`: `ItemSizingStrategy="MeasureFirstItem"` where cards have the same height; keep virtualization
     (T-028c: never more than ~100 row views at once); no `CollectionView` inside a `ScrollView`.
   - Replacing items: update the collection in place (or keep the scroll position) instead of resetting `ItemsSource`.
   - Images (photos/attachments): thumbnails decoded at display size, never full images in lists.
4. **Memory:** check for leaks with repeated menu switching (event handlers / messenger registrations of transient VMs —
   `WeakReferenceMessenger.RegisterAll` in `ViewModelBase`, `IDataChangeNotifier.Changed`, `LayoutModeService` events must
   be unsubscribed or weak). A test that creates and disposes VMs and checks that handlers are released where possible.
5. **Startup:** measure cold start to the Overview; move anything not needed for the first screen after it is shown
   (reminder rescheduling already runs in the background).
6. **Battery (Android):** confirm nothing runs while the app is closed except the reminder alarms (T-028e); no timers or
   polling while the app is in the background (stop any `Dispatcher` timers on `Stopped`).

## Scope — do NOT
No visual redesign (keep the look of the cards), no new packages unless a measurement proves the need (then ask in the PR),
no changes to calculations.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (linux, windows).
2. PR lists the measured times (before/after) per main page and the decisions (page lifetime, templates changed).
3. Owner, Release build, real data: rapid menu switching on the tablet without ANR or hang; scrolling Bills (all years),
   Payments, Readings history, Overview smooth on phone and tablet; the error log shows no "Performance" entries over ~1 s
   in normal use.

## Manual test steps for the owner
1. Tablet, Release: tap Overview → Bills → Payments → Readings → Charges → Reports → Settings … quickly 20 times → no
   "app isn't responding", the last section shows its data within about a second.
2. Bills → All years → scroll fast to the end and back; same for Payments and a meter's reading history → smooth.
3. Settings → Error log → Copy → send the lead the "Performance" lines (if any).
4. Leave the phone with the app closed overnight → battery usage of the app in Android settings ≈ 0 %.
