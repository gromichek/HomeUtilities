# T-014a: Robustness (no crashes on bad input), stay in editor after Save, Android layout fixes
Phase: 3 · Depends on: T-014 · Size: M

## Goal
Owner test 2026-10-23 found crashes on unexpected input and two Android UI problems. Make the app
**crash-proof against user input** on every existing screen, change the Save behaviour, and fix layout issues.

## Owner findings
1. Purpose template `Abon. Nr. {CustomerNumber}, {Period:mm.yyyy}` (lowercase `m`) **crashes** on Windows and
   Android. Cause: `DateOnly.ToString("mm…")` throws `FormatException` (`m` = minutes). "Handle abnormal input in
   all fields so the app never crashes."
2. Android, supplier editor: pressing **Save closes the editor**. The owner wants to **stay in the editor after
   Save**; the Back button is for leaving.
3. Android, category editor: the colour swatches do not fit and are cut off on the right.

## Scope — do

### 1. Purpose template (Core)
- `PaymentPurpose.Render` never throws: an invalid or unsupported `{Period:<format>}` is rendered **as typed**.
  Allowed format characters: `d`, `M`, `y` and separators `. - / space` (plus quoted literals not needed);
  anything else (e.g. `m`, `h`, `s`, `t`, `f`, `z`, `K`, `g`) → invalid.
- New `PaymentPurpose.Validate(string? template)` → errors `payment.purpose_invalid_placeholder` (unknown name)
  and `payment.purpose_invalid_format` (with the offending format in `Args`); `PaymentDetailsValidator` calls it.
  Editors show the error under the template field and the preview shows the template as typed.
- Tests: `{Period:mm.yyyy}`, `{Period:HH}`, `{Period:}` (default), `{Period:MM.yyyy}`, `{Foo}`, `{Period:M/yy}`,
  unbalanced braces — none throws; Validate returns the right codes.

### 2. Input hardening audit (ViewModels + Core)
Go through **every input field** of Properties, Parameters, Suppliers, Links, Payment details, Categories,
Contracts, Custom fields and make sure that any text the user can type (empty, spaces, letters in numbers,
huge numbers like `99999999999999999999999`, negative numbers, `,` and `.`, emoji, 5000-character strings,
leading/trailing spaces, invalid dates, day `0`/`32`, hex `#12`, `#GGGGGG`) results in a **validation message,
never an exception**. Specifically:
- `DecimalInput.TryParse` returns false on overflow (no `OverflowException`).
- Integer fields (days, sort order, interval months) parse with `int.TryParse`, range-check, overflow → false.
- All string fields respect the validator max lengths **in the UI** (`MaxLength` on `Entry`/`Editor`) so the
  user cannot type beyond them.
- Add a data-driven test class `InputHardeningTests` that feeds the list of nasty strings above into every
  editor view model's text properties and runs `SaveCommand` / preview logic: **no exception** in any case.

### 3. Global safety net
- `EditableViewModel` and all `[RelayCommand]` async handlers in view models: unexpected exceptions are caught in
  one place (`ViewModelBase.RunSafeAsync(Func<Task>)` or an `IAsyncRelayCommand` wrapper), logged, and shown with
  the standard error dialog (guidelines §5: title "Error"/"Ошибка", message "Something went wrong. Your data was
  not changed." / "Что-то пошло не так. Данные не изменены." + details in Debug builds). The app keeps running.
- App: subscribe to `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`,
  and on Windows `Microsoft.UI.Xaml.Application.UnhandledException` (set `Handled = true` where possible);
  write the exception to `logs/errors.log` in the app data folder (keep last 1 MB). Settings page: row
  **"Error log"** with "Copy log" (clipboard) and "Clear" so the owner can send it to us.

### 4. Save keeps the editor open (all editors) — guidelines change
- After a successful **Save**: stay in the editor, `IsDirty = false`, title updates (e.g. new → name), show a
  short confirmation where available (toast on Android; on Windows nothing until the in-app snackbar), the list
  (if visible) refreshes. **Back** leaves. After **Delete**: leave (unchanged).
- Applies to Property, Supplier, Category, Contract editors and the compact editor pages.
- Owner rule: on Android the toolbar **✓ only saves, never navigates**; **only Back leaves**. Check every page
  that has ✓ (incl. full-screen child-item dialogs on phones: there ✓ saves the item and closes the dialog —
  that is the one exception, because the dialog *is* the item editor; state it in the PR).
- Update tests that expected "close after save".

### 5. Android layout
- Category colour swatches: use a wrapping layout (`FlexLayout Wrap="Wrap"`, fixed swatch size 36–40 dp, spacing
  8) so all 12 fit on any width; same check for every other horizontal row of buttons/chips (copy buttons in
  payment details, footer buttons) — they must wrap or stack on Compact width, never be cut off.
- Contract page guard (T-014 open question): a layout-mode change while the contract page is open must not pop
  the contract page; only the property editor host changes.

## Scope — do NOT
- No new features. No in-app snackbar yet (T-019).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. No input in any field can crash the app (owner will try).
3. Save keeps the editor open on all editors; Back leaves (with the unsaved-changes dialog if needed).
4. Colour swatches fully visible on a phone in portrait.
5. Error log visible/copyable in Settings.

## Docs to update in the PR
None — the lead updates `UI_GUIDELINES.md` §4 (already changed: "after Save stay").

## Manual test steps for the owner
1. Supplier → purpose template `Abon. Nr. {CustomerNumber}, {Period:mm.yyyy}` → red message under the field,
   preview shows the text as typed, no crash. Fix to `MM` → preview correct.
2. Try nonsense in every field (letters in numbers, `99999999999999999999`, day 0 / 32, very long text) → only
   red messages, no crash.
3. Android: Supplier → change → **Save** → stays in the editor, title updated; Back → list.
4. Android: Category → all 12 colour circles visible.
5. Settings → Error log → Copy log works (may be empty).
