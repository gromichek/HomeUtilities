# T-039: Performance and memory — database work off the UI thread in editors, photo memory, small leaks
Phase: 9 · Depends on: T-038 (#267, #269) · Size: M

Owner (2026-11-07): the app is now filled with real data and used daily; "analyse the code for memory leaks and for the
places that can make the UI stutter — optimise the app for good". The lead's audit (below) found one big and a few small
causes. The event-subscription hygiene is mostly good (Snackbar host, ChoiceField, ListColumns, Readings window, sync
settings and Android field borders all unsubscribe or use weak references) — do not rewrite it.

## Findings → scope — do

### A. Editors do all database work on the UI thread (main cause of stutter)
Microsoft.Data.Sqlite has **no real asynchronous I/O**: `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync` run
synchronously on the calling thread. List pages already use `OffUiAsync` (`ViewModelBase`), but these view models await the
stores directly on the UI thread — 1 to 13 store calls when an editor opens, plus the save:
`BillEditViewModel` (+ `.Parts`), `BillTemplateEditViewModel`, `ContractEditViewModel` (+ `.Balance`, `.Bills`,
`.Equalized`, `.Tax`), `MeterEditViewModel`, `TariffComponentEditViewModel`, `PaymentEditViewModel`,
`PropertyEditViewModel`, `SupplierEditViewModel`, `CategoryEditViewModel`, `VatRateEditViewModel`,
`ReplaceMeterWizardViewModel`, `EqualizedSettlementViewModel`.
1. Move every store / database call of these view models off the UI thread (`OffUiAsync` or one helper per load and per
   save), keeping the existing order: read the data off the UI thread, then set the observable properties on the UI
   thread (no `ObservableCollection` change and no property raised from a worker thread). One load pass per editor open
   (group the reads), not one hop per property.
2. Saves and deletes (including Undo, T-036b) off the UI thread, with the Save button disabled while saving (no double
   save) and the existing error handling.
3. Editors log slow opens and saves like the lists: `PerformanceLog` lines `Performance: <VM> open … ms` / `save … ms`
   (threshold 300 ms), so the owner sees them in the error log.
4. A test helper or a guard that fails a unit test when a store is called on the "UI" thread would be ideal; at least
   tests that the editors still load and save the same data (existing tests stay green).

### B. Photos use too much memory on Windows
1. Thumbnails: `StreamFactoryImageConverter` gives the image the **full-size** photo. Android (Glide) downsamples to the
   view, WinUI decodes the whole bitmap (a 12 MP photo ≈ 48 MB per thumbnail). Decode thumbnails at thumbnail size on
   Windows (e.g. a Windows-only image handler mapping setting `DecodePixelWidth`, or generating a small cached thumbnail
   file next to the attachment, `attachments/thumbs/<id>.jpg`, never synced). Choose the simpler robust way and say why.
2. `ImageProcessor` shrinks photos only on Android (known issue): add the Windows implementation with the same rules
   (longest side, JPEG quality, EXIF orientation) using the Windows imaging API (`Windows.Graphics.Imaging`, no package).

### C. Layout work when an editor opens
`AdaptiveFieldGrid` arranges all children again on every `ChildAdded` (and on every child's `IsVisible` change): an editor
with 20 fields arranges 20 times while it is built. Coalesce: mark dirty and arrange once per dispatcher turn.

### D. Small leaks and hygiene
1. `OverviewViewModel` subscribes to the singleton sync engine with a lambda and never unsubscribes: unsubscribe on
   dispose / when the page goes away, or use a weak subscription, so a re-created Overview never stays alive.
2. `ReportsViewModel`: the previous build's `CancellationTokenSource` is cancelled but not disposed — dispose it.
3. Check the editor dialogs (`EditorDialogPage`, `DialogService`) do not keep the last page / view model alive after it
   closed (no static or singleton field holding it); fix if they do.

### E. Windows start-up
Release publish for Windows with `PublishReadyToRun=true` (in `scripts/publish-release.ps1` / the Windows publish
properties): less JIT at start. Measure the start time before / after on the owner's PC (step in the manual test).

## Not in this task
No new features, no change of the data loading of the list pages (already off the UI thread), no profiler packages.

## Tests
Editors load and save the same data as before (existing tests); new tests for the coalesced grid arrange (one arrange per
batch of children), the editor performance lines, and the Windows shrink rules where testable (sizes computed in shared
code).

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings, CI green. Owner: editors open and save without a visible
freeze on the phone; the error log shows no `Performance: … open/save` lines above 300 ms in normal use; memory of the
Windows app stays low with several photo thumbnails open.

## Manual test steps for the owner (short)
1. Phone, Release build: open a contract with many bills, a meter, a bill with lines — no freeze; save — no freeze.
2. Settings → error log: any `Performance:` lines for editors (open/save, ms).
3. Windows: open a reading or bill with 3–4 photos; Task Manager → HomeUtilities memory before / after (write the numbers).
4. Windows: add a large photo (several MB) → its file in `%LocalAppData%\HomeUtilities.App\HomeUtilities.App\Data\attachments`
   is small (a few hundred KB).
5. Windows: start-up time of the installed app before / after this release (stopwatch from the click to Overview).
