# T-023a: Reports — compact filters with drop-downs, readable multi-property table, Android spinner
Phase: 6 · Depends on: T-024 (run after it; both change the Reports screen) · Size: M

## Findings (owner, 2026-10-28, Windows wide window + Android)
1. The filter area is a wall of buttons: the category row does not fit on Windows (on Android it can be swiped), the
   period "‹ from › ‹ to ›" row is stretched over the full width, and the table starts only below the fold.
   Owner: "make the filters drop-down lists with check boxes (or something better) — very hard to use now".
2. With several properties the table columns are hard to assign to their property (only a centred property name above).
3. Android: when Reports opens for the first time the busy spinner keeps turning; it disappears only after switching to
   another report tab.

## Scope — do
### 1. Filters: one compact, collapsible panel
- Keep the report picker (Costs · Consumption · Unit cost · Calculated vs billed) as it is (it fits), and the
  Table/Chart switch from T-024.
- Replace all other button rows by **filter fields**, each showing its current value and opening a picker:
  - **Period** (single choice): This year · Last 12 months · Last year · All · **Custom…** → from/to month pickers in
    the same dialog. Field text: "This year (01.2026–12.2026)".
  - **Properties** and **Categories** (multi choice): dialog with check boxes, **All** / **None** at the top, OK /
    Cancel; field text "All (3)" / "Flat in town, Cottage" / "4 of 15". At least one must stay selected (OK disabled
    otherwise). Categories show their colour dot.
  - **Group by**, **Show** (incl. / excl. VAT / VAT), **Rows** (months / years): single choice — a native `Picker` is
    fine.
  - **Year over year**: switch.
  - Dialogs follow UI_GUIDELINES §3 (popup on desktop/tablet, full-screen sheet on phones), standard button texts.
  Implement the multi-select dialog once as a reusable view + VM (`MultiSelectDialogViewModel`), unit tested.
- Layout: the fields in a `Grid` — 3 columns on expanded, 2 on medium, 1 on compact (no FlexLayout, no horizontal page
  scroll). The panel can be **collapsed** to a one-line summary ("This year · All properties · All categories · by
  category · incl. VAT ▾"), remembered per session; collapsed by default on phones.
- Export buttons move into the header (icon buttons with tooltips, or a "⋯" menu with Export to Excel / CSV).

### 2. Table with several properties
- Each property's column group is visually separate: a property header cell spanning its columns with a coloured top
  stripe or a subtle alternating background per group, a **vertical separator** (2 px, outline colour) before each
  group, and the group's **Total** column bold with a light background. Grand total column (all properties) at the end
  when more than one property is selected.
- Column header text: wrap at word boundaries only (no "Электричеств/о" splits) — use a minimum column width that fits
  the longest word, or shorten with a tooltip showing the full name.
- The period column stays visible while scrolling horizontally (frozen) on all layouts.

### 3. Android spinner
- Find why the first load leaves `IsBusy`/`IsLoading` on (e.g. a load started in the constructor and another in
  `OnAppearing`, the second skipped by a guard while the first never resets the flag; or the flag set on a non-UI
  thread). Fix so the indicator always stops (try/finally), with a VM test for the first-load sequence and for an
  exception during load (error shown, spinner off).

### 4. Guide
- Reports section of `docs/user-guide/en.md` and `ru.md`: describe the filter fields, the collapsed summary, and how to
  read the multi-property table.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Windows wide and Android phone: all filters reachable without horizontal scrolling; the table is visible without
   scrolling when the panel is collapsed.
3. Multi-property table: columns clearly grouped; headers not split inside words.
4. Android: first opening of Reports shows the table and the spinner stops.

## Manual test steps for the owner
1. Reports → Costs: change period (presets and Custom), properties (check boxes, All/None), categories, group by,
   VAT display, rows, year over year — each from its field.
2. Collapse the filter panel → one summary line → table/chart moves up; go to another section and back → still
   collapsed.
3. Select all three properties → column groups separated, totals per property and a grand total.
4. Android: open Reports for the first time after app start → spinner stops when the table appears.
