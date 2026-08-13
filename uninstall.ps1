$ErrorActionPreference = 'Stop'

$RunKey    = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$ValueName = 'MetroOsd'

if ($null -ne (Get-ItemProperty -LiteralPath $RunKey -Name $ValueName -ErrorAction SilentlyContinue)) {
    Remove-ItemProperty -LiteralPath $RunKey -Name $ValueName
    Write-Host "MetroOsd autostart disabled."
} else {
    Write-Host "MetroOsd autostart was not enabled."
}
Read-Host "Press Enter to exit"