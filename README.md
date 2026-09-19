# MetroOsd

MetroOsd is a lightweight, metro-style on-screen display (OSD) for Windows 10.

It hooks specific keyboard events globally and shows an indicator, positioned relative to Windows' native OSD.

![preview](preview.webp)

## Requirements

- Windows 10
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — if you prefer non-SelfContained builds

## Usage

Download from the [Releases](https://github.com/proxyerium/MetroOsd/releases):

- `MetroOsd-<version>.msi` — installs `metro-osd.exe` to `%ProgramFiles%\MetroOsd` and adds a Start Menu shortcut.
- `MetroOsd-<version>-selfcontained.zip` — no runtime needed.
- `MetroOsd-<version>.zip` — smaller `metro-osd.exe`, requires the .NET 8 Desktop Runtime.

## Building from source

```powershell
# Debug
dotnet build

# SelfContained release
dotnet publish MetroOsd.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=none -p:DebugSymbols=false `
  -o bin\publish\selfcontained
```

### Build the MSI installer

Prerequisites: [WiX Toolset](https://wixtoolset.org/) (`dotnet tool install --global wix`), the Windows SDK (`signtool`), and a code-signing certificate.

```powershell
.\build-msi.ps1 -CertThumbprint <SHA1-thumbprint>
```

The script publishes a self-contained single-file `metro-osd.exe` to `bin\publish\osd\`, signs it (required for `uiAccess`), builds the MSI with `wix build`, and signs the MSI too. The final installer is written to `bin\publish\MetroOsd-<version>.msi`. The MSI installs per-machine to `%ProgramFiles%\MetroOsd` and supports major upgrades via a fixed `UpgradeCode`.

## Code signing and uiAccess

MetroOsd's application manifest (`app.manifest`) requests `uiAccess="true"`, which lets the overlay draw on top of elevated windows. Windows only allows `uiAccess` when the executable is Authenticode-signed with a certificate the machine trusts **and** is located in a protected directory such as `%ProgramFiles%` (the MSI installs there).


## Credits

- **[Microsoft.Windows.CsWin32](https://github.com/microsoft/CsWin32)** — source-generated Win32 P/Invoke bindings used for all native calls.
- **.NET / Windows Forms** — application framework ([.NET](https://dotnet.microsoft.com)).
- **Segoe MDL2 Assets** — icon glyphs used for indicators (Microsoft).
