namespace Arb.NET.Test;

[TestFixture]
public class ArbGeneratorTests {
    [Test]
    public void GenerateClass_ProducesCorrectSource() {
        ArbDocument document = new() {
            Locale = "en",
            Entries = new Dictionary<string, ArbEntry> {
                {
                    "key", new ArbEntry {
                        Key = "key",
                        Value = "A parameter: '{param}' is here.",
                    }
                }
            }
        };

        string generatedCode = new ArbCodeGenerator().GenerateClass(document, "AppLocalizations", "MyApp.Localizations");

        Console.WriteLine(generatedCode);

        Assert.That(generatedCode, Does.Contain("class AppLocalizations"));
        Assert.That(generatedCode, Does.Contain("public static string Key("));
        Assert.That(generatedCode, Does.Contain("A parameter:"));
    }

    [Test]
    public void GenerateClassPlural_ProducesCorrectSource() {
        ArbDocument document = new() {
            Locale = "en",
            Entries = new Dictionary<string, ArbEntry> {
                {
                    "key", new ArbEntry {
                        Key = "key",
                        Value = "{count, plural, =0{No items} =1{{count} item} other{{count} items}}",
                    }
                }
            }
        };


        string generatedCode = new ArbCodeGenerator().GenerateClass(document, "AppLocalizations", "MyApp.Localizations");

        Console.WriteLine(generatedCode);

        Assert.That(generatedCode, Does.Contain("class AppLocalizations"));
        Assert.That(generatedCode, Does.Contain("public static string Key("));

        Assert.That(generatedCode, Does.Not.Contain("plural, =0{"));
        Assert.That(generatedCode, Does.Contain("0 - \"No items\""));
    }

    // ── Dispatcher summary-tag tests ─────────────────────────────────────────────

    private static (IReadOnlyList<(ArbDocument, string)> locales, ArbDocument defaultDoc) BuildDispatcherFixture() {
        ArbDocument en = new() {
            Locale = "en",
            Entries = new Dictionary<string, ArbEntry> {
                { "appTitle",       new ArbEntry { Key = "appTitle",       Value = "My Application" } },
                { "welcomeMessage", new ArbEntry { Key = "welcomeMessage", Value = "Welcome, {username}!" } },
            }
        };
        ArbDocument cs = new() {
            Locale = "cs",
            Entries = new Dictionary<string, ArbEntry> {
                { "appTitle",       new ArbEntry { Key = "appTitle",       Value = "Moje aplikace" } },
                // welcomeMessage intentionally missing => MISSING (root culture, no fallback)
            }
        };
        ArbDocument enUs = new() {
            Locale = "en_US",
            Entries = new Dictionary<string, ArbEntry> {
                { "appTitle",       new ArbEntry { Key = "appTitle",       Value = "My US Application" } },
                // welcomeMessage intentionally missing => fallback to en
            }
        };

        (ArbDocument, string)[] locales = [
            (en,   "AppLocale_en"),
            (cs,   "AppLocale_cs"),
            (enUs, "AppLocale_en_US")
        ];

        return (locales, en);
    }

    [Test]
    public void GenerateDispatcherClass_SummaryContainsDirectValue() {
        (IReadOnlyList<(ArbDocument, string)> locales, ArbDocument defaultDoc) = BuildDispatcherFixture();
        string source = new ArbCodeGenerator().GenerateDispatcherClass(locales, defaultDoc, "AppLocale", "Test");

        Console.WriteLine(source);

        // en has appTitle directly → its value should appear in the summary
        Assert.That(source, Does.Contain("My Application"));
        // cs has appTitle directly → Czech value should appear
        Assert.That(source, Does.Contain("Moje aplikace"));
        // en_US has appTitle directly → US value should appear
        Assert.That(source, Does.Contain("My US Application"));
    }

    [Test]
    public void GenerateDispatcherClass_SummaryContainsFallbackAnnotation() {
        (IReadOnlyList<(ArbDocument, string)> locales, ArbDocument defaultDoc) = BuildDispatcherFixture();
        string source = new ArbCodeGenerator().GenerateDispatcherClass(locales, defaultDoc, "AppLocale", "Test");

        // en_US is missing welcomeMessage → summary should say [fallback to en]
        Assert.That(source, Does.Contain("[fallback to en]"));
    }

    [Test]
    public void GenerateDispatcherClass_SummaryContainsMissingAnnotation() {
        (IReadOnlyList<(ArbDocument, string)> locales, ArbDocument defaultDoc) = BuildDispatcherFixture();
        string source = new ArbCodeGenerator().GenerateDispatcherClass(locales, defaultDoc, "AppLocale", "Test");

        // cs is missing welcomeMessage and has no parent with it → [MISSING]
        Assert.That(source, Does.Contain("[MISSING]"));
    }

    [Test]
    public void GenerateDispatcherClass_PluralSummaryUsesCompactNotation() {
        ArbDocument en = new() {
            Locale = "en",
            Entries = new Dictionary<string, ArbEntry> {
                { "itemCount", new ArbEntry { Key = "itemCount", Value = "{count, plural, =0{No items} =1{{count} item} other{{count} items}}" } },
            }
        };
        ArbDocument cs = new() {
            Locale = "cs",
            Entries = new Dictionary<string, ArbEntry> {
                { "itemCount", new ArbEntry { Key = "itemCount", Value = "{count, plural, =0{Žádné položky} =1{{count} položka} =2{Dvě položky} other{{count} položek}}" } },
            }
        };

        (ArbDocument, string)[] locales = [(en, "AppLocale_en"), (cs, "AppLocale_cs")];
        string source = new ArbCodeGenerator().GenerateDispatcherClass(locales, en, "AppLocale", "Test");

        Console.WriteLine(source);

        // Raw ARB syntax must NOT appear in the doc comment
        Assert.That(source, Does.Not.Contain("plural, =0{"));
        // Compact notation must appear — numbered cases
        Assert.That(source, Does.Contain("0 - \"No items\""));
        Assert.That(source, Does.Contain("1 - \""));
        Assert.That(source, Does.Contain("else \""));
        // Czech compact form
        Assert.That(source, Does.Contain("0 - \"Žádné položky\""));
    }
}