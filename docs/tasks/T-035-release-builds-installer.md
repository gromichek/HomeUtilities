# T-035: Release builds — version number, signed APK, Windows installer (Inno Setup), release script
Phase: 9 · Depends on: T-034 (#222) · Size: M

Owner decisions (2026-11-05): Windows gets an installer made with **Inno Setup 6** (already installed on the owner's PC,
6.7.3); **no auto-update**; files are copied to the devices **by hand**. Android: the signed release APK. Everything is
built **on the owner's PC** by one script, because `secrets.json` and the signing key exist only there. Nothing is
published to GitHub Releases.

## Scope — do

### A. Version number (every build, no file to edit or commit)
Today every build is 1.0 (1), so nobody can tell which build is installed. Compute the version in MSBuild for **all**
builds of the App (Debug from Visual Studio too — a Debug deploy with a lower Android `versionCode` than an installed
release build would fail with a downgrade error):
- `ApplicationDisplayVersion` = `yyyy.M.d` of the build date (UTC), e.g. `2026.11.5` — valid for Windows (each part
  ≤ 65535) and readable.
- `ApplicationVersion` (Android `versionCode`) = whole minutes since 2026-01-01 00:00 UTC — always increasing, far below
  the Android limit (2 100 000 000).
- Both can be overridden from the command line (`-p:ApplicationVersion=…`) — the script does that so the APK and the
  Windows build of one release carry the same numbers.
- Settings → About shows `2026.11.5 (build 446000)` on both platforms (check what `AppInfo.VersionString` /
  `BuildString` return on Windows unpackaged and make both show the same). The version written into backups and sync
  files is informational only (checked: nothing compares it) — keep using `VersionString`.
If MSBuild property functions turn out not to work here (e.g. the MAUI targets read the values before), describe it under
"Open questions" with your proposal instead of guessing.

### B. Windows build and installer
1. Publish unpackaged and self-contained (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true` as today,
   `SelfContained=true`, `RuntimeIdentifier=win-x64`), so the target PC needs no .NET or Windows App SDK install. Do not
   switch to MSIX/packaged.
2. Inno Setup script `installer/HomeUtilities.iss` (committed; licence of Inno Setup: its own permissive licence — it is
   a build tool, nothing of it ships except the setup stub; state it in the PR):
   - fixed `AppId` (new GUID, never change it), `AppName` HomeUtilities, version from the command line (`/DAppVersion=…`);
   - **per-user install without admin rights** (`PrivilegesRequired=lowest`, `{localappdata}\Programs\HomeUtilities`);
   - Start menu shortcut, optional desktop shortcut (unchecked task), "Launch HomeUtilities" at the end;
   - installer languages English + Russian (Inno's built-in `Russian.isl`), chosen from the Windows language;
   - installing a newer version over an older one replaces the program files (delete the old program files first, so
     removed DLLs do not linger) — the running app is closed first (`CloseApplications`);
   - **user data is never touched**: the app keeps its data in `FileSystem.AppDataDirectory` (database, backups,
     attachments, secrets, logs). Find out the real path for the unpackaged build and make sure the install folder is a
     different one and the uninstaller does not delete the data folder; say both paths in the PR and in RELEASE.md;
   - setup and uninstall icon = the app icon (an `.ico` generated from the app icon SVGs, committed under `installer/`).
3. Output: `HomeUtilities-Setup-<version>.exe`.

### C. Android
`dotnet publish -f net10.0-android -c Release` with the version properties; take the `-Signed.apk`; copy it as
`HomeUtilities-<version>.apk`. The script **stops with a clear message** if `signing.props` is missing (a release must
never be signed with the debug key) or `secrets.json` is missing (Google sign-in would not work).

### D. Release script `scripts/publish-release.ps1` (Windows PowerShell 5.1 and PowerShell 7)
1. Checks: `secrets.json`, `signing.props`, Inno Setup compiler (`ISCC.exe` in `Program Files (x86)\Inno Setup 6`, or a
   `-Iscc` parameter); warns (does not stop) when the working tree has uncommitted changes or the branch is not `main`.
2. Computes the version once (same rule as A), runs the Android and Windows publishes, compiles the installer.
3. Puts `HomeUtilities-<version>.apk` and `HomeUtilities-Setup-<version>.exe` into `release\` at the repo root
   (git-ignored), prints the two paths and the version. Parameters `-AndroidOnly` / `-WindowsOnly`.
4. Clear errors (which step failed, the last lines of the build output), exit code ≠ 0.

### E. Docs
- `docs/RELEASE.md` (English, for the owner): one-time setup (Inno Setup, the two git-ignored files), making a release,
  installing on Windows (SmartScreen: "More info" → "Run anyway" — the installer is not code-signed), installing the APK
  (allow "install unknown apps" for the file manager; Play Protect warning → install anyway), updating = install the new
  file over the old one, data is kept; where the data lives; how to uninstall.
- README build section: link to RELEASE.md.
- User guide EN + RU: update "Installing a new version" (from T-034) with the installer / APK steps in user words; About
  shows the version.
- In the PR propose ARCHITECTURE text (release builds are made locally by the script; CI only builds) — do not edit it.

## Scope — do not
No auto-update, no code-signing certificate, no CI release job, no MSIX, no GitHub Releases, no new NuGet packages.

## Tests
Unit-test anything computed in C# (e.g. a version text helper for About). The MSBuild version rule: verify locally by
building the Android target in the sandbox if possible and reading the manifest's `versionCode`/`versionName`; the
Windows publish, the installer and the script cannot run in the sandbox — say so in the PR. CI must stay green (CI
builds without `signing.props`; the version rule must work there too).

## Acceptance criteria
1. `dotnet test HomeUtilities.Tests.slnf` green, zero warnings; CI green; nothing secret or personal in the repo.
2. Owner: `scripts\publish-release.ps1` produces both files; the installer installs without admin rights, updates over an
   older version with data kept, uninstalls cleanly leaving the data; the APK installs over the current app with data
   kept; About shows the same version on both.
3. UI checklist of CLAUDE.md (only About changes) and the guide EN + RU.

## Manual test steps for the owner
1. PowerShell in the repo: `.\scripts\publish-release.ps1` → `release\` contains the APK and the setup exe, version printed.
2. PC: run the setup (SmartScreen → More info → Run anyway) → Russian installer → install → app starts with all your data.
3. Settings → About: the version from step 1.
4. Make a second release later (or run the script again after a minute) → install over → data kept, About shows the new
   build number.
5. Phone: copy the APK, open it → install over the existing app → data kept, About shows the same version as the PC.
6. Windows → Settings → Apps → HomeUtilities → Uninstall → reinstall → your data is still there.
