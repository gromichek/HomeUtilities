# UI guidelines

Status: revision 1, agreed with the owner on **2026-11-05** after the UI review (`docs/UI_REVIEW_2026-11-05.md`,
findings referred to as R-NN). Every UI task follows these rules. Markers **[NEW]** / **[CHANGED]** show what changed
against the first version (2026-10-20); **(owner 2026-11-05)** marks a reversed earlier owner decision.
The history of why a rule exists is in the appendix at the end.

## Change summary

| § | Change | Why |
|---|---|---|
| 1 | Material Icons on **both** platforms; no Segoe glyphs | §1 and §6a contradicted each other: image icons on Windows must use the bundled font, text icons used Segoe → two icon families on one screen (R-17). |
| 1 | `IconSave` and `IconPaid` get different glyphs; duplicate `IconHelp` row removed | On Windows both were `E73E` (same check mark). |
| 2 | ✓ in the toolbar *is* the primary action of an editor; filled style never means "selected" | Filled buttons appeared next to ✓ and as a toggle state (R-40). |
| 2 | Touch targets ≥ 48 dp on Android | Not written down → all styles use 40 (R-05). |
| 3 | Delete: **only** a red button at the end of the form | "⋮ menu **or** red button" produced both. |
| 3 | Phone sheet layout (top bar + fixed bottom bar) for **all** dialogs | Was only specified for choice dialogs → child-item dialogs have no bars (R-29). |
| 3 | Form content ≤ 600 dp wide in the compact layout | Phone landscape stretches fields to ~800 dp (R-27). |
| 3 | (owner 2026-11-05) Date-ordered lists (Bills, Payments) in one column | Columns read in a zig-zag; searching "last month" is harder. |
| 3 | (owner 2026-11-05) Compact-by-height only on Android | A short wide Windows window became a "phone". |
| 3 | Period switcher ‹ › visible in the collapsed filter summary | Most common filter action without expanding (R-50). |
| 3a | One choice field control; native `Picker` and action sheets not used for picking records; dialogs return Ids | Two field looks on one screen, no current-value mark, records matched by display text (R-03, R-04, R-06). |
| 3a | Segmented control allowed for 2–3 short options | The ban led to two plain buttons with the primary style as "selected" (R-40). |
| 5 | Undo snackbar instead of a confirmation for a single delete; clearer confirmation text; standard texts for empty, loading, saved | "Will be hidden" is unclear; Undo infrastructure exists. |
| 6 | One field look (bordered), read-only value pattern, unit suffix, `ReturnType=Next`, switch placement | Several patterns side by side (R-07, R-08, R-24). |
| 8 [NEW] | Colours, contrast, status colours | Toolbar icons 1.6 : 1; green category vs green "paid" (R-01, R-15). |
| 9 [NEW] | Numbers, dates, empty and approximate values | English months on Windows, mixed month abbreviations, 0,00 € for "not calculated" (R-02, R-11, R-35). |
| 10 [NEW] | Screen states: loading / empty / nothing found / error | Only Reports shows "Loading…" (R-52). |
| 11 [NEW] | Type scale | 12 and 13 mixed at random. |
| 7 | Checklist: phone landscape, large font, 48 dp, long values, states | Missing cases found in the review. |

---

## 1. Icons — one icon font on both platforms [CHANGED]
- **Android and Windows**: Google **Material Icons Outlined** (static font `MaterialIconsOutlined-Regular.otf`, ~330 KB,
  bundled in `Resources/Fonts/`, Apache 2.0, licence file next to it). **Do not** add the 10 MB variable Material
  Symbols font.
- **[CHANGED]** Segoe Fluent Icons / Segoe MDL2 are **no longer used**, also not for text icons. Reason: every
  `FontImageSource` on unpackaged Windows must use a bundled font file (§6a), so toolbar and button images were Material
  while text icons were Segoe — the same action looked different on one screen. One font removes the problem.
  Windows users read these icons without difficulty.
- One XAML resource per icon (`IconX` for text, `IconXSource` for images, same glyph), used everywhere; never raw glyph
  codes in pages. Arrows and chevrons are icons too — no `‹` `›` text characters.

