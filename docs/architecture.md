# Architecture

## Dependency direction

`Tidverk.App` depends on `Tidverk.Core`, `Tidverk.Infrastructure`, and the ShadUI Avalonia package. Infrastructure depends on Core. Core has no Avalonia, EF Core, or ClosedXML dependency.

## Domain and persistence

Core owns immutable work entries, settings, month records, calculations, holiday rules, tax contracts, and repository contracts. One file per concept: `WorkEntry`, `AppSettings`, `ExpectedHoursSettings`, `OvertimeCompensationSettings`, `MonthRecord`, `Project`, `TaxSettings`.

Infrastructure maps them to four SQLite tables: work entries keyed by date, singleton settings, month records keyed by year/month, and projects keyed by UUID. Each repository lives in its own file and translates between the row types in `Entities.cs` and the domain. Settings that cannot be read, such as a damaged weekday list or rate-band JSON, log a warning and fall back for that field rather than making the application unopenable.

EF migrations run at startup, and only when something is pending. An existing database is backed up before a pending migration; five database backups are retained, pruned by the timestamp in the filename.

The holiday service caches each year the first time it is asked about, because expected-workday and overtime rules query individual dates hundreds of times per month view.

## UI state

`MainWindowViewModel` owns the current typed page, one selected month, and one set of day models. It is one class split across partial files by concern: the shell and month navigation, the day editor and catch-up flow, the settings form, and export and database management. Its dependencies arrive as a single `ShellServices` record, which the container, the XAML previewer and the tests all build the same way.

Commands come from CommunityToolkit's `[RelayCommand]` generator. Two property styles are used deliberately: state the user edits is generated with `[ObservableProperty]`, and state only the shell may change keeps a private setter.

Avalonia data templates map page records to `UserControl` views through a `TransitioningContentControl`. Ledger and Calendar are projections of the same month state and open the same editor. Saving commits through the repository before the UI says the edit is saved. Catch-up iterates the calculated missing-date list in chronological order.

Every month a view opens needs an opening balance. `OpeningBalanceEstimator` replays earlier months forward, stopping at the first month whose opening balance the user set by hand, because nothing before that can change the result.

## Tax pipeline

The importer reads Skatteverket's fixed-width monthly TXT, validates tables 29-42 and all six columns, records source metadata/checksum, and writes deterministic ordered JSON. The app loads bundled year files without network access, indexing each year's brackets by table number so a lookup binary-searches one table. Missing years return an unavailable estimate and never fall back; the shell displays the corresponding `TaxUnavailableReason`.

## Export pipeline

The view model builds a validated report request. An Avalonia StorageProvider abstraction chooses the destination. ClosedXML writes actual time values, guarded formulas, one valid row per calendar day, and summary totals. Salary and tax are not exported.

## Design system

ShadUI supplies the window chrome, sidebar, cards, badges, controls, icons, typography, semantic colors, interaction states, and theme dictionaries. Tidverk-specific calendar, ledger, editor-sheet, notice, and status treatments remain in `Tidverk.App/Styles`. These styles consume ShadUI resources such as `PrimaryColor`, `CardBackgroundColor`, `BorderColor`, and the notification color scale, so existing controls update with the active light or dark theme. See [design-system.md](design-system.md).
