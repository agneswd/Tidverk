# Tax estimates

Tidverk can estimate Swedish preliminary monthly tax without sending salary information over the internet. The app bundles the official 2026 monthly tables 29-42 and columns 1-6 from Skatteverket.

## Choose the correct setting

- **Swedish tax table**: enter the table and column shown on your A-tax certificate. Column 1 normally applies to salary from a main employer.
- **Secondary income - 30%**: estimates a 30 percent deduction for secondary employment.
- **Manual monthly deduction**: uses the fixed monthly amount you enter.
- **Disabled**: hides the net estimate.

Use Skatteverket's service linked from Settings if you do not know your table. Tidverk reports an unavailable estimate when the selected year's data is missing instead of guessing.

Tax estimates are for planning only. Your employer's payroll calculation and final annual tax assessment remain authoritative.

## Updating bundled tables

Maintainers should download Skatteverket's fixed-width monthly TXT package, not the PDF tables, then import the new year:

```bash
dotnet run --project tools/Tidverk.TaxTableImporter -- input.txt src/Tidverk.Infrastructure/Tax/Data/tax-2027.json 2027 "Allmanna tabeller manad 2027"
```

Review the generated source metadata and checksum, add a known-value test, and run `scripts/verify.sh` before publishing.
