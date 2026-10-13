param([Parameter(Mandatory = $true)][int]$GenerationProcessId)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$audioOutput = Join-Path $projectRoot 'output/story_audio_generated'
$runtimePython = 'D:\Index-TTS-V31\dist\index-tts-windows-cu128-deepspeed\runtime\python.exe'
$godotConsole = 'C:\Program Files\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe'

Wait-Process -Id $GenerationProcessId -ErrorAction SilentlyContinue
$manifest = Get-Content -LiteralPath (Join-Path $audioOutput 'manifest.json') -Raw -Encoding utf8 | ConvertFrom-Json
$entries = @($manifest.PSObject.Properties.Value)
if ($entries.Count -ne 24 -or @($entries | Where-Object status -ne 'complete').Count -gt 0) {
    throw 'Generation stopped before all 24 entrance voice clips completed; no audio has been installed.'
}

& $runtimePython -X utf8 -u (Join-Path $PSScriptRoot 'install_story_audio.py')
if ($LASTEXITCODE -ne 0) { throw 'Entrance voice installation failed.' }

& $godotConsole --headless --path $projectRoot --editor --import --log-file (Join-Path $audioOutput 'godot_import.log')
if ($LASTEXITCODE -ne 0) { throw 'Godot audio import failed.' }

& $godotConsole --headless --path $projectRoot --editor --script 'res://output/story_audio_generated/validate_resources.gd' --log-file (Join-Path $audioOutput 'resource_validation.log')
if ($LASTEXITCODE -ne 0) { throw 'Godot entrance voice validation failed.' }

$reportPath = Join-Path $audioOutput 'integration_report.json'
$report = Get-Content -LiteralPath $reportPath -Raw -Encoding utf8 | ConvertFrom-Json
$report | Add-Member -NotePropertyName status -NotePropertyValue 'complete' -Force
$report | Add-Member -NotePropertyName godot_resources -NotePropertyValue '24/24 readable, positive duration, no loop' -Force
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportPath -Encoding utf8
Write-Output 'COMPLETE: all 24 entrance voice clips installed and verified.'
