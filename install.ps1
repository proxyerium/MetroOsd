$ErrorActionPreference = 'Stop'

$RunKey    = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$ValueName = 'MetroOsd'
$ExePath   = Join-Path $PSScriptRoot 'osd.exe'

if (-not (Test-Path -LiteralPath $ExePath -PathType Leaf)) {
    Write-Host "Error: osd.exe not found next to this script: $ExePath" -ForegroundColor Red
    Read-Host "Press Enter to exit"
    exit 1
}

$CommandLine = '"' + $ExePath + '"'
Set-ItemProperty -LiteralPath $RunKey -Name $ValueName -Value $CommandLine
Write-Host "MetroOsd autostart enabled: $CommandLine"
Read-Host "Press Enter to exit"