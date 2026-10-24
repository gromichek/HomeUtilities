# T-016a: Attachments infrastructure + meter / reading photos + submit links on meter cards
Phase: 3 · Depends on: T-016 · Size: M

## Goal
A reusable way to store files belonging to records (photos now, bill PDFs in T-019), and its first use: take a
photo of the meter dial when entering a reading (Android camera / gallery, Windows file picker) and view it later.

## Context
- `docs/DATA_MODEL.md` §10 (Attachment), §4 (`MeterReading.PhotoAttachmentId`), `docs/ARCHITECTURE.md` §7
  (attachments folder, file name = GUID + extension), §8 (sync later — attachments are synced as separate files).
- `docs/UI_GUIDELINES.md`; T-016 screens (`ReadingsViewModel`, `ReadingHistoryViewModel`).

## Scope — do

### 1. Core
- `Attachment : SyncEntity` — `FileName` (original name, ≤ 200), `ContentType`, `SizeBytes`, `Sha256` (hex),
  `OwnerKind` (enum: `MeterReading`, `Bill`, `Meter`, `Other`), `OwnerId`, `Notes`.
- `AttachmentValidator`: name/content type required, size > 0 and ≤ 20 MB, content type in an allow-list
  (`image/jpeg`, `image/png`, `image/heic`, `application/pdf`), Sha256 64 hex chars.
- `IAttachmentStore`: `AddAsync(Attachment meta, Stream content)` (copies the file into the attachments folder as
  `{Id}{ext}`, computes size + SHA-256, saves the row — file write and row in one logical operation: if the DB
  save fails the file is removed), `OpenReadAsync(Guid id)`, `GetAsync(Guid id)`, `ListByOwnerAsync(kind, id)`,
  `DeleteAsync(Guid id)` (soft delete row; **the file is kept** for undo/sync — cleanup later).

### 2. Data
- Entity configuration, `Attachments` table (index `OwnerKind, OwnerId`, `UpdatedAt`), migration
  `AddAttachments` (generated, not hand-written). `IAppPaths.AttachmentsFolder` (`AppData/attachments`).
- Optional downscale: images larger than 2560 px on the long side are re-encoded to JPEG quality 85 before
  saving (keep the original if decoding fails) — implement in the App layer behind `IImageProcessor` (no-op in tests).

### 3. Platform service
- `IMediaService` (ViewModels): `Task<PickedFile?> CapturePhotoAsync()`, `Task<PickedFile?> PickPhotoAsync()`,
  `Task<PickedFile?> PickFileAsync(string[] contentTypes)`; `PickedFile(string FileName, string ContentType,
  Func<Task<Stream>> OpenAsync)`. App implementation with `MediaPicker` / `FilePicker`; camera capture only where
  `MediaPicker.Default.IsCaptureSupported`. Android: camera permission request + `AndroidManifest` entries
  (`CAMERA`, `queries` for `IMAGE_CAPTURE`); declined permission → localized message, no crash.
- `IFileViewer.OpenAsync(string path, string contentType)` → `Launcher.OpenAsync(new OpenFileRequest(...))`
  (opens the photo/PDF in the system viewer).

### 4. UI
- Readings screen: each meter card gets a **camera** icon button (`IconPhoto` — add to the icon table:
  Material `photo_camera` / Segoe `E722`): Android → choose "Take photo" / "Choose from gallery"; Windows → file
  picker. The photo is attached to the reading that will be saved for that meter (if the meter has several
  registers, to the first saved register's reading); a small thumbnail appears on the card with a remove (×) icon.
  Saving readings saves the attachment rows (`OwnerKind = MeterReading`) and sets `PhotoAttachmentId`.
- Reading history: rows with a photo show a photo icon; tap → opens the photo in the system viewer. Edit dialog:
  add / replace / remove photo.

### 5. Submit-readings links on meter cards (owner request 2026-10-24)
"Wrote the readings, tap the link, submit them to the supplier right away."
- On each meter card of the Readings screen: one button per **distinct URL** of the meter's active linked contracts
  (`ContractMeter` active today): `IconOpenLink` + "Submit: {supplier name}". URL = the contract's effective link
  (`Contract.GetEffectiveLinks(supplier)`) of kind `SubmitReadings`, else `CustomerPortal`; none → no button.
  Hot water meter linked to water + heating contracts → up to two buttons; identical URLs → one button.
- Tapping opens the browser (`ILauncherService`). When the page appears again (`OnAppearingAsync`) and readings
  of that contract were saved in this session but are not yet marked submitted, ask
  "Mark readings for {supplier} as submitted?" (Yes / No) → same logic as the existing "Mark submitted".
- The contract banner's "Submit online" stays.
- Tests: buttons per contract, SubmitReadings preferred over CustomerPortal, contract link overrides supplier
  link, duplicates collapsed, no button without links, prompt only after a save and only once per contract.

## Scope — do NOT
- No bill PDFs yet (T-019 uses this infrastructure). No sync of attachments (T-032). No file cleanup job.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (Android manifest changes build).
2. Android: take a photo while entering a reading, save, open it from the history. Windows: pick an image file.
3. Denied camera permission → message, no crash. Huge/unsupported file → validation message.

## Tests required
- Core: `AttachmentValidatorTests`.
- Data: `AttachmentStoreTests` — file copied to `{Id}{ext}`, SHA-256 and size correct, row saved; DB failure →
  file removed; soft delete keeps the file; list by owner.
- ViewModels: readings — photo attached to the right reading, removed photo not saved, Save without photo unchanged;
  history — open/replace/remove photo; media service returning null (user cancelled) does nothing;
  permission-denied exception → error message (safe command), no crash.

## Manual test steps for the owner
1. Android phone: Readings → camera icon on a meter → Take photo → thumbnail on the card → enter value → Save all.
2. Meter history → photo icon on that reading → photo opens. Edit → remove photo → saved.
3. Deny camera permission once (Android settings) → friendly message.
4. Windows: camera icon → file picker → choose a JPG → saved → opens from history.
