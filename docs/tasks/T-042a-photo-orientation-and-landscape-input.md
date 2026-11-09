# T-042a: Android — photos stored upright, decimal input in landscape
Phase: 9 · Depends on: T-042 (#283) · Size: S · Owner test 2026-11-09

## Findings (owner, phone)
1. **Photos are stored lying on their side.** A portrait photo — camera **and** gallery — shows upright in the 72×72
   thumbnail under the meter on Readings (the original, before processing; Glide applies the EXIF orientation), but the
   stored file opened with the photo icon in the system viewer is rotated. Windows is correct (thumbnail and viewer).
   So the EXIF orientation is in the original and gets lost in `ImageProcessor.Shrink` (Android) — or the stored original
   keeps only the EXIF tag and the viewer, opened through our `FileProvider` URI, ignores it. Do not guess: find out which.
2. **Decimal separator lost in landscape.** Readings, portrait: typing `360,34` works. Turning the phone to landscape turns the
   value into `36034` at once, and in landscape the separator cannot be typed although the keyboard shows it. Most likely the
   full-screen landscape keyboard (extract mode, `ExtractEditText`) re-filters the text with a key listener that only knows
   `.` — to be confirmed.

## Scope — do
1. **Orientation in one place you can test.** Read the EXIF orientation with a small parser in Core (JPEG APP1 / TIFF IFD0,
   tag `0x0112`, both byte orders; no package), unit-tested with synthetic byte arrays, instead of
   `Android.Media.ExifInterface(Stream)`. Log once per photo when an orientation ≠ 1 is found but the rotation fails
   (error log, no personal data).
2. **Every stored JPEG is upright without EXIF** on Android: rotate the pixels whenever the orientation is ≠ 1 — also when the
   photo is already ≤ 2560 px (today such photos are stored unchanged and depend on the viewer honouring EXIF). Same JPEG
   quality; no rotation → behaviour unchanged. Windows unchanged (correct), but use the same Core parser there only if it
   is trivial; no Windows behaviour change.
3. **Landscape input.** Confirm the cause on the Android emulator (landscape with a hardware-less keyboard). Fix for every
   `Entry` / `Editor` of the app on Android: preferably `ImeFlags.NoExtractUi` in an `EntryHandler` / `EditorHandler`
   mapping (no full-screen keyboard in landscape; the field stays visible), or the key listener for the app culture if the
   cause is different. A value typed in portrait must stay `360,34` after rotating; `,` (and `.` where the culture uses it)
   can be typed in landscape. No change on Windows.
4. Already stored rotated photos are **not** converted (the owner re-takes them if needed).
5. User guide EN + RU only if a visible behaviour changes (landscape keyboard no longer full-screen: one line in the
   readings section is enough).

## Tests
EXIF parser: big-endian and little-endian TIFF headers with orientation 1, 3, 6, 8; no APP1; APP1 without orientation; a
truncated header → 1, no exception. Rotation selection for "≤ 2560 px with orientation 6" → rotate. Existing tests green.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green. PR says which cause was confirmed for both findings.

## Manual test steps for the owner (short)
1. Phone: Readings → photo → Camera, portrait → save the readings → open the photo with the photo icon: upright. Same with a
   portrait photo from the gallery.
2. Phone: type `360,34` in portrait, rotate to landscape → still `360,34`; in landscape type `12,5` → accepted.
