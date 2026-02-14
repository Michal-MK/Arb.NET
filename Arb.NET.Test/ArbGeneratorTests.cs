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
        Assert.That(generatedCode, Does.Contain("key"));
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
        Assert.That(generatedCode, Does.Contain("key"));
    }
}