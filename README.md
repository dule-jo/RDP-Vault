# RDP Vault

A simple Windows desktop app for organizing and launching multiple Remote Desktop (RDP) connections. Saved credentials are stored securely in Windows Credential Manager, not in plain text.

## Features

- Group your servers, mark favorites, and search across all of them
- Add / edit / delete server entries and groups
- Optionally save credentials per server — kept in Windows Credential Manager, never written to disk in plain text
- One-click connect via the standard Windows Remote Desktop client (`mstsc.exe`)
- Export / import your server list as JSON (passwords are never included in exports)
- Light and dark theme

## Download

Grab the latest installer from the [Releases](https://github.com/dule-jo/RDP-Vault/releases) page (`RdpVault-Setup.msi`). It's a self-contained installer — you don't need .NET installed separately.

Windows may show a SmartScreen warning ("Windows protected your PC") because the installer isn't code-signed. Click **More info → Run anyway** to continue.

## Building from source

Requirements: [.NET 10 SDK](https://dotnet.microsoft.com/download) on Windows.

```bash
git clone https://github.com/dule-jo/RDP-Vault.git
cd RDP-Vault
dotnet run --project src/RdpVault
```

### Building the installer

```bash
dotnet tool restore
dotnet publish src/RdpVault/RdpVault.csproj -p:PublishProfile=win-x64 -c Release
dotnet build installer/RdpVault.Installer/RdpVault.Installer.wixproj -c Release
```

The `.msi` is written to `installer/RdpVault.Installer/bin/x64/Release/RdpVault-Setup.msi`.

## License

MIT — see [LICENSE](LICENSE).
