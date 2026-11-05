# Signing key for Android builds

Without setup every PC signs Android builds with its own **debug key**. Builds from two PCs then have different SHA-1
fingerprints: they cannot be installed over each other and Google sign-in works only on the PC whose SHA-1 is registered.
A **permanent key** that you keep and copy to every PC fixes both. The key never enters the repository.

## 1. Create the key (once)

Use the JDK that Visual Studio installs. Pick a folder **outside the repository** (for example a `Keys` folder in your
user folder) and a password; keep the password in a text file in the same folder (`homeutilities.pass`) — see below for its
exact content.

cmd:

```bat
"C:\Program Files\Microsoft\jdk-*\bin\keytool.exe" -genkeypair -v -keystore "%USERPROFILE%\Keys\homeutilities.keystore" -alias homeutilities -keyalg RSA -keysize 4096 -validity 10000
```

PowerShell (take the newest JDK folder):

```powershell
$keytool = (Get-ChildItem "C:\Program Files\Microsoft\jdk-*\bin\keytool.exe" | Select-Object -Last 1).FullName
& $keytool -genkeypair -v -keystore "$env:USERPROFILE\Keys\homeutilities.keystore" -alias homeutilities -keyalg RSA -keysize 4096 -validity 10000
```

(cmd does not expand `jdk-*`; if it fails, type the real folder name.) keytool asks for the password (use the same for the
store and the key) and for a name and organisation: any text is fine.

**Keep it safe.** Store the keystore and the password file in a folder outside the repo **and a second copy on another
medium** (USB stick, private cloud). If both are lost, you can never update an installed app again: one more reinstall
with a new key and a new Google client is needed.

## 2. Use it on each PC

1. Copy `src/HomeUtilities.App/signing.template.props` to `src/HomeUtilities.App/signing.props` (git-ignored).
2. Fill in the absolute path of the keystore, the alias (`homeutilities`) and the passwords. Passwords are given as
   `file:<path to the password file>` or `env:<NAME OF AN ENVIRONMENT VARIABLE>`; never type the password into the file.
   Paths can use `$(USERPROFILE)`, e.g. `$(USERPROFILE)\Keys\homeutilities.keystore` (MSBuild does not understand
   `%USERPROFILE%`).

**The password file must contain the password on two lines.** `apksigner` reads the keystore password and the key
password one after the other from the same file (first line, second line); a file with one line fails with
`Failed to read Key "homeutilities" password … end of file reached` and the build stops with `"java.exe" exited with
code 2`. Write it like this in PowerShell (UTF-8 without BOM — `echo … > file` in Windows PowerShell 5.1 writes UTF-16,
which does not work):

```powershell
[IO.File]::WriteAllText("$env:USERPROFILE\Keys\homeutilities.pass", "YOUR_PASSWORD`nYOUR_PASSWORD`n")
```
3. Build as usual. `signing.props` is used for **every** Android build (Debug and Release). Without the file nothing changes
   (the debug key is used, as in CI).

## 3. Read the SHA-1

```bat
"C:\Program Files\Microsoft\jdk-*\bin\keytool.exe" -list -v -keystore "%USERPROFILE%\Keys\homeutilities.keystore" -alias homeutilities
```

Copy the `SHA1:` line. In Google Cloud Console → APIs & Services → Credentials add a **new Android OAuth client** with
the package name `io.github.gromichek.homeutilities` and this SHA-1, and put its client ID into `secrets.json`
(`googleAndroidClientId`). The old debug-key client can stay until all PCs use the new key.

## 4. Migration: one reinstall

An app installed with the debug key cannot be updated by a build with another key. Once:

1. Press *Sync now* on the phone and on the PC.
2. Uninstall the app on the phone.
3. Install the build signed with the new key.
4. *Settings → Sync → Connect Google account*, then *Replace this device's data with the data from the cloud (recommended)*.

Without sync: before uninstalling use *Settings → Backup → Save backup…* and pick a place outside the app (Downloads,
Drive); after installing use *Restore from backup…*. The app's own automatic backup folder is deleted together with the
app, so it does not help here.

From then on a build from any PC that has the same key and `signing.props` installs over the previous one.
