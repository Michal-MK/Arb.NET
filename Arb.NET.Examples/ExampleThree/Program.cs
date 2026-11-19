using System.Globalization;
using ExampleThree.Localizations;

// Pluralization showcase: exact matches (=N), CLDR categories (zero/one/two/few/many/other),
// placeholders inside plural forms and nested plurals. See arbs/*.arb for the source strings.
int[] counts = [0, 1, 2, 5, 22, 25, 101];

foreach (string culture in new[] { "en", "cs", "pl" }) {
    AppLocale l = new(CultureInfo.GetCultureInfo(culture));

    Console.WriteLine($"=== {culture}: {l.AppTitle}");
    foreach (int n in counts) {
        Console.WriteLine($"  [{n,3}] {l.ItemCount(n)}");
        Console.WriteLine($"        {l.DueIn(n)}");
        Console.WriteLine($"        {l.InboxSummary(n, "Alice")}");
        Console.WriteLine($"        {l.FilesInFolders(n, n == 0 ? 0 : 1 + n / 10)}");
        Console.WriteLine($"        {l.CartSummary("Bob", n, (n * 9.99m).ToString("N2", CultureInfo.GetCultureInfo(culture)))}");
    }
    Console.WriteLine();
}
