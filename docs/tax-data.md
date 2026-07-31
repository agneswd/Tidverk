# Tax data

Tidverk bundles the official 2026 monthly salary tables for tables 29-42 and columns 1-6. It has no runtime network dependency.

Source title: `Allmanna tabeller manad 2026`

Source file: `allmanna-tabeller-manad.txt`

Refresh procedure:

1. Download the new year's official monthly TXT package from Skatteverket's technical-description page. Do not use PDF tables.
2. Run:

   ```bash
   dotnet run --project tools/Tidverk.TaxTableImporter -- input.txt src/Tidverk.Infrastructure/Tax/Data/tax-2027.json 2027 "Allmanna tabeller manad 2027"
   ```

3. Review the generated year, filename, title, import time, SHA-256 checksum, ranges, table coverage, and six values per range.
4. Add an official known-value test and run `scripts/verify.sh`.

Primary-income mode uses the user-entered table and column from their A-skattsedel. Secondary-income mode withholds 30 percent in whole kronor. Manual mode uses the entered fixed monthly deduction. Tidverk estimates preliminary withholding, not final annual tax.
