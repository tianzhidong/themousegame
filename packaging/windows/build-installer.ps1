param(
    [string]$CompilerPath = '',
    [string]$Version = '1.0.1',
    [string]$SourceDir = ''
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if (-not $SourceDir) { $SourceDir = Join-Path $projectRoot "output\windows_$Version" }
$sourceDir = (Resolve-Path -LiteralPath $SourceDir).Path
$toolsDir = Join-Path $projectRoot 'output\windows_installer_tools'
$outputFile = Join-Path $projectRoot "output\ForestTown-Setup-$Version-x64.exe"
$appIcon = Join-Path $projectRoot 'assets\images\ui\forest_town_icon.ico'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must use the format major.minor.patch.' }
if (-not (Test-Path -LiteralPath $appIcon)) { throw 'Application icon is missing. Run prepare-export.py first.' }

if (-not $CompilerPath) {
    $command = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if ($command) {
        $CompilerPath = $command.Source
    } else {
        $cacheRoot = Join-Path $env:LOCALAPPDATA 'electron-builder\Cache\nsis'
        $compiler = Get-ChildItem -LiteralPath $cacheRoot -Filter makensis.exe -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.Directory.Name -ne 'Bin' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($compiler) { $CompilerPath = $compiler.FullName }
    }
}
if (-not $CompilerPath -or -not (Test-Path -LiteralPath $CompilerPath)) {
    throw 'NSIS compiler not found. Specify -CompilerPath with the path to makensis.exe.'
}

$payloadFiles = @(
    Get-Item -LiteralPath (Join-Path $sourceDir 'ForestTown.exe')
    Get-ChildItem -LiteralPath (Join-Path $sourceDir 'data_NewGameProject_windows_x86_64') -Recurse -File
)
foreach ($requiredFile in @('NewGameProject.dll', 'GodotSharp.dll', 'coreclr.dll', 'hostfxr.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $sourceDir "data_NewGameProject_windows_x86_64\$requiredFile"))) {
        throw "Missing runtime file: $requiredFile"
    }
}

New-Item -ItemType Directory -Path $toolsDir -Force | Out-Null
Set-Content -LiteralPath (Join-Path $toolsDir '.gdignore') -Value '' -Encoding UTF8
$removeScript = Join-Path $toolsDir 'payload_uninstall.nsh'
$removeLines = @('; Generated from the exact installation payload.')
foreach ($file in $payloadFiles) {
    $relativePath = $file.FullName.Substring($sourceDir.Length + 1)
    if ($relativePath -match '[\r\n"$]') { throw "Unsupported payload filename: $relativePath" }
    $removeLines += 'Delete "$INSTDIR\' + $relativePath + '"'
}
$payloadDirs = Get-ChildItem -LiteralPath (Join-Path $sourceDir 'data_NewGameProject_windows_x86_64') -Recurse -Directory |
    Sort-Object { $_.FullName.Length } -Descending
foreach ($directory in $payloadDirs) {
    $relativePath = $directory.FullName.Substring($sourceDir.Length + 1)
    $removeLines += 'RMDir "$INSTDIR\' + $relativePath + '"'
}
$removeLines += 'RMDir "$INSTDIR\data_NewGameProject_windows_x86_64"'
Set-Content -LiteralPath $removeScript -Value $removeLines -Encoding UTF8
$sizeKiB = [int][math]::Ceiling(($payloadFiles | Measure-Object Length -Sum).Sum / 1KB)

& $CompilerPath /V3 /INPUTCHARSET UTF8 /OUTPUTCHARSET UTF8 "/DSOURCE_DIR=$sourceDir" "/DOUTPUT_FILE=$outputFile" "/DAPP_ICON=$appIcon" "/DAPP_VERSION=$Version" "/DAPP_VERSION_NUMERIC=$Version.0" "/DPAYLOAD_REMOVE_SCRIPT=$removeScript" "/DAPP_ESTIMATED_SIZE=$sizeKiB" (Join-Path $PSScriptRoot 'installer.nsi')
if ($LASTEXITCODE -ne 0) { throw "NSIS build failed: $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $outputFile)) { throw 'Installer output is missing.' }
Get-Item -LiteralPath $outputFile | Select-Object FullName, Length
Get-FileHash -LiteralPath $outputFile -Algorithm SHA256
