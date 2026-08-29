<#
.SYNOPSIS
    Builds the MetroOsd MSI installer end-to-end: publish, sign, wix build, sign.

.DESCRIPTION
    1. Publishes a self-contained single-file osd.exe (win-x64).
    2. Signs osd.exe with the given code-signing certificate (required because the
       app manifest requests uiAccess="true").
    3. Builds the MSI with `wix build`.
    4. Signs the MSI with the same certificate.

    For local testing a self-signed certificate can be used, but the certificate
    must be imported into the machine's Trusted Root and Trusted Publishers stores
    for Windows to honor uiAccess. Production releases should use a CA-issued
    code-signing certificate.

.EXAMPLE
    .\build-msi.ps1 -CertThumbprint 0A5CEAB0FE7E1DB8CA58512E314CE6B071DF2565 -Version 1.0.0
#>
param(
    [Parameter(Mandatory = $true, HelpMessage = 'SHA1 thumbprint of the code-signing certificate')]
    [string]$CertThumbprint,

    [string]$Version = '1.0.0',

    [switch]$SkipTimestamp
)

$ErrorActionPreference = 'Stop'

$root       = $PSScriptRoot
$outDir     = Join-Path $root 'bin\publish\osd'
$wxs        = Join-Path $root 'MetroOsd.wxs'
$msiOut     = Join-Path $root "bin\publish\MetroOsd-$Version.msi"
$publishDir = Split-Path $msiOut -Parent
$signtool   = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
$timestamp  = 'http://timestamp.digicert.com'

if (-not (Test-Path -LiteralPath $signtool)) {
    throw "signtool.exe not found at: $signtool`nInstall the Windows SDK or update the path in this script."
}

Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path $outDir, $publishDir | Out-Null

    # 1) Publish self-contained single-file osd.exe
    Write-Host '[1/4] Publishing osd.exe ...' -ForegroundColor Cyan
    dotnet publish MetroOsd.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none -p:DebugSymbols=false `
        -p:AssemblyName=osd -o $outDir
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    # 2) Sign osd.exe (mandatory for uiAccess)
    Write-Host '[2/4] Signing osd.exe ...' -ForegroundColor Cyan
    & $signtool sign /fd SHA256 /sha1 $CertThumbprint "$outDir\osd.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Failed to sign osd.exe.' }
    if (-not $SkipTimestamp) { & $signtool timestamp /tr $timestamp /td SHA256 "$outDir\osd.exe" 2>$null | Out-Null }

    Write-Host "Note: uiAccess only works when the signing cert is trusted by the machine."
    Write-Host "      For a self-signed cert, run: .\trust-test-cert.ps1 -CertThumbprint $CertThumbprint"
    Write-Host "      (as Administrator) or use a CA-issued certificate." -ForegroundColor Yellow

    # 3) Build the MSI
    Write-Host '[3/4] Building MSI ...' -ForegroundColor Cyan
    $osdExeRel = $outDir
    if ($osdExeRel.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        $osdExeRel = $osdExeRel.Substring($root.Length).TrimStart('\')
    }
    $osdExeRel = Join-Path $osdExeRel 'osd.exe'
    wix --acceptEula wix7 build $wxs -d Version=$Version -d OsdExe="$osdExeRel" -o $msiOut
    if ($LASTEXITCODE -ne 0) { throw 'wix build failed.' }

    # 4) Sign the MSI
    Write-Host '[4/4] Signing MSI ...' -ForegroundColor Cyan
    & $signtool sign /fd SHA256 /sha1 $CertThumbprint $msiOut
    if ($LASTEXITCODE -ne 0) { throw 'Failed to sign MSI.' }
    if (-not $SkipTimestamp) { & $signtool timestamp /tr $timestamp /td SHA256 $msiOut 2>$null | Out-Null }

    Write-Host "`nDone: $msiOut" -ForegroundColor Green
}
finally {
    Pop-Location
}







