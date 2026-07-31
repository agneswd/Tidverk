using System.Globalization;
using Tidverk.Infrastructure.Tax;

if (args.Length is < 3 or > 4 || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int year)) {
    Console.Error.WriteLine("Usage: Tidverk.TaxTableImporter <input.txt> <output.json> <year> [source-title]");
    return 2;
}

string title = args.Length == 4 ? args[3] : "Allmanna tabeller manad";
await SkatteverketTaxTableImporter.ImportFileAsync(args[0], args[1], year, title).ConfigureAwait(false);
Console.WriteLine($"Imported Skatteverket monthly tables for {year} to {args[1]}");
return 0;
