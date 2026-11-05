; Inno Setup 6 script for the Windows installer (T-035). Compiled by scripts/publish-release.ps1:
;   ISCC.exe /DAppVersion=2026.11.5 /DSourceDir=<publish folder> /DOutputDir=<folder> installer\HomeUtilities.iss
; Inno Setup is only a build tool (its own permissive licence); only the small setup stub ends up in the .exe.

#ifndef AppVersion
  #error Pass the version: /DAppVersion=2026.11.5
#endif
#ifndef SourceDir
  #error Pass the folder with the published app: /DSourceDir=<path>
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif

[Setup]
; Never change AppId: it is how a newer setup recognises the installed version and the uninstaller.
AppId={{553C510B-1B6E-4CDB-9CDD-604583C1BC0B}
AppName=HomeUtilities
AppVersion={#AppVersion}
AppVerName=HomeUtilities {#AppVersion}
VersionInfoVersion={#AppVersion}.0
DefaultDirName={localappdata}\Programs\HomeUtilities
DefaultGroupName=HomeUtilities
DisableProgramGroupPage=yes
; Fixed per-user folder: [InstallDelete] empties {app}, so the user must not be able to point it at a folder with other files.
DisableDirPage=yes
; Per-user install, no administrator rights.
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=HomeUtilities.ico
UninstallDisplayIcon={app}\HomeUtilities.App.exe
UninstallDisplayName=HomeUtilities
OutputDir={#OutputDir}
OutputBaseFilename=HomeUtilities-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Close the running app before replacing its files (and start it again only through the "Launch" checkbox).
CloseApplications=yes
RestartApplications=no

[Languages]
; Chosen from the Windows display language; English is the fallback.
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; A newer version replaces the program files completely, so removed DLLs do not linger.
; Only {app} (the install folder) is touched. The user data lives in %LocalAppData%\<publisher>\HomeUtilities\Data, never here.
Type: filesandordirs; Name: "{app}\*"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\HomeUtilities"; Filename: "{app}\HomeUtilities.App.exe"
Name: "{group}\{cm:UninstallProgram,HomeUtilities}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\HomeUtilities"; Filename: "{app}\HomeUtilities.App.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\HomeUtilities.App.exe"; Description: "{cm:LaunchProgram,HomeUtilities}"; Flags: nowait postinstall skipifsilent

; No [UninstallDelete] section on purpose: the uninstaller removes only what the installer installed.
