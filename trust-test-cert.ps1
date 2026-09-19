<#
.SYNOPSIS
    Trusts the MetroOsd self-signed code-signing certificate so the signed metro-osd.exe
    can start with uiAccess="true".

.DESCRIPTION
    Windows only honors uiAccess="true" when the executable's Authenticode signature
    chains to a root certificate the machine trusts. For the self-signed
    "MetroOsd Development (Test Only)" certificate this means importing it into:

      - LocalMachine\Trusted Root Certification Authorities
      - LocalMachine\Trusted Publishers

    SECURITY NOTE: this is a persistent, machine-wide trust change. Any binary signed
    with this key will be treated as a trusted publisher and may obtain UIAccess.
    Production releases should use a CA-issued code-signing certificate instead.
    Use -Remove to undo the change.

    Requires an elevated (Administrator) PowerShell session because it writes to the
    LocalMachine certificate stores.

.EXAMPLE
    .\trust-test-cert.ps1 -CertThumbprint 0A5CEAB0FE7E1DB8CA58512E314CE6B071DF2565
    .\trust-test-cert.ps1 -CertThumbprint 0A5CEAB0FE7E1DB8CA58512E314CE6B071DF2565 -Remove
#>
param(
    [Parameter(Mandatory = $true, HelpMessage = 'SHA1 thumbprint of the code-signing certificate')]
    [string]$CertThumbprint,

    [switch]$Remove
)

$ErrorActionPreference = 'Stop'

$cert = Get-ChildItem "Cert:\CurrentUser\My\$CertThumbprint" -ErrorAction SilentlyContinue
if (-not $cert) { $cert = Get-ChildItem "Cert:\LocalMachine\My\$CertThumbprint" -ErrorAction SilentlyContinue }
if (-not $cert) { throw "Certificate $CertThumbprint not found in CurrentUser\My or LocalMachine\My." }

$tmp = Join-Path $env:TEMP "metroosd-trust.cer"
try {
    if ($Remove) {
        Get-ChildItem "Cert:\LocalMachine\Root\$CertThumbprint" -ErrorAction SilentlyContinue | Remove-Item
        Get-ChildItem "Cert:\LocalMachine\TrustedPublisher\$CertThumbprint" -ErrorAction SilentlyContinue | Remove-Item
        Write-Host "Removed $($cert.Subject) from LocalMachine Trusted Root and Trusted Publishers." -ForegroundColor Green
    } else {
        Export-Certificate -Cert $cert -FilePath $tmp -Type CERT | Out-Null
        Import-Certificate -FilePath $tmp -CertStoreLocation Cert:\LocalMachine\Root | Out-Null
        Import-Certificate -FilePath $tmp -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null
        Write-Host "Imported $($cert.Subject) into LocalMachine Trusted Root and Trusted Publishers." -ForegroundColor Green
        Write-Host "metro-osd.exe should now start. Remember: this is a machine-wide trust change." -ForegroundColor Yellow
    }
}
finally {
    Remove-Item -LiteralPath $tmp -Force -ErrorAction SilentlyContinue
}
