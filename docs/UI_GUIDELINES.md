# UI guidelines

Status: agreed with the owner on 2026-10-20 (after testing the Properties screen). Every UI task follows
these rules; the Properties screen (after T-012a) is the reference implementation.

## 1. Icons — use the platform's standard icons
- **Android**: Google **Material Icons Outlined** (static font `MaterialIconsOutlined-Regular.otf`, ~330 KB,
  same code points as Material Symbols) — the icons Android users know. Bundled in `Resources/Fonts/`
  (Apache 2.0, licence file next to it). **Do not** add the 10 MB variable Material Symbols font.
- **Windows**: **Segoe Fluent Icons** (built into Windows 11; fall back to **Segoe MDL2 Assets** on Windows 10)
  — the icons of Windows itself. Not bundled (system font).
- One XAML resource per icon with `OnPlatform`, e.g. `IconDelete`, used everywhere; never raw glyph codes in pages.

| Key | Meaning | Android (Material Symbols) | Windows (Segoe Fluent / MDL2) |
|---|---|---|---|
| `IconAdd` | add | `add` | `E710` Add |
| `IconEdit` | edit | `edit` | `E70F` Edit |
| `IconDelete` | delete | `delete` (trash can) | `E74D` Delete |
| `IconSave` | save / confirm | `check` | `E73E` CheckMark (toolbar) / `E74E` Save (desktop command) |
| `IconClose` | close / cancel | `close` | `E711` Cancel |
| `IconBack` | back | system back arrow | system back arrow |
| `IconMore` | more actions | `more_vert` | `E712` More |
| `IconOpenLink` | open in browser | `open_in_new` | `E8A7` OpenInNewWindow |
| `IconCopy` | copy | `content_copy` | `E8C8` Copy |
| `IconArchive` | archive | `archive` | `E7B8` Package |
| `IconPhoto` | photo / camera | `photo_camera` | `E722` Camera |
| `IconHelp` | help (toolbar "?" opening the user guide at the screen's section) | `help_outline` | `E897` Help |
| `IconHistory` | history | `history` | `E81C` History |
| `IconPaid` | mark as paid | `task_alt` | `E73E` CheckMark |
| `IconExpand` / `IconCollapse` | expand / collapse | `expand_more` / `expand_less` | `E70D` / `E70E` Chevron down/up |

Icon-only buttons always have a tooltip (`ToolTipProperties.Text`) and an accessibility name
(`SemanticProperties.Description`) — localized.

## 2. Button hierarchy
- **Primary** (filled, accent colour): **one per screen/dialog** — Save / confirm.
- **Secondary** (outlined or text): Cancel, Edit, secondary actions.
- **Destructive** (red text or red trash icon): Delete. Placed apart from Save (end of the form, or icon in the
  row). Always confirmed.
- Styles `PrimaryButton`, `SecondaryButton`, `DestructiveButton`, `IconButton` in `Styles.xaml`; no ad-hoc colours.

## 3. Layout of list + editor screens
- **List**: "＋ New …" — on phones a **floating action button** with `IconAdd` bottom-right; on tablet/desktop a
  button above the list. Tap a row to open it.
- **Editor**: grouped into **cards/sections** with a header; a section that contains a list (parameters, links,
  lines…) has its own `IconAdd` button **in the section header**.
- **Child rows**: tap the row → edit; `IconDelete` at the end of the row (with confirmation). No separate
  "Edit"/"Remove" text buttons.
- **Adding/editing a child item opens a dialog** (popup on desktop/tablet, full-screen sheet on phones) with its
  own title ("New parameter", "Edit parameter") and its own Save / Cancel — never an inline form inside the
  parent form.
- **Editor commands**
  - Phone (separate page): toolbar has system **Back** + **Save** (`IconSave` ✓). **✓ only saves and never
    leaves the page; only Back leaves** (owner rule 2026-10-23, applies to every editor and dialog page).
    No separate "Cancel" — Back cancels. Delete in the `IconMore` menu or as a red "Delete …" at the end of the form.
  - Tablet/desktop (right pane): a footer with **Save** (primary), **Cancel** (secondary), and **Delete …**
    (destructive) at the far end.
- No artificial maximum width of the window or panes (space is needed for charts later). Two-pane split is
  proportional (list ~35–40 %).

## 4. Navigation and Back
- Back (Android hardware/gesture back, toolbar back arrow, Windows back) **must always work**:
  - no unsaved changes → go back immediately;
  - unsaved changes → the standard "unsaved changes" dialog (§5); after Save/Don't save → go back;
  - after a successful **Save → stay in the editor** (title updates, short confirmation); after **Delete →
    go back**. (Changed 2026-10-23 at the owner's request.)
- Never swallow Back without showing a dialog.
- **Menu (flyout) items always open the section's root page**; pages pushed inside the section being left are
  closed. Leaving any page with unsaved changes — menu, back, window close — goes through the central navigation
  guard (T-017a) with the standard "Save changes?" dialog. The menu is never disabled. (Owner decision 2026-10-24.)

## 5. Standard dialog texts (EN / RU)
Use these wordings everywhere (resx keys in brackets):

| Situation | Title | Message | Buttons |
|---|---|---|---|
| Leaving with unsaved changes [`Dialog_Unsaved_*`] | Save changes? / Сохранить изменения? | — | **Save** / Сохранить · **Don't save** / Не сохранять · **Cancel** / Отмена |
| Delete an item [`Dialog_Delete_*`] | Delete «{0}»? / Удалить «{0}»? | It will be hidden; history is kept. / Запись будет скрыта, история сохранится. | **Delete** / Удалить · **Cancel** / Отмена |
| Delete a child row | Delete this entry? / Удалить эту запись? | {0} | **Delete** / Удалить · **Cancel** / Отмена |
| Error | Error / Ошибка | {0} | **OK** |

"Cancel" in a dialog always means "close the dialog and do nothing". Words like "Discard", "Continue",
"Отбросить", "Продолжить" are not used.

## 6. Forms
- Label above the field; validation message in red directly under the field.
- Pickers must always display the current value (Android binding bugs: test that the selected item shows).
- Numbers accept `,` and `.`; dates use the platform date picker.
- Read-only summary lines ("Now: 54.7 m² since 20.10.2026") in muted colour.

## 6a. Known MAUI pitfalls (found in owner tests)
- `IsVisible` on an element whose `BindingContext` is changed by the same element is evaluated against the
  **new** context — wrap it: `<ContentView IsVisible="{Binding HasSelection}"><views:Editor BindingContext="{Binding Selected}"/></ContentView>`.
- Bundled fonts need `<MauiFont Include="Resources\Fonts\*.otf;Resources\Fonts\*.ttf" />` in the App csproj;
  missing glyphs show as empty boxes on Android only.
- Selectable list items: give the `Border` an explicit base `Stroke` (Outline colour) and `StrokeThickness="1"`;
  a selection trigger that reverts to a *null* stroke leaves the old highlight on Windows (owner screenshot
  2026-10-24: all previously tapped items stayed highlighted).
- Decimal input: every number field parses through `DecimalInput` (accepts both `,` and `.`, spaces as group
  separators); values are stored as `decimal`, never as text. Android's numeric keyboard may offer only one of
  the two separators depending on the keyboard app — both are accepted, so either works.
- **Do not use `FlexLayout`** on Android: it clips the first item of each wrapped row (owner screenshot
  2026-10-23). Use fixed rows (`HorizontalStackLayout`s) or a `Grid`.
- Only one "add" entry point per layout: FAB on phones, button above the list on tablet/desktop; the empty
  state shows text only.

- Do not use CommunityToolkit `Toast`/`Snackbar` — they use system notifications and crash the unpackaged
  Windows app. Use `ISnackbarService` (`ShowAsync` with an optional action such as Undo; `Notify` for
  fire-and-forget confirmations like "Saved", "Copied") — in-app bar on both platforms since T-019.

## 6b. Robustness
- No user input may crash the app: every parser uses `TryParse`, every text field has `MaxLength` matching the
  validator, format strings typed by the user are validated (see `PaymentPurpose.Validate`).
- Command handlers run through the shared safe wrapper (error dialog + log), never let exceptions escape.

## 7. Checklist for every UI task (put into the PR's manual test steps)
1. Phone, tablet portrait, tablet landscape / wide window.
2. Back from every page with and without unsaved changes.
3. Every picker shows its value after reopening the item.
4. Dark theme readable.
5. Russian and English texts fit (no cut-off buttons); rows of buttons/swatches wrap on phones.
6. Nonsense input in every field → validation message, no crash.
7. `docs/user-guide/en.md` and `ru.md` updated for everything the user sees (CLAUDE.md, "User guide").
