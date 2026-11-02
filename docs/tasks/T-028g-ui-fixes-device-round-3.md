# T-028g: UI fixes from the device tests (round 3)
Phase: 8a · Depends on: T-028d, T-028f · Size: S–M

Owner tests 2026-11-02 (phone Android 16, tablet Android 13, Windows) after T-028d. Can run in parallel with T-028b
(performance) — keep to the files below.

## 1. Imports: no help
Settings → Imports (and the import pages) have no "?" or it opens nothing. Add a `HelpTopic` for Imports (the guide has an
import chapter — check the anchor in both languages, `RealGuide_HasEveryHelpTopicAnchor` test).

## 2. Imports → Saved profiles: buttons cut off
The card of a saved profile has **Export** and **Delete** side by side; on the phone their texts are cut. Compact: buttons
stacked (full width of the card, Delete last, red); medium/expanded: in a row only if both fit, otherwise stacked
(same grid pattern as T-028a/T-028d). Check every other card in Imports for the same problem.

## 3. Bill editor → Payment card: payment row and "Cancel payment" (phone)
A payment row shows the text squeezed into the left half (`02.11.2026 · 22,00 € · Bank transfer · "Paid" button` wraps
word by word, five lines) and a **huge red "Cancel payment" button** taking the right half. Make the payment row a normal
child row (UI_GUIDELINES §3): the text uses the full width (one or two lines), the action is a small icon button at the end
of the row (`IconDelete`/undo icon with the accessible name "Cancel payment", confirmation as now) — or, if a text button is
kept, it goes **under** the text, natural width, left-aligned. Same check for the payment rows in the Payments editor
(allocations) and the contract editor's Payments card.

## 4. Help: pinch to zoom (phone, tablet)
Owner wants to zoom the help with two fingers to read tables and diagrams. Android WebView: enable zoom
(`Settings.BuiltInZoomControls = true`, `DisplayZoomControls = false`, `SupportZoom`) in a handler/mapper for the help
WebView only; viewport meta `width=device-width, initial-scale=1` (keep), allow user scaling (no `user-scalable=no`, add
`maximum-scale=5`). Tables wider than the screen scroll horizontally inside their own block (`overflow-x: auto`) instead of
squeezing columns. Windows: Ctrl + wheel already zooms WebView2 — verify. In-help links (T-028c/#147) keep working.

## 5. Windows: first switch to the dark theme draws wrongly
First switch Settings → Theme → Dark on Windows: parts of the UI keep light colours / look broken; choosing Dark again
fixes it. Find why the first `UserAppTheme` change is not fully applied (e.g. resources read with `StaticResource` instead
of `AppThemeBinding`/`DynamicResource`, pages cached before the change, the title bar) and fix it so one switch is enough
in both directions (Light → Dark → System). Android: verify the same.

## Scope — do NOT
No performance work (T-028b), no new screens, no new packages.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green.
2. Owner: points 1–5 on phone, tablet and Windows as in the steps below. User guide EN + RU: help zoom (one sentence),
   Imports help.

## Manual test steps for the owner
1. Settings → Imports → "?" → the import part of the guide opens.
2. Phone, Russian: Imports → Saved profiles → Export / Delete fully visible (stacked). Tablet/Windows: no cut texts.
3. Phone: Bill → Paid → the payment row text in one or two lines across the width, a small cancel icon at the end; tap it →
   confirmation → payment cancelled.
4. Phone/tablet: Help → spread two fingers → the text and a diagram zoom; a wide table scrolls sideways.
5. Windows: Settings → Theme → Dark once → everything dark at once; → Light → everything light; → System.
