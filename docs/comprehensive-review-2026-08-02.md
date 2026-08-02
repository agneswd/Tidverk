# Comprehensive product and engineering review

Review date: 2026-08-02  
Baseline: `3925fc6f11b27e97c2baa73ce1cd9dec4164ef47` (`feat(payroll): support shift work, overtime, and OB`)

## Executive decision

Tidverk has a sound small-application shape: a local Avalonia client, a calculation-focused Core project, and SQLite persistence behind repository interfaces. It is useful today for one employment arrangement with hourly or monthly pay, one shift per day, a repeating weekly schedule, daily overtime, and configurable time-window premiums.

The main product risk is trust, not missing feature count. Current settings are mutable global policy. Editing them recalculates old periods, lunch is a duration without a real position in the shift, and one date can hold only one entry. Those constraints make several displayed results impossible to reproduce or explain once a user's employment terms change.

The recommended direction is a small versioned employment-policy model, explicit worked and break intervals, and a calculation trace. Multiple employers, split shifts, irregular rosters, and typed absence should build on that foundation. A generic payroll rules engine, full event sourcing, and certified tax or payroll behavior should not be built.

## Review method

The Sol orchestrator established a shared repository briefing, then delegated ten independent read-only reviews covering architecture, compensation research, calculation correctness, employment personas, UX and accessibility, framework quality, local data safety, testing, operational quality, and adversarial analysis. All worker requests explicitly selected `gpt-5.6-luna` with Max reasoning and prohibited recursive delegation. The spawning interface accepted those settings but returned only worker IDs and names, so it did not provide separate effective-model metadata to record.

Every accepted material finding was checked against the referenced implementation and tests. The baseline passed `scripts/verify.sh`: 50 App tests, 85 Core tests, and 37 Infrastructure tests, 172 total, with no build warnings or errors.

## Current architecture

- `.NET 10`, C#, Avalonia 12, CommunityToolkit.Mvvm, ShadUI, EF Core SQLite, ClosedXML, Velopack, and xUnit v3.
- `Tidverk.Core` owns time, schedule, compensation, tax-result, and monthly-calculation concepts.
- `Tidverk.Infrastructure` owns SQLite repositories and migrations, backup and restore, tax-table data, and Excel export.
- `Tidverk.App` owns the Avalonia shell, settings and editor forms, navigation, localization, and application services.
- `WorkEntry` is keyed by `DateOnly` and stores one start, one end, and one lunch duration.
- `AppSettings` is the single current compensation and schedule policy. There are no effective dates or historical snapshots.
- Monthly calculation loads entries and a month record, applies current settings, and derives time, balance, gross pay, premiums, overtime, and estimated tax.

## Verified high-priority findings

### SOL-001: Current policy rewrites historical meaning

Severity: High  
Classification: Essential foundation

`AppSettings` stores one active schedule, salary, overtime, and premium configuration. `MonthlyWorkspaceService.LoadAsync` and `OpeningBalanceEstimator` apply that current object to any requested month. A rate, workweek, premium, or overtime edit therefore changes old results and may also change balances carried into later months.

Recommendation: add immutable, effective-dated employment-policy versions and associate calculations with the version effective for each interval. Preserve imported legacy history under one explicit legacy policy. Do not copy all settings into every entry.

### SOL-002: A lunch duration cannot locate unpaid time

Severity: High  
Classification: Essential foundation

`WorkEntry` records only `LunchMinutes`. Premium and overtime calculations must invent where that time occurred. A 30-minute break during an evening premium can produce a different salary from a 30-minute break before the premium, even though both entries are identical in storage.

Recommendation: represent one or more explicit break intervals with paid or unpaid status. Keep the duration-only field as a legacy input and map it with a visible "time unknown" assumption until the user edits the entry.

### SOL-003: One row per date blocks common work patterns

Severity: High  
Classification: Common configurable capability

`WorkEntries.Date` is the primary key. Split shifts, two jobs on one day, and separate on-call and active-work intervals overwrite or collapse into one entry.

Recommendation: introduce stable entry IDs and allow multiple shifts per date after employment arrangements and interval calculations exist. Keep the simple editor's default path at one shift.

### SOL-004: Restore trusted table names instead of the current model

Severity: Critical  
Classification: Essential foundation

The restore path copied a candidate, ran `PRAGMA quick_check`, and checked five table names before replacing the live database. An older valid backup could pass and then fail because it lacked current columns. A fabricated SQLite file could also pass the name check.

Decision implemented in this branch: migrate a private candidate, reject unknown future migration IDs, execute current-model reads against every table, and only then create a safety backup and replace the live file.

### SOL-005: Late month loads could replace current UI state

Severity: High  
Classification: Essential foundation

Rapid previous and next navigation started overlapping loads. The slower first request could update entries and totals after the selected month had changed, producing a month title and export target that disagreed with the visible data.

