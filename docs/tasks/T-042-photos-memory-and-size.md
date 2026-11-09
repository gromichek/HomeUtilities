# T-042: Photos — shrink on Windows, less memory on Android, small thumbnails on Windows
Phase: 9 · Depends on: T-041 (start after the owner tested it) · Size: S–M · Source: `docs/PERF_AUDIT_2026-11-09.md`
R-1, R-2 and its "Lead notes"

Owner (2026-11-09): only clear defects, nothing that changes how the app feels. Photos picked on Windows are stored at full
size (several MB each: database folder, backups and cloud grow), shrinking a 12 MP photo on Android peaks at ~100 MB
(risk on the tablet), and a 72×72 thumbnail on Windows decodes the whole photo (~48 MB for 12 MP).

## Scope — do

1. **`ImageProcessor` stays the single place** (`IImageProcessor`, called by `AttachmentStore`). Do **not** switch to
   `MediaPickerOptions` and do not change the pickers: on Windows that would replace the `FilePicker` dialog the owner uses.
2. **Windows implementation** with the same rules as Android (`MaxSide` 2560 on the long side, `JpegQuality`, EXIF
   orientation applied, smaller photos and non-JPEG/PNG files unchanged) using `Windows.Graphics.Imaging`
   (`BitmapDecoder` + `BitmapTransform` with scaled size and `ExifOrientationMode.RespectExifOrientation`, `BitmapEncoder`
   JPEG with `ImageQuality`) — **no package**. Runs off the UI thread like today on Android. The size rule (target width /
   height from source size and max side) in shared code, unit-tested. The reverted T-039 commit `ee97804`
   (`WindowsImageResizer`, `ImageSizing`) may be reused after review — it was not the cause of the jerkiness.
3. **Android with less memory** (`ImageProcessor.Shrink`): no extra `ToArray()` copy (`TryGetBuffer` / read once);
   decode closer to the target size (sample size and/or `InDensity` / `InTargetDensity` scaling while decoding); rotation and
   scaling in one `Matrix` / one `CreateBitmap`; `Recycle()` intermediate bitmaps. Same output rules as today; the photo
   must not come out rotated wrongly. State the estimated peak before / after for a 4000×3000 photo in the PR.
4. **Windows thumbnails decode at thumbnail size** (`StreamFactoryImageConverter`, `ReadingsPage` 72×72 and every other
   thumbnail of an attachment): a Windows-only `Image` handler mapping setting `BitmapImage.DecodePixelWidth` from the
   requested size, or another simple way — say which and why. Android unchanged (Glide already downsamples). Full-size
   view (opening the photo) unchanged.
5. Existing large photos are **not** converted (no migration); only new ones are shrunk.
6. User guide EN + RU: the meter photo and bill document texts now say that photos are shrunk on Android **and** Windows
   (T-040 wrote "on Windows the file is kept as it is").

## Not in this task
No picker / dialog change, no migration of stored photos, no change of the photo viewer, no sync snapshot work (R-3).

## Tests
Size rule with concrete numbers: 4000×3000 → 2560×1920; 3000×4000 → 1920×2560; 2000×1500 → unchanged; 2561×10 →
2560×10 (minimum 1 px). Orientation values → rotation (if shared). Existing attachment tests stay green.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (the Windows code is built only there — no CA1416).
Owner: a new photo on Windows and on the phone is ≤ ~600 KB, upright, JPEG; reading thumbnails on Windows as before; the
app feels the same.

## Manual test steps for the owner (short)
1. Windows: add a large photo (several MB) to a reading or a meter → its file in
   `%LocalAppData%\HomeUtilities.App\HomeUtilities.App\Data\attachments` is a few hundred KB; open it — upright, sharp.
2. Phone: take a portrait photo for a reading → upright; also pick one from the gallery.
3. Windows: Readings with several photos — thumbnails show; Task Manager memory of HomeUtilities before / after (optional,
   write the numbers).
