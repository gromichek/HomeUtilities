# T-017b: In-app help — the user guide inside the app
Phase: 3 · Depends on: T-017a · Size: S–M

## Goal
The owner wants the user guide (`docs/user-guide/en.md`, `ru.md`) readable inside the app, offline, in the app's
language, with a "?" on screens that opens the matching section. There must be **one source**: the same Markdown
files that sessions already maintain — no copies.

## Decided (lead, 2026-10-24)
- **Bundling:** the App csproj includes the guide files directly from `docs/user-guide/` as `MauiAsset`s, e.g.
  `<MauiAsset Include="..\..\docs\user-guide\**\*" LogicalName="guide\%(RecursiveDir)%(Filename)%(Extension)" />`.
  Nothing is copied into `src/`.
- **Rendering:** Markdown → HTML with **Markdig** (new dependency allowed; add it to `Directory.Packages.props`,
  latest stable that is at least two weeks old). Conversion lives in `HomeUtilities.ViewModels` (Markdig is pure
  .NET, no MAUI) as `IHelpContentService`/`HelpDocument`: reads the file through an `IAppAssetReader` interface
  (implemented in App with `FileSystem.OpenAppPackageFileAsync`), converts with Markdig (pipeline:
  pipe tables, auto identifiers for headings, auto links), strips HTML comments, wraps it in a page with inline CSS
  (light + dark via `prefers-color-scheme` and the app theme, readable on a phone, tables scroll horizontally).
- **Display:** `HelpPage` with a `WebView` + `HtmlWebViewSource`. Links:
  - `#anchor` and links between `en.md`/`ru.md` → stay inside the help page;
  - `http(s)://` → open in the system browser (`Launcher`), cancel the WebView navigation.
- **Diagrams:** the WebView cannot render Mermaid offline without bundling a 3 MB script, so the guide switches to
  images: each diagram's source lives in `docs/user-guide/img/<name>.<lang>.mmd`, the rendered
  `docs/user-guide/img/<name>.<lang>.svg` is committed, and the Markdown references the SVG
  (`![…](img/relations.ru.svg)`). Add `scripts/render-guide-diagrams.sh` (uses `mmdc`, already present in the
  coding sandbox; documented fallback `npx @mermaid-js/mermaid-cli`). Convert the existing relationships diagram in
  both files. Images are inlined into the HTML (SVG as `data:` URI or inline markup) so the page needs no file access.
  Add one line to CLAUDE.md "User guide": diagrams are edited in the `.mmd` files and re-rendered with the script.
- **Language:** the file for the current UI language (`ru` → `ru.md`, otherwise `en.md`); a language change
  while the page is open reloads it (existing language-changed message).

## Scope — do
1. Menu item **Help / Справка** (above Settings) → `HelpPage` showing the whole guide.
2. Contextual help: icon button `IconHelp` (add to UI_GUIDELINES table: Android `help_outline`, Windows `E897` Help)
   in the toolbar of the **section root pages** (Properties, Suppliers, Readings, Settings → Categories) and of the
   contract and meter editors. It opens `HelpPage` scrolled to the section anchor (use the anchors Markdig generates; keep a small map `HelpTopic → anchor per language` in ViewModels with a test that every
   anchor exists in both files). Back returns to the screen.
3. Robustness: missing/broken file → a short localized message on the page, error logged, no crash.
4. Tests (ViewModels): file per language; HTML contains headings with ids, tables, no HTML comments; external links
   marked for the browser; every `HelpTopic` anchor exists in en and ru (reads the real files from `docs/user-guide`
   via a test-time path); images inlined; missing file → message.
5. User guide: add a short note in §2 of both files ("Help in the menu, ? on screens").

## Scope — do NOT
- No search inside help, no editing, no online version.

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green (both builds include the assets).
2. Menu → Help shows the guide in the current language with the diagram, tables and dark theme readable.
3. "?" on Properties opens the Properties section; Back returns.
4. External links open the browser; internal links stay in help.

## Manual test steps for the owner
1. Windows and Android: menu **Справка** → guide in Russian with the diagram; switch the language to English in
   Settings → Help shows English.
2. Objects → **?** → opens at "Объекты"; Back → Objects. Same for a meter editor.
3. Dark theme: text and tables readable.
4. Tap a link to another section (stays) and an external link (browser opens).