Decision implemented in this branch: each load captures its month and generation. Only the latest generation may apply results.

### SOL-006: Sunday schedules were silently changed

Severity: High  
Classification: Common configurable capability

The Swedish holiday calendar correctly includes Sundays under the statutory holiday definition. The UI allowed Sunday as a working weekday, but settings creation always forced public-holiday exclusion. A regular Sunday worker could therefore receive zero expected hours and an incorrect scheduled-hours overtime threshold.

Decision implemented in this branch: expose and preserve the existing `ExcludePublicHolidays` policy in setup and employment settings. It defaults to the current simple weekday behavior.

### SOL-007: Setup accepted incomplete identity and invalid default breaks

Severity: High  
Classification: Essential foundation

Employee, employer, and default project were not required by form validation, and default lunch was not checked against default shift length before persistence.

Decision implemented in this branch: validate all required identity fields and require a non-negative default break shorter than the shift before any write.

### SOL-008: Backup names could collide

Severity: Medium  
Classification: Essential foundation

Backup names had one-second precision. Repeating the same backup reason within a second reused the same path.

Decision implemented in this branch: retain chronological UTC names while adding fractional seconds and a unique suffix.

## Employment capability matrix

| Scenario | Current status | Product decision |
| --- | --- | --- |
| One fixed hourly rate | Supported | Keep the default setup path |
| Monthly salary and employment percentage | Supported with estimate limitations | Common configurable capability |
| Rates or salary changing over time | Unsafe for history | Essential policy-version foundation |
| Different jobs or employers | Unsupported | Common capability after policy versioning |
| Scheduled versus worked time | Partially supported | Preserve, then add roster exceptions |
| Minimum paid shift | Unsupported | Advanced optional capability |
| Evening, night, weekend, and holiday windows | Partially supported | Common capability; improve trace and break handling |
| Windows crossing midnight | Supported for civil clock intervals | Add boundary fixtures; document DST limitation |
| Additive OB and overtime | Configurable | Preserve current explicit mode |
| Daily overtime threshold | Supported | Preserve |
| Weekly overtime threshold | Unsupported | Common capability after interval model |
| Part-time additional hours | Treated as overtime | Common capability after employment policy model |
| Approved overtime | Unsupported | Advanced optional capability |
| Compensation time | Supported as a balance mode | Add agreement-specific multipliers later if needed |
| Repeating weekday schedule | Supported | Preserve simple default |
| Public-holiday schedule choice | Supported by this branch | Common configurable capability |
| Irregular or rotating roster | Unsupported | Common capability after dated schedule overrides |
| Overnight shift | Supported with one next-day boundary | Add DST and pay-period boundary policy later |
| Split shifts or multiple shifts per day | Unsupported by primary key | Common capability after stable entry IDs |
| Unpaid break duration | Partially supported | Replace with explicit intervals |
| Multiple paid or unpaid breaks | Unsupported | Common capability with interval model |
| Vacation, sickness, parental leave | Only generic day off | Common typed absence, staged later |
| Multiple currencies | One display currency at a time | Future extension per employment arrangement |
| Different pay periods | Calendar month only | Common capability after policy versioning |
| Historical reproducibility | Unsupported | Essential foundation |
| Backup and restore | Supported and hardened by this branch | Keep local and user-controlled |
| Import | Unsupported | Common capability after a versioned interchange format |
| Tax estimate | Swedish 2026 tables with explicit estimate language | Keep jurisdiction-specific and never present as payroll certification |

## Prioritized roadmap

### P0: Protect current users

Implemented in this branch:

1. Stage, migrate, and fully validate restore candidates before live replacement.
2. Ignore stale month loads.
3. Preserve the user's public-holiday schedule choice.
4. Validate required setup identity and default break duration before writes.
5. Make every backup path unique.

These changes have high evidence, low migration risk, and direct leverage on calculation and data trust.

### P1: Make each calculation explainable

1. Replace duration-only lunch with explicit paid and unpaid break intervals.
2. Return a calculation trace containing source intervals, applied rules, precedence, quantities, rates, and rounding.
3. Define one monetary precision and rounding contract, including the point at which displayed totals are rounded.
4. Add golden fixtures for midnight, period boundaries, overlapping OB and overtime, public holidays, and invalid or missing inputs.

This is the next implementation milestone. It prevents silent disagreement between what a user entered and what Tidverk paid.

### P2: Preserve employment history

1. Add `EmploymentArrangement` for employer, job, currency, pay period, and active state.
2. Add immutable `EmploymentPolicyVersion` records with effective-from timestamps for salary, expected schedule, overtime, and premiums.
3. Resolve a shift interval against the policy effective for that interval, including a shift crossing a policy boundary.
4. Record finalized period outputs or a compact balance ledger so later setting edits cannot rewrite an approved result.
5. Migrate existing data into one default arrangement and one legacy policy without inventing unknown historical dates.

