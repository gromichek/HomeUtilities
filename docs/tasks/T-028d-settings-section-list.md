# T-028d: Settings as a list of sections, unified controls
Phase: 8a · Depends on: T-028f (one content pane), T-028e (Reminders card) · Size: M

Owner finding 3 (PROGRESS "T-028a findings", 2026-10-31): Settings is one long page; on a phone a button text is cut
("Очистит" instead of "Очистить" in Error log — two buttons side by side). He wants Settings like the rest of the app.

## Goal
Settings becomes a **list of sections**; tapping a section opens it as a **page** on every device (one content pane,
T-028f; owner decision 2026-11-01). Back returns to the list.

## Sections (one row each: icon, title, one-line current value)
1. **Language** — current language ("Русский"); choice per UI_GUIDELINES §3a (single choice dialog / radio list).
2. **Theme** — System / Light / Dark (if the app has the setting; same choice pattern).
3. **Reminders** — "On, 18:30" / "Off"; the existing card content (T-028/T-028e) incl. Planned reminders.
4. **Backup** — last backup date; Save copy…, Restore from copy….
5. **Imports** — existing import profiles and the meter-link repair (as today).
6. **Error log** — number of entries; Copy, Clear (confirmation).
7. **About** — version, database file path, layout mode (debug info), sample data / delete all in Debug as today.
Keep every existing setting; move, do not drop anything. If you find a setting not in this list, add a section or put
it in the closest one and say so in the PR.

## Controls
- Choices (language, theme, lead days if it fits) as choice rows/drop-downs like elsewhere (UI_GUIDELINES §3a), not as
  rows of buttons. Steppers (`−`/`+`) only for small numbers.
- Buttons inside a section: at most one primary; buttons stack vertically on compact width and never cut text.
- Plain explanatory texts as muted labels under the section title.

## Scope — do NOT
- No new settings, no behaviour changes of reminders/backup/import beyond moving them.
- No new packages.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Phone (portrait, landscape), tablet, Windows: Settings shows the section list; each section opens as page/pane;
   Back returns to the list; no cut texts in Russian.
3. The "?" help opens the matching guide section; user guide EN + RU updated (Settings chapter restructured by section).
4. ViewModel tests: section list values (language name, reminders "On, 18:30"/"Off", last backup, log count).

## Manual test steps for the owner
1. Phone: Menu → Settings → list of sections with current values. Tap Language → choose English → the UI switches;
   Back → list shows "English".
2. Tap Reminders → all reminder settings and Planned reminders as before; Back.
3. Tap Error log → Copy / Clear stacked, texts fully visible.
4. Tablet/Windows: the section list fills the content area; a section opens as a page; Back returns to the list.
