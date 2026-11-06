# T-037a: Database clean-up fixes — attachments of deleted records, counts, sizes
Phase: 9 · Depends on: T-037 (#248) · Size: S

Owner's test of T-037 (2026-11-06, findings T-037/1–4 in `docs/PROGRESS.md`): one bill with a photo was deleted; the
dialog said "Удалённых записей: 2 · файлов удалённых фото и документов: 0 (1 КБ)", the result "Удалено: записей 1, файлов
0 (1 КБ)" — the photo was not removed, the counts differ and nothing explained the difference.

## Scope — do
1. **Attachments follow their owner (T-037/1).** Deleting a record that can own attachments (bill, payment, reading,
   meter, contract, property, supplier — every `AttachmentOwnerKind`) soft-deletes its attachments in the same save.
   Restoring the owner (Undo, T-036b) restores them — provide one store/helper method both can use. The clean-up treats
   attachment rows whose owner is deleted **or missing** as deleted (existing data), so their rows and files are purged.
2. **Same rows in preview and purge (T-037/4).** Find why the preview counted 2 and the purge removed 1 without a "kept"
   line; reproduce with one bill (one line, one photo) deleted. Preview and result must count the same rows; a kept row is
   always reported.
3. **User-level counts (T-037/2).** Dialog and result by kind, only kinds with a count: e.g. "Счета: 1 · строки счетов: 1
   · вложения: 1 (фото и документы: 1, 2,3 МБ)". Kind names in resx (EN + RU).
4. **Sizes (T-037/3).** No size when there are no files; no "minimum 1 KB" for zero. Database size before → after stays.
5. User guide EN + RU: the clean-up text matches the new dialog.

## Tests (Data / ViewModels)
Deleting a bill with a photo soft-deletes the attachment; Undo/restore helper restores both; clean-up of a bill with one
line and one photo: preview = result = bill 1, line 1, attachment 1, file 1; attachment whose owner row is missing is
purged; zero files → no size text; kept row reported in preview and result.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green. Owner: delete a bill with a photo → clean-up
dialog shows "Счета: 1 · строки счетов: 1 · вложения: 1" and the photo file; the result shows the same and the file is gone.

## Manual test steps for the owner
1. Add a bill with one line and a photo, delete it.
2. Settings → Backup → "Очистить базу данных…": the dialog lists the bill, its line and the attachment with the file size.
3. Confirm: the result shows the same counts; run again → "Очищать нечего".
