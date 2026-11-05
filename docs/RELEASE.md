# Making and installing a release

A release is made **on your PC** with one script, because `secrets.json` and the signing key exist only there.
It produces two files: the signed Android APK and the Windows installer. Nothing is published online; you copy the files
to the devices by hand. There is no auto-update: to update, install the new file over the old one.

## One-time setup

1. **Visual Studio** with the .NET MAUI workload (as for normal builds) and the .NET 10 SDK.
2. **Inno Setup 6** (free, https://jrsoftware.org/isinfo.php), default folder `C:\Program Files (x86)\Inno Setup 6`.
   If it lives elsewhere, pass `-Iscc "<path>\ISCC.exe"` to the script.
3. The two git-ignored files next to the App project (templates are committed):
   - `src\HomeUtilities.App\secrets.json` — Google OAuth client values (Sync);
   - `src\HomeUtilities.App\signing.props` — the permanent Android signing key, see [SIGNING.md](SIGNING.md).

## Making a release

In PowerShell, in the repository folder (best on a clean `main`; the script warns about uncommitted changes and other branches):

```powershell
.\scripts\publish-release.ps1
```

`-AndroidOnly` or `-WindowsOnly` builds just one file. The script stops with a clear message if `secrets.json`,
`signing.props` (a release is never signed with the debug key) or Inno Setup is missing, and shows the last lines of the
build output when a step fails; the full logs are in `release\work\`.

Result in `release\` (git-ignored):

- `HomeUtilities-<version>.apk`
- `HomeUtilities-Setup-<version>.exe`

**Version number.** Every build (also from Visual Studio) gets it automatically, nothing to edit:
`2026.11.5` = the build date (UTC); the **build number** (Android `versionCode`) = minutes since 2026-01-01 00:00 UTC, so it only
grows. *Settings → About* shows `Version: 2026.11.5 (build 446000)` on both platforms. The script computes the numbers once and
passes them to both builds, so the APK and the setup of one release are identical in version.

## Installing on Windows

1. Copy `HomeUtilities-Setup-<version>.exe` to the PC and run it. The installer is **not code-signed**, so SmartScreen may
   say "Windows protected your PC": click **More info → Run anyway**.
2. No administrator rights are needed. The program goes to `%LocalAppData%\Programs\HomeUtilities`; the Start menu gets a
   shortcut (a desktop shortcut is an unchecked option). The installer language follows Windows (English or Russian).
3. **Updating:** run the newer setup. The running app is closed, the old program files are replaced completely, your data stays.
4. **Uninstalling:** *Settings → Apps → HomeUtilities → Uninstall*. Your data stays.

The app needs neither .NET nor the Windows App SDK on the PC (everything is included).

## Installing the APK

1. Copy `HomeUtilities-<version>.apk` to the phone/tablet (USB, Drive, …) and open it in a file manager.
2. Android asks to allow **installing unknown apps** for that file manager: allow it.
3. Play Protect may warn about an app from an unknown developer: choose **Install anyway**.
4. **Updating:** install the newer APK over the old one; data is kept (same package name and the same signing key).
   Installing an older build over a newer one is refused by Android (downgrade).

## Where the data lives

The app keeps its data (database, backups, attachments, secrets, logs) in the platform's app data folder
(`FileSystem.AppDataDirectory`). On Windows (unpackaged) this is
`%LocalAppData%\HomeUtilities.App\HomeUtilities.App\Data` (confirmed on the owner's PC, 2026-11-05); *Settings → About* shows the exact path of the database file. It is **not** the
install folder (`%LocalAppData%\Programs\HomeUtilities`), and neither the update nor the uninstaller touches it. To remove
the data too, delete that folder by hand (make a backup first). On Android the data is inside the app and is removed when the
app is uninstalled.
