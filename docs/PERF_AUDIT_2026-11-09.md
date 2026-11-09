# Performance and memory audit — 2026-11-09

Static review of commit `d0b6051` (2026-11-07). No code was changed. Line numbers refer to that commit.

## Summary

Only clear defects go into work: 11 findings about leaks, resource release and memory peaks. None of the fixes changes
how the UI behaves. The owner considers the current speed and smoothness satisfactory — they are not touched.

- **Leaks:** nothing critical. One latent leak (`OverviewViewModel` subscribes to the sync singleton with a lambda),
  one Android lifecycle risk (`AppShell` is a singleton when the activity is recreated) and four places with fragile
  unsubscription.
- **Photos:** processing a 12 MP photo on Android peaks at about 100 MB (risk of a crash on the tablet); on Windows
  photos are stored without shrinking and bloat the database folder and the cloud.
- **Sync:** the snapshot is built in memory as four copies.
- **Cancellation and errors:** two `CancellationTokenSource` instances are not disposed; in two places an error is lost
  or a dialog can hang.
- **Not touched:** nine findings about speed and smoothness (including SQLite on the UI thread) are listed under
  "Deferred" for information only.
- **Rollout:** four small steps, one PR each, each tested by the owner.

## Context and method

Static code review without a profiler. Memory estimates and view counts are calculated; confirm them by measuring on
the owner's devices.

| Item | Value |
| --- | --- |
| Repository | `gromichek/HomeUtilities`, commit `d0b6051` |
| Platform | .NET 10 MAUI, Windows (unpackaged) + Android; EF Core 10 + SQLite (WAL); CommunityToolkit.Mvvm 8.4 / Maui 15 |
| Size | App 6.7k lines C# + 71 XAML; ViewModels 34k; Core 14.8k; Data 7k; Sync 0.9k |
| How | search for every `+=` subscription, handler mapper, `Task.Run` / `.Result`, `IDisposable`; manual review of the key files |

