<#
.SYNOPSIS
    Builds the MetroOsd MSI installer end-to-end: publish, sign, wix build, sign.

.DESCRIPTION
    1. Publishes a self-contained single-file metro-osd.exe (win-x64).
    2. Signs metro-osd.exe with the given code-signing certificate (required because the
       app manifest requests uiAccess="true").
    3. Builds the MSI with `wix build`.
    4. Signs the MSI with the same certificate.

    The MSI version stays in sync with the git tag: when -Version is omitted it is
    derived from the most recent tag (e.g. v0.2 -> 0.2.0). The major version is kept
    at 0 (0.x.y). Pass -Version to override.

    For local testing a self-signed certificate can be used, but the certificate
    must be imported into the machine's Trusted Root and Trusted Publishers stores
    for Windows to honor uiAccess. Production releases should use a CA-issued
    code-signing certificate.

.EXAMPLE
    .\build-msi.ps1 -CertThumbprint 0A5CEAB0FE7E1DB8CA58512E314CE6B071DF2565
    .\build-msi.ps1 -CertThumbprint 0A5CEAB0FE7E1DB8CA58512E314CE6B071DF2565 -Version 0.2.0
#>
param(
    [Parameter(Mandatory = $true, HelpMessage = 'SHA1 thumbprint of the code-signing certificate')]
    [string]$CertThumbprint,

    [string]$Version,

    [switch]$SkipTimestamp
)

$ErrorActionPreference = 'Stop'

$root       = $PSScriptRoot
$outDir     = Join-Path $root 'bin\publish\osd'
$wxs        = Join-Path $root 'MetroOsd.wxs'
$signtool   = 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe'
$timestamp  = 'http://timestamp.digicert.com'

if (-not (Test-Path -LiteralPath $signtool)) {
    throw "signtool.exe not found at: $signtool`nInstall the Windows SDK or update the path in this script."
}

# --- Resolve the MSI version -------------------------------------------------
if ([string]::IsNullOrEmpty($Version)) {
    Push-Location $root
    try {
        $tag = & git describe --tags --abbrev=0 2>$null
        if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($tag)) {
            $Version = $tag -replace '^[vV]', ''
            Write-Host "Version derived from git tag: $tag -> $Version" -ForegroundColor Cyan
        }
    }
    finally {
        Pop-Location
    }
}

if ([string]::IsNullOrEmpty($Version)) {
    throw 'Cannot determine version: no git tag found. Pass -Version explicitly.'
}

# Normalize to x.y.z (Windows Installer requires 3 components): v0.2 -> 0.2.0
$parts = $Version.Split('.')
if ($parts.Count -lt 3) {
    $Version = (($parts + @('0', '0', '0'))[0..2]) -join '.'
}
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Invalid MSI version: '$Version'. Expected x.y.z (e.g. 0.2.0)."
}
Write-Host "MSI version: $Version"

$msiOut     = Join-Path $root "bin\publish\MetroOsd-$Version.msi"
$publishDir = Split-Path $msiOut -Parent

# --- Build -------------------------------------------------------------------
Push-Location $root
try {
    New-Item -ItemType Directory -Force -Path $outDir, $publishDir | Out-Null

    # 1) Publish self-contained single-file metro-osd.exe
    Write-Host '[1/4] Publishing metro-osd.exe ...' -ForegroundColor Cyan
    dotnet publish MetroOsd.csproj -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none -p:DebugSymbols=false `
        -o $outDir
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    # 2) Sign metro-osd.exe (mandatory for uiAccess)
    Write-Host '[2/4] Signing metro-osd.exe ...' -ForegroundColor Cyan
    & $signtool sign /fd SHA256 /sha1 $CertThumbprint "$outDir\metro-osd.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Failed to sign metro-osd.exe.' }
    if (-not $SkipTimestamp) { & $signtool timestamp /tr $timestamp /td SHA256 "$outDir\metro-osd.exe" 2>$null | Out-Null }

    Write-Host "Note: uiAccess only works when the signing cert is trusted by the machine."
    Write-Host "      For a self-signed cert, run: .\trust-test-cert.ps1 -CertThumbprint $CertThumbprint"
    Write-Host "      (as Administrator) or use a CA-issued certificate." -ForegroundColor Yellow

    # 3) Build the MSI
    Write-Host '[3/4] Building MSI ...' -ForegroundColor Cyan
    $osdExeRel = $outDir
    if ($osdExeRel.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        $osdExeRel = $osdExeRel.Substring($root.Length).TrimStart('\')
    }
    $osdExeRel = Join-Path $osdExeRel 'metro-osd.exe'
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
