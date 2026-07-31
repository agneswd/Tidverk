# Tidverk

Tidverk is a local-first Linux desktop application for entering previous workdays, tracking hour balance, estimating salary and Swedish preliminary tax, and exporting a familiar employer-facing Excel report.

Ledger is the default view. Calendar shows the same month and opens the same editor. Data is stored only on the local computer; there are no accounts, cloud services, telemetry, timers, or activity monitoring.

The UI is built on [ShadUI](https://github.com/accntech/shad-ui), an Avalonia implementation of the shadcn visual system. Tidverk uses its native window, sidebar, cards, badges, controls, icons, typography, and light/dark tokens. Local styles are limited to timesheet-specific layout and state presentation. See [design-system.md](docs/design-system.md).

## Requirements

- .NET SDK 10.0.110 or a compatible .NET 10 patch
- A current Linux desktop with X11 or XWayland, Windows 10 or later, or a supported macOS release

## Develop and verify

```bash
scripts/run-linux.sh
scripts/verify.sh
```

The test runner opens a local loopback test-host socket. Sandboxed environments must permit that local socket.

## Local data

On Linux, Tidverk uses `${XDG_DATA_HOME:-$HOME/.local/share}/Tidverk`:

- `tidverk.db` - SQLite database
- `backups/` - migration and manual database backups (five retained)
- `logs/` - rolling local logs (seven days retained)

Committed edits are saved immediately. Export files go only to the location selected in the platform file picker.

## Tax disclaimer

Tax is an estimate of preliminary withholding, not guaranteed take-home pay or a final annual tax calculation. Primary-income mode uses the official bundled Skatteverket table/year/column selected by the user. If a year is missing, Tidverk shows that the estimate is unavailable and does not guess. See [tax-data.md](docs/tax-data.md) for updates.

## Excel export

Preview the selected month and choose `Export Excel`. The workbook contains time-report fields and balance totals, but not salary or tax. See [export-format.md](docs/export-format.md).

## Publish and install on Linux

```bash
scripts/publish-linux-x64.sh
packaging/linux/install-user.sh
```

This creates a self-contained, multi-file `linux-x64` publish and installs it beneath `~/.local/opt/tidverk` with a desktop entry and icon. No root access is required. Uninstall with `packaging/linux/uninstall-user.sh`.

## Publish for Windows and macOS

```bash
scripts/publish-win-x64.sh
scripts/publish-osx-x64.sh
scripts/publish-osx-arm64.sh
```

These commands create self-contained outputs under `artifacts/publish/<rid>`. Linux has a user installer; Windows and macOS currently ship as unpackaged publish directories without signing or platform installers.

## Screenshots

Render UI states off-screen without opening a desktop window:

```bash
TIDVERK_SNAPSHOT_DIR="$PWD/artifacts/ui-snapshots" \
  dotnet test tests/Tidverk.App.Tests/Tidverk.App.Tests.csproj -c Release \
  --filter FullyQualifiedName~Ui_surfaces_render_to_headless_snapshots_when_requested
```

The generated PNGs cover ledger, calendar, editor, first-run setup, settings, catch-up, report, and dark ledger states.
