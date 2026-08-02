# Tidverk

Tidverk is a local-first desktop timesheet for Windows and Linux. Record workdays, track regular and overtime hours, estimate pay and preliminary tax, and export an employer-ready Excel report without creating an account.

## Features

- List and calendar views for monthly time entries
- Configurable workweek, normal hours, overtime rules, hourly rate, and currency
- Comp-time or paid-overtime calculation
- Preliminary tax estimates using bundled official tax tables
- Local SQLite storage with backup and restore tools
- Light, dark, and system themes with adjustable interface scale
- Automatic updates from GitHub Releases

## Install

Download the latest files from [GitHub Releases](https://github.com/agneswd/Tidverk/releases/latest). Tidverk includes .NET, so no separate runtime installation is required.

### Windows 10 or later

Download and run the `Tidverk-*-Setup.exe` file. Velopack installs Tidverk for the current user, adds shortcuts, and registers it in Windows Installed apps.

Windows may show a SmartScreen warning until the unsigned installer has established reputation. Open "More info" and choose "Run anyway" if you downloaded the file from this repository.

### Linux x64

Run the AppImage directly:

```bash
chmod +x Tidverk-*.AppImage
./Tidverk-*.AppImage
```

For application-menu integration, download `Tidverk-<version>-linux-x64.tar.gz`, extract it, and run:

```bash
./install.sh
```

The installer places the AppImage under `~/.local/opt/tidverk` without root access. Run `./uninstall.sh` from the extracted bundle to remove application files while keeping your data.

Avalonia uses X11 or XWayland on Linux by default. A desktop installation also needs the standard fontconfig and X11 runtime libraries supplied by most distributions.

## Updates

An installed copy checks GitHub once after startup. Tidverk downloads an available update in the background and shows progress above Settings in the sidebar. When the download finishes, choose "Restart now" or continue working. A downloaded update installs before the next launch if you choose "Later".

Development builds run without update support and do not report an error.

## Privacy and local data

Tidverk has no accounts, cloud storage, telemetry, timer, or activity monitoring. Entries are saved to a local SQLite database. The app contacts GitHub after startup to check for application updates, but it does not send timesheet data.

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

Create a Velopack package for the current operating system:

```bash
scripts/package-linux-x64.sh
scripts/package-win-x64.sh
```

Packages are written to `artifacts/releases`. GitHub Actions builds and verifies native Windows and Linux packages before publishing a tagged release.

## Technical documentation

- [Architecture](docs/architecture.md)
- [Comprehensive review and roadmap](docs/comprehensive-review-2026-08-02.md)
- [UI system](docs/design-system.md)
- [Excel export](docs/export-format.md)
- [Tax estimates](docs/tax-data.md)

## License

Tidverk is available under the [MIT License](LICENSE).
