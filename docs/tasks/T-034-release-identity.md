# T-034: Release identity — permanent signing key, real app icon, house notification icon
Phase: 9 · Depends on: T-030 (#194), T-028e · Size: M

Owner requests (2026-11-02, PROGRESS findings 8 and 9): Android builds are signed with the per-PC debug keystore, so a
build from another PC has another SHA-1 (Google sign-in fails) and cannot be installed over the existing app. The app
icon and splash are the MAUI placeholders (purple square, white house), and the notification icon is a bell.

## Scope — do

### A. Permanent Android signing key (build wiring only — the key itself never enters the repo)
1. Optional, git-ignored file `src/HomeUtilities.App/signing.props` (commit `signing.template.props` with placeholders
   and comments). When it exists, the App csproj imports it for **every** Android build (Debug and Release), so the SHA-1
   is the same on every PC: `AndroidKeyStore=true`, `AndroidSigningKeyStore` (absolute path **outside the repo**),
   `AndroidSigningKeyAlias`, `AndroidSigningStorePass` / `AndroidSigningKeyPass` preferably as `file:<path>` or
   `env:<NAME>` (MSBuild Android signing supports both) — no password in the repo, the template shows the forms.
   Without the file the build uses the debug key as today (CI unchanged; CI signing is out of scope).
2. `.gitignore`: `signing.props`, `*.keystore`, `*.jks`, `*.pass` (anywhere).
3. A short owner guide `docs/SIGNING.md` (English): create the key once with `keytool -genkeypair` (RSA 4096, validity
   10000 days, alias `homeutilities`; commands for **cmd and PowerShell** with the JDK that Visual Studio installs —
   `"C:\Program Files\Microsoft\jdk-*\bin\keytool.exe"`), where to keep it (a folder outside the repo, plus a second copy
   on another medium — losing it means one more reinstall), how to fill `signing.props` on each PC, how to read the SHA-1
   (`keytool -list -v`), and that a new **Android OAuth client** (same package name, new SHA-1) is added in Google Cloud
   (the old debug-key client can stay until all PCs use the new key). No real paths, names or fingerprints in the file.
4. **Migration (one reinstall, in SIGNING.md and in the user guide):** an installed debug-signed app cannot be updated by
   a build with another key. Steps: phone syncs with the PC (Sync now on both) → uninstall the app on the phone →
   install the build signed with the new key → Settings → Sync → Connect → "Replace this device's data with the data from
   the cloud". Alternative without sync: on the phone **Export** a backup to a place outside the app (check that the
   existing backup / JSON export can write a file the user picks; if it cannot, list it under "Open questions", do not
   build it), reinstall, restore it. Point out that the app's own backup folder is deleted with the app.

### B. Real app icon and splash
1. Replace the placeholders with an **original** design (no third-party artwork, no icon packs with non-permissive
   licences): background the app's primary blue `#2F5D8A` (the colour used by the notifications, `SetColor`), foreground
   a **white house** silhouette with a simple utility sign inside it (e.g. a meter dial or a drop) — readable at 24 dp,
   inside the adaptive-icon safe zone (centre 66 %). SVG sources in `Resources/AppIcon` (`appicon.svg` background,
   `appiconfg.svg` foreground), `MauiIcon Color` updated; splash = the same house on the same blue (`MauiSplashScreen`).
2. Windows: the same `MauiIcon` gives the exe/taskbar/window icon — check the unpackaged build shows it (CI artifact).
3. Put PNG previews (rendered from the SVGs, 48/96/192 px, round and square mask) into the PR description, not the repo.

### C. Notification small icon = the white house
1. Replace `Platforms/Android/Resources/drawable/ic_stat_reminder.xml` (keep the name — it is used by `ReminderAlarms`
   and `AndroidNotificationService`) with a vector of the **same house silhouette**, one colour (white) + transparency,
   24 dp, no inner details that vanish at status-bar size (the utility sign may be cut out as transparency if it stays
   readable; otherwise the plain house). `SetColor(0xFF2F5D8A)` stays.

### D. Docs
- `README.md`: build section mentions `signing.props` (optional) next to `secrets.json`.
- User guide EN + RU: short "Installing a new version" / migration note (Settings or the start of the guide — where it
  fits), screenshots not needed. In the PR propose the text for ARCHITECTURE §9 Secrets (signing key) — do not edit it.

## Scope — do not
No CI signing, no Play Store packaging (AAB), no version-number scheme, no new packages.

## Tests
No logic changes; `dotnet test HomeUtilities.Tests.slnf` green. Verify locally that the Android build works **without**
`signing.props` (CI path) and, with a throw-away keystore created in the sandbox (`keytool` from the SDK's JDK), that the
APK is signed with it (`apksigner verify --print-certs` or `keytool -printcert -jarfile`). Do not commit that keystore.

## Acceptance criteria
1. CI green, zero warnings; no keystore, password, path or fingerprint in the repo.
2. Owner: after creating the key and `signing.props`, builds on two PCs install over each other, Google sign-in works
   after adding the new Android client; the launcher, splash, Windows taskbar and notifications show the new house icon.
3. UI checklist of CLAUDE.md in the PR (only the items that apply).

## Manual test steps for the owner
1. Follow `docs/SIGNING.md`: create the key, fill `signing.props`, add the Android OAuth client with the new SHA-1.
2. Phone: Sync now (PC too) → uninstall the app → deploy the new build → Settings → Sync → Connect → "Replace this
   device's data with the data from the cloud" → your data is back.
3. Launcher icon and splash: blue with the white house. Windows: the same icon on the taskbar.
4. Settings → Reminders → test notification: a white house in the status bar and on the lock screen.
5. Second PC: copy the keystore + password file, fill `signing.props`, build → it installs over the phone app without
   uninstalling, Google sign-in still works.