This is architectural work. It should be designed and migrated as one coherent change, not added as nullable identifiers throughout the current schema.

### P3: Broaden common employment support

1. Move work entries to stable IDs and permit multiple shifts per date.
2. Add dated schedule overrides for irregular and rotating rosters.
3. Distinguish part-time additional hours from overtime.
4. Add typed paid and unpaid absence.
5. Add configurable pay-period boundaries and import/export round trips.

Progressive disclosure should keep one employer, one shift, and a weekday schedule as the initial experience.

### P4: Optional extensions

- Weekly thresholds and agreement-specific comp-time multipliers.
- On-call and standby intervals.
- Custom holiday calendars and agreement-specific major-holiday presets.
- Explicit time-zone and DST policies where elapsed-time accuracy is required.
- Multiple active currencies, isolated by employment arrangement.

## Recommendations rejected

- Change the OB migration default to additive. Rejected because the released pre-feature calculation applied OB only to regular minutes. `ExcludeOb` preserves released behavior; an unmerged intermediate commit was not the compatibility baseline.
- Build a generic rules engine. Rejected because typed overtime, premium, schedule, and break policies are easier to validate and explain.
- Adopt full event sourcing. Rejected because immutable policy versions plus finalized calculation records provide the required history with less operational cost.
- Encrypt the database by default. Rejected without a defined threat model. OS account protection and disk encryption address the common local-device risk without introducing key-loss and recovery hazards. Optional application encryption can be reconsidered for shared-device scenarios.
- Virtualize the calendar immediately. Rejected because no measured rendering bottleneck exists for one month.
- Target an arbitrary coverage percentage. Rejected in favor of calculation fixtures, migration tests, round trips, and critical UI workflows.
- Implement a universal Swedish collective-agreement catalog. Rejected because collective agreements differ. Tidverk should offer configurable policies and clearly labeled presets, not claim one agreement is statutory law.

## Jurisdiction boundaries

Universal concepts belong in Core: intervals, schedules, policy effective dates, money, rounding, premiums, overtime, and calculation traces. Swedish statutory holidays and tax-table lookup should remain replaceable jurisdiction modules. Collective-agreement concepts such as OB windows, major-holiday periods, additional-hours treatment, and overtime multipliers must be configurable and labeled by source.

Swedish law defines general working-time limits and recordkeeping duties, but collective agreements can replace parts of the Working Hours Act. Sundays and named days are public holidays under the Public Holidays Act. Neither source creates one universal OB schedule. Tax tables depend on current official data and user circumstances, so Tidverk's net result must remain an estimate.

## Authoritative sources

Accessed 2026-08-02:

- [Swedish Working Hours Act (1982:673), Sveriges riksdag](https://www.riksdagen.se/sv/dokument-och-lagar/dokument/svensk-forfattningssamling/arbetstidslag-1982673_sfs-1982-673/)
- [Working Hours Act guidance, Swedish Work Environment Authority](https://www.av.se/arbetsmiljoarbete-och-inspektioner/lagar-och-regler-om-arbetsmiljo/om-arbetstidslagen/)
- [Records of on-call time, overtime, and additional hours, Swedish Work Environment Authority](https://www.av.se/arbetsmiljoarbete-och-inspektioner/arbetsgivarens-ansvar-for-arbetsmiljon/anteckna-uppgifter-om-jourtid-overtid-och-mertid/)
- [Public Holidays Act (1989:253), Sveriges riksdag](https://www.riksdagen.se/sv/dokument-och-lagar/dokument/svensk-forfattningssamling/lag-1989253-om-allmanna-helgdagar_sfs-1989-253/)
- [2026 tax tables, Swedish Tax Agency](https://www.skatteverket.se/foretag/arbetsgivare/arbetsgivaravgifterochskatteavdrag/skattetabeller.4.96cca41179bad4b1aa8a46.html)
- [Swedish Tax Agency legal guidance, 2026 edition](https://www4.skatteverket.se/rattsligvagledning/edition/2026.8/325047.html)
- [WCAG 2.2, W3C](https://www.w3.org/TR/WCAG22/)
- [EF Core migrations, Microsoft](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [SQLite Online Backup API](https://sqlite.org/backup.html)
- [SQLite integrity and quick checks](https://sqlite.org/pragma.html#pragma_quick_check)

## Verification strategy

Keep the existing unit and integration suite, then add:

- Table-driven and property-based interval tests for boundaries and invariants.
- Golden calculation fixtures that show every input and pay line.
- Migration tests from every released schema plus rejection of unknown future schemas.
- Backup and import/export round trips with malformed and interrupted inputs.
- Headless UI tests for setup, keyboard and screen-reader names, destructive confirmations, settings persistence, and rapid navigation.
- Performance measurements only around observed risks: opening-balance replay over long histories, startup migration, and large entry lists.
