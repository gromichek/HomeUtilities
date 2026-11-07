# T-040: User guide review — check every section against the app, add what is missing (EN + RU)
Phase: 9 · Depends on: T-039 (#273, #275) · Size: M · Docs only (no app code, except help-topic anchors if they break)

Owner (2026-11-07): "the guide must be re-read and corrected, what is missing added". The guide grew task by task
(`docs/user-guide/en.md`, `ru.md`, ~1 450 lines each); several later changes were written as additions, and the
"17. Coming soon" section still says "charts on the Overview" although T-038 added them. The owner is now entering his
real data, so the guide is read by someone who uses the app daily, not by a developer: what is obvious to the author is not
obvious to him (owner's words; example: he did not find where custom fields with "Относится к: Строка счёта" appear).

## Scope — do
1. **Check every section against the current app**: screens, field and button names (exact wording from
   `AppResources.resx` / `AppResources.ru.resx`), behaviour, rules, warnings. Where the guide and the app disagree, the app
   is right — fix the guide; if the app looks wrong, list it under "Findings" in the PR (do not change code).
2. **Add what is missing** — at least, and check that each is described where the user looks for it:
   - Overview charts (A/B/C) — remove "Coming soon" (or the whole section if nothing is left);
   - choices: one tap selects, OK only for rows with inputs, "＋ New …" action rows;
   - delete without a question + Undo (which records), delete with the list of dependants (property, contract, supplier),
     the meter's "only from a backup";
   - editors appear complete after loading; a quick double tap opens a page once;
   - Database clean-up (T-037/T-037a); Windows photo shrinking (T-039); "Show sync messages" also hides errors;
   - "Передать онлайн" stays after a submit with "передано {date}";
   - custom fields: "Относится к: Счёт" → the bill's "Дополнительные поля" card, "Строка счёта" → inside each bill line
     (check T-036c wrote it in both the contract and the template sections);
   - Installing a new version / About (version and build number), release and signing as far as the user sees them.
3. **A "Quick start" section near the top** (both languages): the order in which a new user enters data — property →
   supplier → contract (+ tariff) → meter (+ link to the contract) → first reading → bills / templates → payments — with one
   line each and a link to the section; plus "what to do after the initial data entry" (clean-up, backup, sync on all
   devices).
4. **A short "Problems and answers" section**: Google sign-in fails (secrets / Android client / SHA-1 on another PC),
   Windows SmartScreen, "a change from the other device did not arrive" (Sync now, sync messages), reminders late or not
   shown (Android battery settings, lock-screen), restore of an older copy, where the data lives on Windows / Android.
   Facts only from the guide, the code and `docs/RELEASE.md` / `docs/SIGNING.md`.
5. **EN and RU say the same**: same sections in the same order, same facts and numbers; natural Russian, not a word-by-word
   translation. Examples stay invented (no real names, numbers, IBANs).
6. **Structure**: section numbers consistent (today "Overview" and "Bill templates" have no number), the table of contents
   (if any) and every in-app help anchor (`HelpTopic` → heading) still match — the "?" button of every screen must open the
   right heading. If a heading must change, change the anchor mapping in the same PR and test it (`HelpTopic` tests).
7. Diagrams (`docs/user-guide/img/*.mmd`) still correct; re-render with `scripts/render-guide-diagrams.sh` if changed.
8. Remove repetition and outdated sentences; keep it readable (short paragraphs, tables for rules, lists for steps).

## Not in this task
No app changes (except the help anchors, point 6), no screenshots in the guide.

## Acceptance criteria
`dotnet test HomeUtilities.Tests.slnf` green (help-topic tests), CI green. PR lists: sections changed, sections added,
findings where the app looks wrong. Owner: reads "Quick start" and "Problems and answers" and one or two sections he uses
most; "?" on Overview, Bills, Readings, Settings → Backup opens the right place.

## Manual test steps for the owner
1. Help → the new "Quick start" section; follow two links.
2. "?" on Overview, Bills, Readings, Settings → Backup and Sync — the right headings.
3. Read one section you use a lot (e.g. Readings or Bills) — anything missing or wrong → tell the lead.
