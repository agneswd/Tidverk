# Excel export

Choose **Export report** from a month to create `Tidverk_<employee>_YYYY-MM.xlsx`. Tidverk removes characters that are unsafe in filenames and lets you choose the destination.

The export language can follow the operating system or be fixed to Swedish or English in Settings.

## Employer month sheet

The first sheet contains one row for every calendar day in the selected month.

| Column | English heading | Swedish heading | Content |
| --- | --- | --- | --- |
| A | Day | Dag | Calendar day number |
| B | Start | Start | Start time |
| C | Stop | Slut | Stop time |
| D | Lunch | Lunch | Unpaid lunch duration |
| E | Hours | Timmar | Regular hours, capped at the configured daily threshold |
| F | Overtime | Övertid | Hours above the daily threshold |
| G | Status | Status | Day-off status |
| H | Project | Projekt | Project name |

The summary shows total regular hours. Overtime remains visible in its own column so the employer can see the complete workday without mixing those hours into regular salary.

## Personal time-balance sheet

The second sheet shows regular hours, overtime, actual worked time, expected time, the month's balance, opening balance, and closing balance. Comp-time overtime contributes to the time balance. Paid overtime stays separate from the time balance.

Salary, hourly rate, overtime premiums, and tax estimates are never included in the workbook.
