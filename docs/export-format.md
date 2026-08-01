# Excel export

Choose **Export report** from a month to create `Tidverk_<employee>_YYYY-MM.xlsx`. Tidverk removes characters that are unsafe in filenames and lets you choose the destination.

## Employer month sheet

The first sheet contains one row for every calendar day in the selected month.

| Column | Content |
| --- | --- |
| A | Calendar day number |
| B | Start time |
| C | Stop time |
| D | Unpaid lunch duration |
| E | Total worked hours after unpaid lunch |
| F | Day-off status |
| G | Project name |

The summary shows separate totals for regular hours and overtime. Daily rows keep all worked time in one hours column.

## Personal time-balance sheet

The second sheet shows regular hours, overtime, actual worked time, expected time, the month's balance, opening balance, and closing balance. Comp-time overtime contributes to the time balance. Paid overtime stays separate from the time balance.

Salary, hourly rate, overtime premiums, and tax estimates are never included in the workbook.
