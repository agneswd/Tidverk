# Excel export mapping

The filename is `Tidverk_<employee>_YYYY-MM.xlsx` with invalid filename characters sanitized. The workbook contains an employer-facing month sheet and a separate personal `Tidsbalans` sheet.

| Column | Workbook heading | Source |
| --- | --- | --- |
| A | Day | Actual calendar day number |
| B | Start | Excel time value for worked days |
| C | Stop | Excel time value for worked days |
| D | Lunch | Excel duration value |
| E | Timmar kund | Guarded worked-hours formula |
| F | Status | `Ledig` for off days |
| G | Projektnamn | Work-entry project |

The employer-facing month sheet shows `Totalt ordinarie timmar`. A worked day contributes at most the configured daily hours to this paid total. The detailed rows still show every actual worked hour, while overtime totals stay off this sheet.

The personal `Tidsbalans` sheet shows ordinary time, overtime, actual worked time, expected time, monthly balance, opening balance, and closing balance. Shorter days and days off affect the time bank without being confused with overtime pay. Incomplete rows leave time cells empty, and the customer-hours formula explicitly returns empty if start or stop is absent. Only valid days for the selected month are generated. Salary and tax remain private and are excluded.
