# Tidverk

Tidverk is a private, local-first desktop timesheet for Windows and Linux. Record workdays, track regular and overtime hours, estimate pay and Swedish preliminary tax, and export an employer-ready Excel report without creating an account.

## Features

- List and calendar views for monthly time entries
- Configurable workweek, normal hours, overtime rules, hourly rate, and currency
- Comp-time or paid-overtime calculation
- Swedish and English interface and Excel export
- Swedish preliminary tax estimates using bundled official tax tables
- Local SQLite storage with backup and restore tools
- Light, dark, and system themes with adjustable interface scale

## Install

Tidverk packages are self-contained, so users do not need to install .NET.

### Windows 10 or later

1. Download and extract `Tidverk-<version>-win-x64.zip`.
2. Open PowerShell in the extracted folder.
3. Run:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\install.ps1
   ```

Tidverk appears in the Start menu and in Windows Installed apps. Uninstall it from Windows Settings. Your database and exported reports are kept when the app is removed.

### Linux x64

1. Download `Tidverk-<version>-linux-x64.tar.gz`.
2. Extract and install it:

   ```bash
   tar -xzf Tidverk-*-linux-x64.tar.gz
   cd Tidverk-*-linux-x64
   ./install.sh
   ```

The installer adds Tidverk to the desktop application menu without root access. Run `./uninstall.sh` from the extracted package to remove the application while keeping your data.

Avalonia uses X11 or XWayland on Linux by default. A normal desktop installation also needs the standard fontconfig and X11 runtime libraries supplied by most distributions.

## Privacy and local data

Tidverk has no accounts, cloud service, telemetry, timer, or activity monitoring. Entries are saved immediately to a local SQLite database.

- Windows: `%LOCALAPPDATA%\Tidverk`
- Linux: `${XDG_DATA_HOME:-$HOME/.local/share}/Tidverk`
- Excel reports: the folder selected in the export dialog

The data directory contains `tidverk.db`, rolling local logs, and database backups. Tidverk retains five migration or manual backups and seven days of logs.

## Excel and tax

The Excel workbook contains an employer-facing month sheet plus a personal time-balance sheet. Salary and tax values stay out of the workbook. See [Excel export](docs/export-format.md).

Tax values are estimates of preliminary withholding, not guaranteed take-home pay or a final annual tax calculation. See [Tax estimates](docs/tax-data.md).

## Build and verify

Requirements:

- .NET SDK 10.0.110 or a compatible .NET 10 patch
- Linux, Windows, or macOS development environment supported by Avalonia

```bash
scripts/verify.sh
scripts/run-linux.sh
```

Create installable release archives:

```bash
scripts/package-linux-x64.sh
scripts/package-win-x64.sh
```

Packages and SHA-256 checksums are written to `artifacts/packages`. GitHub Actions verifies the solution on Windows and Linux and builds both archives.

## Technical documentation

- [Architecture](docs/architecture.md)
- [UI system](docs/design-system.md)
- [Excel export](docs/export-format.md)
- [Tax estimates](docs/tax-data.md)
