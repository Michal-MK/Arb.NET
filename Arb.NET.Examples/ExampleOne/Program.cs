using System.Runtime.CompilerServices;
using Arb.NET.Example.Localizations;

namespace Arb.NET.ExampleOne;

internal static class Program {
    private static void Main() {
        // ── 1. Runtime: parse an .arb file and inspect its contents ──────────
        Console.WriteLine("=== Runtime parse ===");

        var arbFilePath = Path.Combine(AppContext.BaseDirectory,
                                       "..", "..", "..", "arbs", "app_en.arb");

        var result = new ArbParser().Parse(arbFilePath);
        if (!result.ValidationResults.IsValid) {
            Console.WriteLine("Validation errors:");
            foreach (var error in result.ValidationResults.Errors)
                Console.WriteLine($"  [{error.Keyword}] {error.Message}, instance at {error.InstanceLocation}, schema path {error.SchemaPath}");
            return;
        }

        var document = result.Document!;

        Console.WriteLine($"Locale : {document.Locale}");
        Console.WriteLine($"Entries: {document.Entries.Count}");
        foreach (var entry in document.Entries.Values)
            Console.WriteLine($"  {entry.Key} = \"{entry.Value}\"");

        // ── 2. Runtime: generate C# source and write it out ──────────────────
        Console.WriteLine("\n=== Code generation (runtime) ===");

        var outputPath = Path.Combine(AppContext.BaseDirectory, "Generated", "AppLocalizations.cs");
        new ArbCodeGenerator().GenerateClassFile(
            arbFilePath, outputPath,
            className: "AppLocalizations",
            namespaceName: "Arb.NET.Tool"
        );

        Console.WriteLine($"Written: {outputPath}");

        // ── 3. Source generator: use the compile-time generated class ─────────
        // AppLocalizations is emitted by Arb.NET.Generators at build time
        // from Examples/app_en.arb.  No runtime loading needed.
        Console.WriteLine("\n=== Source-generated class ===");

        Console.WriteLine($"AppTitle       : {AppLocale_cs.AppTitle}");
        Console.WriteLine($"WelcomeMessage : {AppLocale_cs.WelcomeMessage("Alice")}");

        // ── 4. Dispatcher: route by CultureInfo ───────────────────────────────
        Console.WriteLine("\n=== Dispatcher class ===");

        AppLocale csLocale = new(System.Globalization.CultureInfo.GetCultureInfo("cs"));
        Console.WriteLine($"[cs] AppTitle       : {csLocale.AppTitle}");
        Console.WriteLine($"[cs] WelcomeMessage : {csLocale.WelcomeMessage("Alice")}");
        Console.WriteLine($"[cs] ItemCount(0)   : {csLocale.ItemCount(0)}");

        AppLocale enLocale = new(System.Globalization.CultureInfo.GetCultureInfo("en"));
        Console.WriteLine($"[en] AppTitle       : {enLocale.AppTitle}");
        Console.WriteLine($"[en] WelcomeMessage : {enLocale.WelcomeMessage("Alice")}");
        Console.WriteLine($"[en] ItemCount(5)   : {enLocale.ItemCount(5)}");

        // en-US falls back to en (sub-culture → parent fallback)
        AppLocale enUsLocale = new(System.Globalization.CultureInfo.GetCultureInfo("en-US"));
        Console.WriteLine($"[en-US] AppTitle       : {enUsLocale.AppTitle}");
        Console.WriteLine($"[en-US] WelcomeMessage : {enUsLocale.WelcomeMessage("Alice")}");
        
        Console.WriteLine($"[en-US] LongText       : {enUsLocale.LongText}");
    }
}