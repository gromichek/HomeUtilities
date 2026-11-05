<#
.SYNOPSIS
    Builds the release files on the owner's PC: the signed Android APK and the Windows installer (T-035).

.DESCRIPTION
    Needs the two git-ignored files next to the App project (secrets.json, signing.props), Inno Setup 6 for the
    Windows installer, and the .NET 10 SDK with the MAUI workload (as for Visual Studio builds).
    Output: release\HomeUtilities-<version>.apk and release\HomeUtilities-Setup-<version>.exe (release\ is git-ignored).
    The version is computed once: display version yyyy.M.d (UTC), build number = minutes since 2026-01-01 00:00 UTC.
    Works in Windows PowerShell 5.1 and PowerShell 7.

.PARAMETER AndroidOnly
    Build only the APK.

.PARAMETER WindowsOnly
    Build only the Windows installer.

.PARAMETER Iscc
    Full path of ISCC.exe when Inno Setup is not in "Program Files (x86)\Inno Setup 6".
#>
[CmdletBinding()]
param(
    [switch]$AndroidOnly,
    [switch]$WindowsOnly,
    [string]$Iscc
)

$ErrorActionPreference = 'Stop'

if ($AndroidOnly -and $WindowsOnly) {
    Write-Host 'ERROR: use either -AndroidOnly or -WindowsOnly, not both.' -ForegroundColor Red
    exit 1
}
$doAndroid = -not $WindowsOnly
$doWindows = -not $AndroidOnly

$repo = Split-Path -Parent $PSScriptRoot
$appDir = Join-Path $repo 'src\HomeUtilities.App'
$appProject = Join-Path $appDir 'HomeUtilities.App.csproj'
$releaseDir = Join-Path $repo 'release'
$workDir = Join-Path $releaseDir 'work'
$issFile = Join-Path $repo 'installer\HomeUtilities.iss'

function Stop-Release([string]$Message) {
    Write-Host ''
    Write-Host "ERROR: $Message" -ForegroundColor Red
    exit 1
}

# Runs a dotnet command, keeps the whole output in a log file and shows its last lines when it fails.
function Invoke-Step([string]$Name, [string]$LogFile, [string[]]$Arguments) {
    Write-Host "== $Name" -ForegroundColor Cyan
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'   # Windows PowerShell 5.1 turns native stderr text into errors otherwise
    try {
        & dotnet @Arguments *> $LogFile
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $oldPreference
    }
    if ($code -ne 0) {
        Write-Host (Get-Content $LogFile -Tail 25 | Out-String)
        Stop-Release "Step '$Name' failed (exit code $code). Full output: $LogFile"
    }
}

# ---- 1. Checks -------------------------------------------------------------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Stop-Release 'dotnet was not found. Install the .NET 10 SDK.' }

if (-not (Test-Path (Join-Path $appDir 'secrets.json'))) {
    Stop-Release 'src\HomeUtilities.App\secrets.json is missing: Google sign-in would not work in the release. Copy secrets.template.json and fill it in.'
}
if ($doAndroid -and -not (Test-Path (Join-Path $appDir 'signing.props'))) {
    Stop-Release 'src\HomeUtilities.App\signing.props is missing: a release must never be signed with the debug key. See docs\SIGNING.md.'
}
if ($doWindows) {
    if (-not $Iscc) {
        $candidates = @()
        if (${env:ProgramFiles(x86)}) { $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe') }
        if ($env:ProgramFiles) { $candidates += (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe') }
        $Iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (-not $Iscc -or -not (Test-Path $Iscc)) {
        Stop-Release 'Inno Setup 6 (ISCC.exe) was not found. Install it or pass -Iscc <path to ISCC.exe>.'
    }
}

$git = Get-Command git -ErrorAction SilentlyContinue
if ($git) {
    $status = & git -C $repo status --porcelain
    if ($status) { Write-Warning 'The working tree has uncommitted changes: the release does not match a commit.' }
    $branch = (& git -C $repo rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -ne 'main') { Write-Warning "The current branch is '$branch', not 'main'." }
}

# ---- 2. Version (computed once, same rule as the csproj) -------------------------------------------------------------
$now = [DateTime]::UtcNow
$epoch = New-Object DateTime 2026, 1, 1, 0, 0, 0, ([DateTimeKind]::Utc)
$version = '{0}.{1}.{2}' -f $now.Year, $now.Month, $now.Day
$build = [int][Math]::Floor(($now - $epoch).TotalMinutes)
Write-Host "Version $version (build $build)" -ForegroundColor Green

if (Test-Path $workDir) { Remove-Item $workDir -Recurse -Force }
New-Item -ItemType Directory -Path $workDir -Force | Out-Null
$versionArgs = @("-p:ApplicationDisplayVersion=$version", "-p:ApplicationVersion=$build")
$results = @()

# ---- 3. Android ------------------------------------------------------------------------------------------------------
if ($doAndroid) {
    $androidOut = Join-Path $workDir 'android'
    Invoke-Step 'Android publish' (Join-Path $workDir 'android.log') (@(
            'publish', $appProject, '-f', 'net10.0-android', '-c', 'Release', '-p:AndroidPackageFormat=apk', '-o', $androidOut) + $versionArgs)
    $apk = Get-ChildItem $androidOut -Filter '*-Signed.apk' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $apk) { Stop-Release "No signed APK (*-Signed.apk) found in $androidOut. See $(Join-Path $workDir 'android.log')." }
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
    $apkTarget = Join-Path $releaseDir "HomeUtilities-$version.apk"
    Copy-Item $apk.FullName $apkTarget -Force
    $results += $apkTarget
}

# ---- 4. Windows (unpackaged, self-contained) + installer -------------------------------------------------------------
if ($doWindows) {
    $winOut = Join-Path $workDir 'windows'
    Invoke-Step 'Windows publish' (Join-Path $workDir 'windows.log') (@(
            'publish', $appProject, '-f', 'net10.0-windows10.0.19041.0', '-c', 'Release', '-r', 'win-x64', '--self-contained',
            '-p:WindowsPackageType=None', '-p:WindowsAppSDKSelfContained=true', '-p:PublishReadyToRun=false', '-o', $winOut) + $versionArgs)
    if (-not (Test-Path (Join-Path $winOut 'HomeUtilities.App.exe'))) {
        Stop-Release "HomeUtilities.App.exe is missing in $winOut. See $(Join-Path $workDir 'windows.log')."
    }

    Write-Host '== Installer (Inno Setup)' -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
    $isccLog = Join-Path $workDir 'iscc.log'
    $oldPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $Iscc "/DAppVersion=$version" "/DSourceDir=$winOut" "/DOutputDir=$releaseDir" $issFile *> $isccLog
        $code = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $oldPreference
    }
    if ($code -ne 0) {
        Write-Host (Get-Content $isccLog -Tail 25 | Out-String)
        Stop-Release "Step 'Installer' failed (exit code $code). Full output: $isccLog"
    }
    $setup = Join-Path $releaseDir "HomeUtilities-Setup-$version.exe"
    if (-not (Test-Path $setup)) { Stop-Release "The installer was not created: $setup" }
    $results += $setup
}

# ---- 5. Result -------------------------------------------------------------------------------------------------------
Write-Host ''
Write-Host "Done. Version $version (build $build)" -ForegroundColor Green
$results | ForEach-Object { Write-Host "  $_" }