| Key | Meaning | Material Icons glyph |
|---|---|---|
| `IconAdd` | add | `add` |
| `IconEdit` | edit | `edit` |
| `IconDelete` | delete (always red, §2) | `delete` |
| `IconSave` | save (toolbar) | `check` |
| `IconClose` | close / cancel | `close` |
| `IconBack` | back | system back arrow |
| `IconMore` | more actions | `more_vert` |
| `IconOpenLink` | open an **external** web page only | `open_in_new` |
| `IconChevron` | go to another page **inside** the app | `chevron_right` |
| `IconPrevious` / `IconNext` | previous / next period | `chevron_left` / `chevron_right` |
| `IconCopy` | copy | `content_copy` |
| `IconArchive` | archive | `archive` |
| `IconPhoto` | photo / camera | `photo_camera` |
| `IconHelp` | help (toolbar "?" opening the user guide at the screen's section) | `help_outline` |
| `IconHistory` | history | `history` |
| `IconPaid` | mark as paid — **[CHANGED]** must differ from `IconSave` | `task_alt` |
| `IconTodoReadings` / `Due` / `Missing` / `Verification` / `Settlement` | Overview to-do kinds | `speed` / `event` / `note_add` / `build` / `balance` |
| `IconCheckOn` / `IconCheckOff` / `IconRadioOn` / `IconRadioOff` | marks in choice dialogs (§3a) | `check_box` / `check_box_outline_blank` / `radio_button_checked` / `radio_button_unchecked` |
| `IconExpand` / `IconCollapse` | expand / collapse | `expand_more` / `expand_less` |
| `IconWarning` [NEW] | warning next to a status text | `warning_amber` |

Icon-only buttons always have a tooltip (`ToolTipProperties.Text`) and an accessibility name
(`SemanticProperties.Description`) — localized.

## 2. Buttons [CHANGED]
- **Primary action — one per screen or dialog.**
  - In an **editor page** the primary action is the **✓ Save in the toolbar**. No filled button inside the form.
    **[CHANGED]** ✓ is disabled (or visibly dimmed) while there are no changes.
  - On a page **without** a Save toolbar item, the main action may be one filled button (`PrimaryButton`), e.g.
    "Save all" on Readings, "Sync now" on Sync. Prefer a fixed bar at the bottom for it.
  - In a dialog: the OK / Save button.
  - **[NEW]** The filled style is never used to mark a *selected* state (see segmented control, §3a).
- **Secondary** (outlined) and **text** buttons: other actions.
- **Destructive**: red text, red outline or red trash icon (`DestructiveButton`, `DestructiveIconButton`). **Every**
  delete icon is red — also in rows of child lists. Always placed apart from Save.
- **[NEW] Touch targets:** on Android every tappable element is at least **48 × 48 dp** (buttons, icon buttons,
  ‹ › arrows, chips, whole list rows). The visible icon may stay 20–24 dp; the hit area grows
  (`MinimumHeightRequest` / `MinimumWidthRequest` via `OnPlatform`). Windows may keep 40.
- Styles `PrimaryButton`, `SecondaryButton`, `TextButton`, `DestructiveButton`, `IconButton`, `DestructiveIconButton`,
  `FabButton` in `Styles.xaml`; no ad-hoc colours. **[NEW]** `TextButton` has no horizontal padding, so its text
  lines up with the labels above it.
- **[NEW] Button texts are verbs** that name the action: "Отметить оплаченным", not "Оплачено" (which reads like a
  status and equals the label "Оплачено 8,31 €").

## 3. Layout of lists, editors and dialogs

### 3.1 One content pane
- **One content pane on every device** (owner decision 2026-11-01): no list + editor split. The list fills the content
  area; tapping a card pushes the **editor page**. The menu (flyout) is the only other pane.
- **List columns.** Catalog-like lists (Properties, Suppliers, Categories, Tariffs) use 1–3 columns of cards
  (`ListColumns.MinItemWidth="360"`).
  **[CHANGED] (owner 2026-11-05)** Lists ordered by date (Bills, Payments, reading history) stay **one column**, centred, max
  width ~760 dp; on wide windows the extra width is used *inside* the row (more fields), not for more columns.
- **List cards** have the card background (`Card` colour) and the outline, like section cards — never transparent on
  the page background.
- **Add.** Phone: a **floating action button** with `IconAdd` bottom-right. Tablet/desktop: a button above the list.
  Only one add entry point per layout. **[NEW]** The list keeps an empty footer of **88 dp** on phones so the last
  card can be scrolled clear of the FAB.
- Back from an editor keeps the scroll position and highlights the card opened last.

### 3.2 Filters above long lists
- Filter blocks (Bills, Payments) are the list **header** (they scroll with the cards), collapse to one summary line on
  compact width (state remembered per screen for the session) and show 2 / 3 columns of fields on medium / expanded.
- **[NEW]** The collapsed summary line contains the **‹ period ›** switcher itself, so the most common action
  (previous / next month or year) needs no expanding. Example: `‹ октябрь 2026 ›  ·  все объекты  ·  осталось 0,00 €`.
- The period (All years / Year / Month) is a choice field followed by the ‹ value › switcher; the year never wraps.
- **[CHANGED]** The ‹ › arrows sit **directly next to** the value (a centred `Auto,Auto,Auto` group), not at the edges of
  the card.

### 3.3 Editor pages
- Grouped into **cards/sections** with a header; a section that contains a list (parameters, links, lines, readings…)
  has its own `IconAdd` button **in the section header**.
- **Editor commands (every device):** toolbar has **Back** + **Save** (`IconSave` ✓). **✓ only saves and never
  leaves the page; only Back leaves** (owner rule 2026-10-23). After Save a short "Saved" snackbar. No separate
  "Cancel" — Back cancels.
- **[CHANGED] Delete** is always a red **"Удалить …"** button at the **end of the form** (full-row, natural width). It is
  not put into the ⋮ menu. (The ⋮ menu is for rare secondary commands: export, duplicate, history.)
- **Wide windows:** the cards flow into up to 2 columns (`AdaptiveFieldGrid MinColumnWidth="560" MaxColumns="2"`).
  Buttons and notes that are direct children of that grid get `views:AdaptiveFieldGrid.FullRow="True"`.
  **[NEW]** Cards in a row align to the top (`VerticalOptions="Start"`) — a short card is not stretched to the height
  of its neighbour.
- **[NEW] Compact layout (phone portrait and landscape):** form content is at most **600 dp** wide and centred; fields
  are never stretched to the full width of a landscape phone.
- **[NEW] Order of sections:** the section the user needs most often comes right after the identifying fields. For a
  bill: General → Amounts → **Payment** → the rest. An action needed often (mark as paid) is also offered on the list
  card.
- On compact width **every stacked action button inside a card has full width** (`HorizontalOptions=Fill`).
  Exception: the full-row "Delete …" / "Save as template" buttons under the editor cards keep their natural width.
- **Stacked buttons have the same width:** buttons that do not fit in a row go into an `AdaptiveFieldGrid`
  (`MinColumnWidth` = the width each needs). Never stack buttons with `HorizontalOptions="Start"`.
  **[NEW]** Two buttons side by side on compact width only if *both* full texts fit at 360 dp in Russian; otherwise
  stack them. A button text is never shortened by truncation.
- **Child rows:** tap the row → edit (dialog); red `IconDelete` at the end of the row. No separate "Edit"/"Remove"
  text buttons.

### 3.4 Dialogs [CHANGED]
- **Adding/editing a child item opens a dialog** with its own title ("New parameter", "Edit parameter") and its own
  Save / Cancel — never an inline form inside the parent form.
- **[CHANGED] One dialog frame for every dialog** (child-item editors, choice dialogs, wizards):
  - **Phone (compact):** full-screen sheet; the **title in a top bar** (with the theme's toolbar colour and status bar),
    the content scrolls in the middle, **Cancel + Save/OK in a fixed bottom bar** that stays visible above the
    keyboard. Back = Cancel.
  - **Tablet / desktop:** popup of fixed width (~420 for choices, ~560 for editors), content scrolls inside, max height
    ~70 % (wizards 90 %) of the window, buttons bottom-right.
  - Implemented once in `EditorDialogPage` / the popup host, not per dialog.
- **[NEW] Context in the title:** a dialog about a part of a record names the record: "Показание · Холодная вода,
  ванная", not only "Изменить показание".

### 3.5 Window size classes
- **Low windows are phones** (owner decision 2026-11-01, T-028a): a window lower than 480 dp (a phone in landscape,
  ~900 × 400 dp) is `Compact` whatever its width.
  **[CHANGED] (owner 2026-11-05)** This rule applies on **Android only**. On Windows the size class follows the **width**; a short
  wide desktop window keeps the desktop layout (button above the list, locked menu).
- No artificial maximum width of the window (space is needed for charts); width is used by columns or by the content
  of a row, not by stretching single fields.

## 3a. Choices [CHANGED]
- **[CHANGED] One choice field control** for every "pick one of a list" field in the app: caption above, current value
  in a bordered box with `IconExpand`, a tap opens the choice dialog (today's `ReportFilterField`; rename it to
  `ChoiceField`). The value wraps to at most 2 lines; it is never cut to one line.
- **[CHANGED] The native `Picker` is not used** (on Android it shows no current-value mark and looks like a text field).
  **`DisplayActionSheet` is used only to pick a *command*** (e.g. "Camera / Gallery"), never to pick a record or an
  option.
- **[NEW] Choice dialogs return the Id** (or index) of the chosen item, never its display text — two records may have
  the same name (two contracts "Газ" after a change of supplier).
- One look for every choice dialog: title top-left, content **directly under the title**, **Cancel** + **OK** at the
  bottom (frame per §3.4). Rows are list rows: whole row tappable, height ≥ 48, the radio / check mark **directly
  before** the text, optional colour dot between mark and text, text left-aligned. **The current value is marked when
  the dialog opens.**
- Single choice: radio rows. Extra inputs of an option (e.g. "Custom…" → from/to month) appear under that row.
- Multi choice: **All / None** as text buttons under the title, then the check rows.
- An entry that creates a new record ("＋ Новый поставщик…") is the **last row**, visually separated, with `IconAdd`
  instead of a radio mark.
- **[NEW] Segmented control** for 2–3 short mutually exclusive options that switch a *view* (Table / Chart): one
  bordered group of equal columns, the selected segment tinted (`Highlight`) with a check icon — not the filled primary
  style. Labels ≤ 12 characters; otherwise use a choice field.
- **Rows of separate toggle buttons are not used.**
- No horizontal scrolling inside dialogs; nothing may be cut off ("Всё вр…").

## 4. Navigation and Back
- Back (Android hardware/gesture back, toolbar back arrow, Windows back) **must always work**:
  - no unsaved changes → go back immediately;
  - unsaved changes → the standard "unsaved changes" dialog (§5); after Save / Don't save → go back;
  - after a successful **Save → stay in the editor** (title updates, "Saved" snackbar); after **Delete → go back**
    and show the Undo snackbar (§5).
- Never swallow Back without showing a dialog.
- The **system Back** (incl. predictive back on Android 16) takes the same path as the toolbar arrow: dialog →
  Cancel; pushed page / editor → the unsaved-changes dialog if needed, then up; section root → Overview;
  Overview → the app goes to the background. Implemented in `MainActivity` + `PageBase.OnBackButtonPressed`.
- **Menu (flyout) items always open the section's root page**; leaving any page with unsaved changes goes through the
  central navigation guard (T-017a) with the standard "Save changes?" dialog. The menu is never disabled.
- **[NEW]** A row or card that opens another page shows `IconChevron` at its end; the whole row is the tap target.
  Rows without a chevron are not tappable (or open a dialog).

## 5. Standard texts (EN / RU) [CHANGED]
Use these wordings everywhere (resx keys in brackets).

### 5.1 Dialogs
| Situation | Title | Message | Buttons |
|---|---|---|---|
| Leaving with unsaved changes [`Dialog_Unsaved_*`] | Save changes? / Сохранить изменения? | — | **Save** / Сохранить · **Don't save** / Не сохранять · **Cancel** / Отмена |
| **[CHANGED]** Delete a record that has dependent records [`Dialog_Delete_*`] | Delete «{0}»? / Удалить «{0}»? | {1} will be deleted with it. / Вместе с ним будут удалены: {1}. | **Delete** / Удалить · **Cancel** / Отмена |
| Error | Error / Ошибка | {0} | **OK** |

- **[CHANGED] Deleting a single record or a child row** needs **no confirmation dialog**: it is deleted at once (soft
  delete) and the snackbar shows **"«{0}» удалено · Отменить"** / **"«{0}» deleted · Undo"** for ~6 s
  (`ISnackbarService.ShowAsync` with action). Reason: the old message "Запись будет скрыта, история сохранится" did not
  tell the user where the record went or how to get it back; Undo does.
  **(owner 2026-11-05)** The snackbar must be easy to hit with a finger: the bar is at least **56 dp** high on Android,
  the "Отменить" action is a button of at least **48 × 48 dp** (in practice wider: the whole right part of the bar), and
  it stays at the bottom above the FAB. **Undo restores exactly** what the delete changed (the record and every row the
  delete touched, e.g. released payment allocations); where that cannot be guaranteed, the confirmation dialog with the
  list of dependent records is used instead.
- "Cancel" in a dialog always means "close the dialog and do nothing". Words like "Discard", "Continue", "Отбросить",
  "Продолжить" are not used.

### 5.2 Screen texts [NEW]
| Situation | RU pattern | EN pattern |
|---|---|---|
| Empty list | «{Чего} пока нет. Нажмите ＋, чтобы добавить.» | "No {things} yet. Tap ＋ to add one." |
| Filter finds nothing | «По выбранным фильтрам {чего} нет.» | "No {things} match the filters." |
| Loading | «Загрузка…» / «Расчёт…» (text, no spinner — owner 2026-10-29) | "Loading…" / "Calculating…" |
| Saved | «Сохранено» (snackbar) | "Saved" |
| Not calculated | «—» plus the reason in muted text | "—" plus the reason |

- Empty texts never say "the button below/above" — the position differs per platform; they name the ＋ instead.
- Messages say **what is missing and what to do**, in this order, and use the user's words (month, meter, contract),
  not internal ones (rate date, snapshot, rows).
- Quote button names exactly as they are written on the button.
- Technical logs shown to the user (sync details) are localized and short; file names and ids only in the error log.

## 6. Forms [CHANGED]
- **Label above the field**; validation message in red directly under the field; an optional hint in muted 12 under it.
- **[NEW] One field look:** text fields, number fields, date fields and choice fields all use the **bordered box**
  (outline colour, radius 8, padding 12 × 10) on both platforms. The Android underline of `Entry` / `DatePicker` is
  removed in the handler. Field text starts at the same x as its label.
- **[NEW] Read-only values** use the same pattern as fields — muted label above, value in normal text (not bold, no
  box). Several read-only values in a card all use this pattern; "Label: value" in one line only for a muted summary
  line.
- **[NEW] Units and currency** stand directly right of a number field ("м³", "€", "%"), not at the far edge of the card.
- **[NEW] Fast entry:** number fields use `Keyboard="Numeric"` and `ReturnType="Next"`; Next moves the focus to the next
  number field; on the last one it is `Done`.
- Numbers accept `,` and `.`; dates use the platform date picker, but the **displayed text** comes from the ViewModel
  (§9).
- **Nothing is cut off**: a switch / check box label is a `Grid Auto,*`, never a `HorizontalStackLayout`; several texts
  or buttons in a row use a `Grid` with `Auto,…,*` or equal `*` columns so that the texts wrap.
- **[NEW] Switches:** in a form, the switch is **left** of its label (`Grid Auto,*`); in a settings list where the row
  *is* the setting, the switch is **right** (`Grid *,Auto`). Within one card only one of the two. On colour =
  `Primary`.
- **[NEW] Hints are conditional:** a hint that only applies to some records (e.g. "hot-water meters are usually linked
  to two contracts") is shown only for those records.
- Read-only summary lines ("Now: 54.7 m² since 20.10.2026") in muted colour.

## 6a. Known MAUI pitfalls (found in owner tests)
- **Windows (unpackaged): every `FontImageSource` must use the font file bundled with the app** (`IconImageFontFamily` =
  MaterialIcons). A system font, a fallback list or no `FontFamily` gives a relative URI → "This operation is not
  supported for a relative URI": the image stays empty and a theme change crashed the app (#197, #199).
- **[NEW] `DatePicker.Format` on Windows ignores the app culture** — WinUI uses the Windows display language, so
  "MMMM yyyy" shows "November 2026" in the Russian UI. Never rely on `Format` for visible text; show the ViewModel's
  text and open the picker on tap.
- **[NEW] .NET's Russian abbreviated month names have different lengths** ("янв.", "февр.", "март", "июнь", "сент.").
  Use the app's own short month names (§9).
- **Windows: `Shell.ForegroundColor` also colours the NavigationView Back and ☰ buttons** (`MauiToolbar.UpdateIconColor`),
  which stand outside the toolbar on the window background — a light foreground made them invisible in the light theme
  (#244). On Windows only `Shell.ForegroundColor` / `TitleColor` are set, to the theme's **text** colour (`TextLight` /
  `TextDark`): unset, WinUI took the hover colours of Back from the system theme and Back vanished in the dark app theme (#245).
  Never a toolbar background colour on Windows.
- **A view that is shown later must have its `IsVisible` bound** (or set in code).
- `IsVisible` on an element whose `BindingContext` is changed by the same element is evaluated against the **new**
  context — wrap it in a `ContentView`.
- Bundled fonts need `<MauiFont Include="Resources\Fonts\*.otf;Resources\Fonts\*.ttf" />` in the App csproj.
- Selectable list items: give the `Border` an explicit base `Stroke` and `StrokeThickness="1"`.
- Decimal input: every number field parses through `DecimalInput`; values are stored as `decimal`, never as text.
- **Do not use `FlexLayout`** on Android: it clips the first item of each wrapped row. Use a `Grid`.
- **[NEW] Replacing `CollectionView.ItemsLayout` after the first layout** may be ignored on Windows — check that
  `ListColumns` really switches to 2–3 columns there (R-21).
- Do not use CommunityToolkit `Toast`/`Snackbar` — use `ISnackbarService`.

## 6b. Robustness
- No user input may crash the app: every parser uses `TryParse`, every text field has `MaxLength` matching the
  validator, format strings typed by the user are validated.
- Command handlers run through the shared safe wrapper (error dialog + log), never let exceptions escape.

## 7. Checklist for every UI task (put into the PR's manual test steps) [CHANGED]
1. Phone portrait **at 360 dp**, **phone landscape (~900 × 400 dp)**, tablet portrait, tablet landscape / wide window.
2. **[NEW]** Android **font size 130 %** (Settings → Display → Font size) — nothing cut, nothing overlapping.
3. Back from every page with and without unsaved changes.
4. Every choice field shows its value after reopening the item; the choice dialog marks the current value.
5. **Light and dark theme** readable; toolbar icons visible in both.
6. Russian and English texts fit (no cut-off buttons); rows of buttons stack on phones.
7. **[NEW]** Every tappable element ≥ 48 dp on Android.
8. **[NEW]** Long values: a name of 60 characters, an amount of 12 345,67 €, a supplier name with quotes.
9. **[NEW]** States: loading, empty list, nothing found, error — each seen once.
10. Nonsense input in every field → validation message, no crash.
11. `docs/user-guide/en.md` and `ru.md` updated for everything the user sees.

## 8. Colours and contrast [NEW]
- Colours only from `Colors.xaml` via styles / `AppThemeBinding`; no hard-coded colours.
- **Toolbar / status bar — Android:** accent blue (`ToolbarLight`) in the light theme, a **dark bar** (`ToolbarDark` #25282D) in the
  dark theme — the light-blue bar was too bright at night (owner 2026-11-05, C-17); icons in `OnToolbar*`. The colours are set in `AppShell.xaml.cs` **on Android only** (#244). **Windows keeps its default toolbar**; toolbar icons
  there use the accent colour (`OnPlatform` toolbar sources). The colours were set in `AppShell` (`Shell.BackgroundColor`, `Shell.ForegroundColor`,
  `Shell.TitleColor`) for both themes, and every toolbar icon uses the toolbar foreground colour (`OnPrimary` style
  sources, e.g. `IconSaveToolbarSource`) — never the accent colour, which is nearly invisible on the dark toolbar
  (1.6 : 1).
- **Contrast minimums** (WCAG AA): body text 4.5 : 1, large text and icons 3 : 1, outlines of input fields and cards
  3 : 1 against their background where the outline is the only boundary. Check new colours with a contrast tool.
- **Status colours are reserved:** green = paid / OK, amber = partial / due soon / warning, red = overdue / error,
  grey = neutral. Status is always **text (+ icon)**, never colour alone.
- **Category colours** are user data; they are shown only as the small dot before the category / contract name and
  never as a text or status colour. The default palette for new categories avoids the pure status green and red.
- **Unpaid** is the state that needs action: "Не оплачен" uses the neutral text colour (not muted grey), "due soon"
  amber, "overdue" red; "Оплачен" may be green or muted.
- Accent (`Primary`) colour only for interactive elements (buttons, links, selection), not for decoration.

## 9. Numbers, dates and empty values [NEW]
- All visible formatting is done in the ViewModel with the **app culture** (`LanguageService`), never by a control's
  `Format` property.
- **Money:** `1 234,56 €` — group separator and the space before `€` are **non-breaking** (` `), so an amount
  never wraps. In "label value" sequences the space between label and amount is also non-breaking
  ("Итого 49,22 €").
- **Month:** long `сентябрь 2026` (nominative); short `сен 2026` / chart axis `сен 26`, using the app's own 3-letter
  names from resx (`янв фев мар апр май июн июл авг сен окт ноя дек`; EN `Jan … Dec`). Ranges: `28 окт – 3 ноя`.
- **No value:** `—` (em dash) in muted colour. **Not calculated:** `—` plus the reason; never `0,00`.
  **Approximate:** `≈ 12,30`. Zero is shown only when the value really is zero.
- A line that would only say "0,00 €" (e.g. "expected recalculation: 0,00 €") is not shown.
- Quantities with their unit (`1,267 м³`); percentages with a space (`21 %`).

## 10. Screen states [NEW]
Every list, overview block and report goes through the same states, each with a defined look:
1. **Loading** — muted text "Загрузка…" / "Расчёт…" in the place of the content (no spinner, owner 2026-10-29).
   Not shown if loading takes < 300 ms.
2. **Empty** — text per §5.2, centred, no button (the add entry point is the FAB / the button above the list).
3. **Nothing found** (filters) — text per §5.2 under the filter header.
4. **Data.**
5. **Error** — a card with the message, "Повторить" (retry) and a pointer to the error log; the rest of the screen
   stays usable.

## 11. Type scale [NEW]
| Use | Size | Weight |
|---|---|---|
| Page title (toolbar) | platform default | — |
| Card / section header (`SectionHeader`) | 18 | Bold |
| Body, field values, list titles | default (14 Android / 14 Windows) | Regular; list title Bold |
| Secondary line, status, captions (`MutedLabel`, badges) | 13 | Regular / Bold for status |
| Hints under fields, footnotes | 12 | Regular, muted |

No other sizes in pages; styles carry the sizes.

---

## Appendix — why some rules exist (history)
- 2026-10-20: rules agreed after testing the Properties screen (T-012a).
- 2026-10-23: owner — Save stays in the editor; only Back leaves. FlexLayout clipped the first item of wrapped rows.
- 2026-10-24: all previously tapped list items stayed highlighted on Windows (null stroke in a selection trigger);
  menu items always open the section root.
- 2026-10-29: choice dialogs §3a (owner feedback); no animated spinners.
- 2026-11-01: one content pane everywhere (T-028f); low windows are phones (T-028a).
- 2026-11-02: `FontImageSource` with a system font crashes unpackaged Windows on theme change (#197, #199).
- #220: the snackbar host had a constant `IsVisible="False"` from T-019 to #220.
- 2026-11-02: changing `ItemsLayout` during an items update threw on WinUI (owner's error log) → `ListColumns` posts
  the change.
- 2026-11-05: UI review → revision 1 (this text), agreed with the owner: date lists in one column, compact-by-height on
  Android only, Delete only at the end of the form, single delete with Undo instead of a confirmation (bigger snackbar).