**T-039 revert and the owner's decision.** On 2026-11-07, T-039 (#273, #275) moved editor loading off the UI thread
and changed the field layout. The owner reverted it (#278): the app felt jerkier. On 2026-11-09 the owner decided: the
current speed is satisfactory, fix only clear defects, do not touch smoothness or UI threading. Preloading the data
before opening an editor was also rejected: after a tap nothing happens and the button seems not to have been pressed.

**Priorities:** Medium — noticeable in some scenarios or a risk of failure. Low — hygiene and protection for the future.

## Findings to fix

11 findings: 4 medium, 7 low. Details below by ID.

| ID | Priority | Problem | Where | Step |
| --- | --- | --- | --- | --- |
| R-1 | Medium | ~100 MB peak when shrinking a photo on Android, bitmaps not `Recycle()`d; no shrinking on Windows | `Services/ImageProcessor.cs`, `Stores/AttachmentStore.cs` | 2 |
| R-2 | Medium | A 72×72 thumbnail decodes the full photo on Windows | `StreamFactoryImageConverter`, `ReadingsPage.xaml` | 2 |
| R-3 | Medium | Sync snapshot: the whole database as JSON, 4 copies in memory (LOH) | `SyncEngine.WriteSnapshotAsync`, `SyncFiles.Gzip` | 3 |
| M-2 | Medium | `AppShell` is a singleton; a recreated activity gives the same Shell to a new `Window` (verify first) | `MauiProgram`, `App.CreateWindow` | 4 |
| M-1 | Low | Lambda on `ISyncEngine.StatusChanged` without unsubscription | `OverviewViewModel.cs:128` | 1 |
| R-5 | Low | An editor load error is lost; a dialog can hang when `PopModalAsync` fails | `EditorPageBase.cs:46`, `DialogService.CloseModalAsync` | 1 |
| R-4 | Low | `CancellationTokenSource` cancelled but not disposed | `ReportsViewModel.cs:546`, `SyncScheduler` (debounce) | 1 |
| M-6 | Low | `window.Activated +=` without protection against a repeated `OnAppearing` | `ReadingsPage.xaml.cs:41` | 1 |
| M-4 | Low | Strong subscription to the items source's `CollectionChanged` | `ChoiceField.xaml.cs:147` | 1 |
| M-5 | Low | A child view's `PropertyChanged` handler is not removed on `ChildRemoved` | `AdaptiveFieldGrid.cs:73` | 1 |
| M-3 | Low | `SnackbarHost` unsubscribes from the singleton only through automatic handler disconnection | `SnackbarHost.xaml.cs:51` | 1 |

## 1. Memory leaks

No real page leaks were found: all ~50 event subscriptions were checked, two are risky. Menu section pages are created
once by Shell (`ShellContent` cache); editors are transient and are released when closed.

### M-1. Overview lambda subscription to the sync singleton (low, latent)

`OverviewViewModel` (transient) subscribes in its constructor to `ISyncEngine.StatusChanged` (singleton) with a lambda
and never unsubscribes. Today Overview is created once, so the leak does not show. It becomes real as soon as the page
is ever recreated (see M-2): the old view model with all its cards and charts would stay alive.

**Fix:** reuse the pattern that already works in `SyncSettingsViewModel`: a named handler, subscribe in
`OnAppearingAsync`, unsubscribe in `OnDisappearingAsync`.

```csharp
// OverviewViewModel: subscribe only while the page is on screen.
public override Task OnAppearingAsync()
{
    if (_sync is not null && !_syncSubscribed)
    {
        _sync.StatusChanged += OnSyncStatusChanged;
        _syncSubscribed = true;
        UpdateSyncHint(); // catch up with changes missed while hidden
    }
    return base.OnAppearingAsync(); // keep the existing load call here
}

public override Task OnDisappearingAsync()
{
    if (_sync is not null && _syncSubscribed)
    {
        _sync.StatusChanged -= OnSyncStatusChanged;
        _syncSubscribed = false;
    }
    return base.OnDisappearingAsync();
}

// Raised on the sync thread: marshal to the UI thread.
private void OnSyncStatusChanged(object? sender, EventArgs e) =>
    _dispatcher?.Post(UpdateSyncHint);
```

Alternative: `WeakReferenceMessenger` (the engine sends a `SyncStatusChangedMessage`), the way `LanguageChangedMessage`
and `LayoutModeChangedMessage` already work.

### M-2. AppShell singleton when the activity is recreated (medium, needs verification)

`AddSingleton<AppShell>()` + `new Window(GetRequiredService<AppShell>())` in `App.CreateWindow`. On Android, MAUI calls
`CreateWindow` again when the system destroyed the activity while the process lives on (low memory on the tablet, a
change of the system language). The same Shell with all its cached pages is then attached to a second `Window`. Handler
exceptions or a retained old native view tree are possible. `ConfigurationChanges` in `MainActivity` covers rotation,
not these cases. A link to the single tablet hang in T-028b is a hypothesis only.

**Check:** Android → Developer options → "Don't keep activities", leave and return to the app, look at the error log.
**Fix (if confirmed):** register `AppShell` as transient — a new Shell per window; `ShellViewModel` stays a singleton.
The `window.*` subscriptions in `App.StartSync` / `StartReminders` are already bound to the specific window — correct.

### M-3 … M-6. Fragile unsubscription (low)

| ID | What is wrong | Fix |
| --- | --- | --- |
| M-3 | `SnackbarHost` unsubscribes from the singleton `SnackbarService` only in `OnHandlerChanged` (handler = null). This works thanks to `HandlerDisconnectPolicy.Automatic` (default since .NET 9), but the dependency is implicit: the host is on every page, and any failure to disconnect would keep every closed editor alive. | Add a second unsubscription in `Unloaded` and a leak test (see "Rollout"). |
| M-4 | `ChoiceField` holds a strong subscription to its `ItemsSource`'s `CollectionChanged` and removes it only when the source changes. Today the sources live in the page's view model — no leak. Binding to a collection in a singleton (`*FilterState`) would make it real. | Unsubscribe in `Unloaded`, subscribe in `Loaded`; or `WeakEventManager`. |
| M-5 | `AdaptiveFieldGrid` attaches a lambda to every child's `PropertyChanged` and does not remove it in `ChildRemoved`. A moved view keeps poking the old grid; adding it again doubles the handler. | Named `OnChildPropertyChanged` method, `-=` in `ChildRemoved`. Do not change the layout logic. |
| M-6 | `ReadingsPage.OnAppearing` does `window.Activated +=` without a preceding `-=`. Two `OnAppearing` calls in a row (happens on WinUI) run `CheckReturnFromSubmitPageAsync` twice. | `window.Activated -= OnWindowActivated;` before `+=`; keep the window in a field and unsubscribe from that one. |

There are no other handlers or effects. `WheelForwardingWindows` uses a static handler with `RemoveHandler` before
`AddHandler` — correct. `HelpPage` only changes `WebView` settings, no subscriptions — correct.

## 2. Resources and async

Database access is correct: `IDbContextFactory` + `await using` per operation, `AsNoTracking` for reads, WAL. The
problems are in photo processing, the sync snapshot and a few places without `Dispose` / `try-finally`.

### R-1. Memory when shrinking photos (medium)

Peak estimate for a 4000×3000 photo (~5 MB JPEG) on Android:

| Step | Where | Memory |
| --- | --- | --- |
| read into `byte[]` | `AttachmentStore.AddAsync` | ~5 MB |
| `new MemoryStream(bytes)` → `CopyTo` → `ToArray()` | `ImageProcessor.Shrink` | +10 MB |
| decode: `InSampleSize` = 1 because 4000/2 < 2560 | `BitmapFactory.DecodeByteArray` | +48 MB |
| `CreateScaledBitmap` to 2560×1920 | | +19.7 MB |
| `CreateBitmap` with rotation | | +19.7 MB |
| **Peak** | | **~100 MB** |

The bitmaps are released with `Dispose()` (releases only the managed wrapper), not `Recycle()`; the pixels stay until
the Java GC runs. On Windows photos are stored without shrinking (known issue).

**Fix — shrink when the photo is picked, with MAUI 10.** In .NET 10, `MediaPickerOptions` has `MaximumWidth`,
`MaximumHeight`, `CompressionQuality`, `RotateImage` and `PreserveMetaData`
([docs](https://learn.microsoft.com/dotnet/maui/platform-integration/device-media/picker)). One change in `MediaService`
covers Android and Windows. Keep the quality below 90: with 90+ and no size limits MAUI 10 may return a PNG
([details](https://startdebugging.net/2026/09/fix-mediapicker-capturephotoasync-returns-png-instead-of-jpeg-in-maui-10/)).

```csharp
// MediaService: let MAUI downscale and re-encode on every platform.
private static readonly MediaPickerOptions PhotoOptions = new()
{
    MaximumWidth = ImageProcessor.MaxSide,   // 2560
    MaximumHeight = ImageProcessor.MaxSide,
    CompressionQuality = 85,                 // below 90: always JPEG
    RotateImage = true,                      // apply EXIF orientation
};

var photo = await MediaPicker.Default.CapturePhotoAsync(PhotoOptions);
var picked = await MediaPicker.Default.PickPhotosAsync(PhotoOptions);
```

On Windows the photo is currently picked with `FilePicker` — switch to `MediaPicker.PickPhotosAsync` with the same
options if shrinking works there (verify in practice). Keep `ImageProcessor` as a safety net for photos from other
sources, but fix it: compute `InSampleSize` from the target size (power of two ≤ source / target),
`buffer.TryGetBuffer()` instead of `ToArray()`, `Recycle()` the intermediate bitmaps, rotation and scaling in one matrix
and one `CreateBitmap`. The peak drops to roughly 35 MB.

### R-2. Thumbnail from the full photo (medium, Windows)

`StreamFactoryImageConverter` gives `ImageSource.FromStream` with the original file to a 72×72 `Image` on
`ReadingsPage`. Glide on Android downsamples by itself; WinUI decodes the whole bitmap (12 MP ≈ 48 MB per thumbnail).
After R-1 the picked photo is already ≤ 2560 px (~20 MB decoded). For a complete fix: a Windows-only `ImageHandler`
mapping that sets `BitmapImage.DecodePixelWidth` from `WidthRequest`, or a small thumbnail file
`attachments/thumbs/<id>.jpg` (not synced).

### R-3. The whole sync snapshot in memory (medium)

`SyncEngine.WriteSnapshotAsync` writes all tables as JSON into a `MemoryStream`, then `ToArray()` → `Gzip` (another
`MemoryStream` + `ToArray()`) → `new MemoryStream(bytes)` for the upload. Four copies, all on the Large Object Heap. The
same in `Gunzip` on receive. 20 years of data mean a peak of several tens of MB on the phone (measure the exact size).

**Fix:** a streaming chain `Utf8JsonWriter` → `GZipStream` → temporary `FileStream`, then upload from the file.

```csharp
var temp = Path.Combine(FileSystem.CacheDirectory, $"snapshot-{Guid.NewGuid():N}.json.gz");
try
{
    await using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
    await using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
    using (var writer = SyncFormat.NewWriter(gzip))
    {
        WriteSnapshot(writer, connection); // rows are streamed, never buffered whole
    }

    await using var upload = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
    await _storage.UploadAsync(path, upload, ct);
}
finally
{
    File.Delete(temp); // the temp file never outlives the upload
}
```

### R-4, R-5. Disposal and cancellation (low)

| ID | Where | What is wrong | Fix |
| --- | --- | --- | --- |
| R-4 | `ReportsViewModel.cs:546` | The previous `_buildCancellation` is cancelled but not disposed; the last one is not disposed when the page is left | `older?.Cancel(); older?.Dispose();` as in `ViewModelBase.BeginLoad`; cancel in `OnDisappearingAsync` |
| R-4 | `SyncScheduler` (debounce) | The old `_debounce` is cancelled without `Dispose` | Same pattern; dispose the current one in `SyncScheduler.Dispose()` |
| R-5 | `EditorPageBase.cs:46` | `_ = fresh.LoadAsync(id)`: the exception is unobserved, the editor stays empty without a message | Wrap in `RunSafeAsync` (standard error dialog + log) |
| R-5 | `DialogService.CloseModalAsync` | If `PopModalAsync` throws, `MarkClosed()` is never called: `ShowEditorAsync` waits forever and keeps the modal page and view model alive | `try { await PopModalAsync(); } finally { modal.MarkClosed(); }` |

Blocking `.GetAwaiter().GetResult()` calls exist in only three places (startup restore, the copy before a restore,
building the database on JSON import). All are documented and go through `Task.Run` — no deadlock. Network: one
singleton `HttpClient` with a 30 s timeout — correct.

## What is already done right — keep it

Treat these patterns as the reference and repeat them in new code.

| Pattern | Where |
| --- | --- |
| Weak messenger for global events (language, layout mode) — view models never need to unsubscribe | `ViewModelBase`, `LayoutModeService` |
| Latest-wins loading: a new load cancels and disposes the old CTS, leaving the page cancels the load | `ViewModelBase.BeginLoad / RunLoadAsync` |
| Lists read the database on a pool thread; the UI thread only assigns | `OffUiAsync` in 14 view models |
| `ReloadGate` compares the data version instead of subscribing to an event | `ReloadGate`, `IDataChangeNotifier.Version` |
| Subscription to a singleton only while the page is on screen, marshalled to the UI thread | `SyncSettingsViewModel`, `SettingsSectionViewModel` |
| Native views held through `WeakReference` | `FieldBorder` |
| Static handler + `RemoveHandler` before `AddHandler` | `WheelForwardingWindows` |
| Theme subscription in `Loaded`, unsubscription in `Unloaded` | `OverviewChartView`, `ReportChartsView` |
| Sync retry timer exists only in the foreground | `SyncScheduler.SetForeground` |
| EF context per operation, `AsNoTracking`, `AsSplitQuery`, WAL | `HomeUtilities.Data` |
| Paging of long lists ("Show more", the 6 latest bills in a contract) | `ListPaging`, `ContractEditViewModel.Bills` |
| Heavy report and chart calculation in `Task.Run`, parallel builds serialized with a `SemaphoreSlim` | `ReportsViewModel` |
| Log of slow operations > 300 ms in Release | `PerformanceLog` |

## Deferred — do not touch

These findings concern UI speed and smoothness. By the owner's decision of 2026-11-09 they are not fixed: the current
speed is satisfactory, and T-039 already made the app feel worse. Listed for information only, in case a concrete
complaint ever comes up.

| ID | Observation | Where |
| --- | --- | --- |
| P-1 | 12 editors read and save SQLite on the UI thread (`Microsoft.Data.Sqlite` has no real async). "Open empty and fill in" (T-039) and "load first, then open" were both rejected by the owner. | `EditorPageBase`, `*EditViewModel` |
| P-2 | The report table builds 3–5 views per cell without virtualization (~10–18k for 20 years by month). | `ReportTableView.cs` |
| P-3 | Help parses 250 KB of Markdown and ~450 KB of SVG again on every open. | `HelpContentService` |
| P-4 | Charges screen: 7 nested `BindableLayout`s in a `ScrollView`, scrolling to a card via `Task.Delay(200)`. | `ChargesPage` |
| P-5 | `AdaptiveFieldGrid` re-arranges on every added field. | `AdaptiveFieldGrid.cs` |
| P-6 | Colour converter in list templates. | `BillsPage.xaml` and others |
| P-7 | No compiled EF Core model — the first query builds the model. | `HomeUtilities.Data` |
| M-7 | Closed view models receive weak-messenger messages until garbage collection (extra work, not a leak). | `ViewModelBase` |
| M-8 | `FieldBorder.Apply` runs twice per field and creates 3 drawables each time (memory handling is correct). | `Platforms/Android/FieldBorder.cs` |

## Rollout and verification

Four independent steps, one PR each, tested by the owner on a Release build. Rule for every step: threading, load order
and UI layout do not change. If the owner notices any difference in how the app feels, the whole step is reverted.

| Step | Content | Findings | What the owner sees | How to verify |
| --- | --- | --- | --- | --- |
| 1 | Unsubscriptions, token disposal, error handling + leak tests | M-1, M-3…M-6, R-4, R-5 | nothing; if an editor fails to load — the standard error dialog instead of an empty page | unit tests green; the sync hint on Overview works; snackbar and reading entry as before |
| 2 | Photo shrinking via `MediaPickerOptions`, `ImageProcessor` fix, Windows thumbnail | R-1, R-2 | new photos take less space | file in `attachments` ≤ ~600 KB on both platforms; photo not rotated wrongly; JPEG, not PNG |
| 3 | Streaming sync snapshot through a temporary file | R-3 | nothing | PC ↔ phone sync as before; "Replace the cloud" works; no files left in the temp folder |
| 4 | Verify M-2; fix only if the defect is confirmed | M-2 | nothing | Android, "Don't keep activities": leave and return to the app 5 times, error log empty |

### Leak test (step 1)

Template for `HomeUtilities.ViewModels.Tests`: the view model is created in a separate method, the singleton is held
by the test, after garbage collection the view model must be gone.

```csharp
[Fact]
public async Task Overview_IsCollected_AfterDisappearing_WhileSyncEngineLives()
{
    var engine = new FakeSyncEngine(); // singleton stand-in, kept alive by the test
    var weak = await CreateShowAndHideAsync(engine);

    for (var i = 0; i < 3 && weak.IsAlive; i++)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    Assert.False(weak.IsAlive, "The singleton engine must not keep the view model alive.");
    GC.KeepAlive(engine);
}

// Separate method so no local in the test keeps a strong reference.
[MethodImpl(MethodImplOptions.NoInlining)]
private static async Task<WeakReference> CreateShowAndHideAsync(FakeSyncEngine engine)
{
    var vm = OverviewViewModelFactory.Create(sync: engine);
    await vm.OnAppearingAsync();
    await vm.OnDisappearingAsync();
    return new WeakReference(vm);
}
```

### How to measure

- **Android memory:** `adb shell dumpsys meminfo <package>` (Java Heap, Native Heap, Graphics) before and after a
  scenario: 20 editor opens, 5 photos, a sync. Growth after repeating the scenario and a GC = a leak.
- **Windows memory:** Task Manager (private working set) or Visual Studio → Diagnostic Tools → two heap snapshots
  before / after the scenario; look for `*Page` and `*ViewModel` types with a growing instance count.
- **Feel:** at every step the owner confirms that the app works the same as before.

## Lead notes (2026-11-09, after reading the audit with the owner)

| ID | Decision |
| --- | --- |
| M-2 | **Checked by the owner, not reproduced**: Android "Don't keep activities" on, left and returned to the app on Overview, Bills, an open editor and an open choice dialog several times — the window is rebuilt, everything works, the error log holds only `Performance:` lines. `AppShell` stays a singleton; no step 4. |
| M-1, M-3…M-6, R-4, R-5 | **T-041**. R-5 also in `ReadingHistoryPage.xaml.cs:34` and `EqualizedSettlementPage.xaml.cs:31` (same `_ = LoadAsync(id)`). M-1 preferably through `WeakReferenceMessenger` (the engine's status as a message), so the hint does not depend on `OnAppearing`. M-3: an unsubscription in `Unloaded` needs the matching subscription in `Loaded` — Shell tab switches raise `Unloaded`, and a host that does not subscribe again loses every snackbar (the #220 bug). |
| R-1, R-2 | **T-042**, but **without** switching to `MediaPickerOptions`: on Windows that replaces the `FilePicker` dialog (a visible change), and the PNG claim rests on a third-party blog. Keep `ImageProcessor` as the single place: Windows implementation with `Windows.Graphics.Imaging`, Android with less memory. |
| R-3 | **Low**, deferred: one household's snapshot is a few MB of JSON. The sample uses `FileSystem.CacheDirectory`, which `HomeUtilities.Data` must not use (no MAUI there) — a later task gets the temporary folder through an interface. |
| New | `Performance: Startup to Overview 3987402 ms` (owner's log 2026-11-09 08:15Z): the time is measured from the **process** start, and Android started the process in the background about an hour earlier (e.g. a reminder alarm) without a window. Fixed in T-041. |
| Speed | Owner (2026-11-09): the current speed is fine. Overview takes ~1 s to load on the phone (`OverviewViewModel 1016 ms`), the cards fill in without a freeze — **not touched**; the number is kept for comparison. |
