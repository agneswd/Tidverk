# Architecture

## Dependency direction

`Tidverk.App` depends on `Tidverk.Core`, `Tidverk.Infrastructure`, and the ShadUI Avalonia package. Infrastructure depends on Core. Core has no Avalonia, EF Core, or ClosedXML dependency.

## Domain and persistence

Core owns immutable work entries, settings, month records, calculations, holiday rules, tax contracts, and repository contracts. Infrastructure maps them to four SQLite tables: work entries keyed by date, singleton settings, month records keyed by year/month, and projects keyed by UUID. EF migrations run at startup. An existing database is backed up before a pending migration; five database backups are retained.

## UI state

`MainWindowViewModel` owns the current typed page, one selected month, and one set of day models. Avalonia data templates map page records to `UserControl` views through a `TransitioningContentControl`. Ledger and Calendar are projections of the same month state and open the same editor. Saving commits through the repository before the UI says the edit is saved. Catch-up iterates the calculated missing-date list in chronological order.

## Tax pipeline

The importer reads Skatteverket's fixed-width monthly TXT, validates tables 29-42 and all six columns, records source metadata/checksum, and writes deterministic ordered JSON. The app loads bundled year files without network access. Missing years return an unavailable estimate and never fall back.

## Export pipeline

The view model builds a validated report request. An Avalonia StorageProvider abstraction chooses the destination. ClosedXML writes actual time values, guarded formulas, one valid row per calendar day, and the familiar Swedish totals. Salary and tax are not exported.

## Design system

ShadUI supplies the window chrome, sidebar, cards, badges, controls, icons, typography, semantic colors, interaction states, and theme dictionaries. Tidverk-specific calendar, ledger, editor-sheet, notice, and status treatments remain in `Tidverk.App/Styles`. These styles consume ShadUI resources such as `PrimaryColor`, `CardBackgroundColor`, `BorderColor`, and the notification color scale, so existing controls update with the active light or dark theme. See [design-system.md](design-system.md).
