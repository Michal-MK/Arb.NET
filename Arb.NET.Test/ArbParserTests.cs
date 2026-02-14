namespace Arb.NET.Test;

[TestFixture]
public class ArbParserTests {
    [Test]
    public void ParseContent_ReturnsCorrectLocaleAndEntries() {
        var arbContent = """
                         {
                           "@@locale": "en",
                           "appTitle": "My Application",
                           "@appTitle": {
                             "description": "The title of the application"
                           },
                           "welcomeMessage": "Welcome, {username}!",
                           "@welcomeMessage": {
                             "description": "Welcome message with username placeholder",
                             "placeholders": {
                               "username": {
                                 "type": "String"
                               }
                             }
                           }
                         }
                         """;

        var result = new ArbParser().ParseContent(arbContent);

        var resultString = result.ToString();
        Console.WriteLine(resultString);

        Assert.That(result.ValidationResults.IsValid, Is.True);

        var document = result.Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Entries, Has.Count.EqualTo(2));

        // Lang strings
        Assert.That(document.Entries.ContainsKey("appTitle"), Is.True);
        Assert.That(document.Entries.ContainsKey("welcomeMessage"), Is.True);

        Assert.That(document.Entries["appTitle"].Value, Is.EqualTo("My Application"));
        Assert.That(document.Entries["welcomeMessage"].Value, Is.EqualTo("Welcome, {username}!"));

        Assert.That(document.Entries["appTitle"].Metadata?.Description, Is.EqualTo("The title of the application"));
        Assert.That(document.Entries["welcomeMessage"].Metadata?.Description, Is.EqualTo("Welcome message with username placeholder"));


        Assert.That(document.Entries["welcomeMessage"].Metadata?.Placeholders, Contains.Key("username"));
        Assert.That(document.Entries["welcomeMessage"].Metadata?.Placeholders, Contains.Value(new ArbPlaceholder {
            Type = "String"
        }));
    }

    [Test]
    public void GenerateClass_ProducesCorrectSource() {
        var document = new ArbDocument {
            Locale = "en",
            Entries = new Dictionary<string, ArbEntry> {
                {
                    "appTitle", new ArbEntry {
                        Key = "appTitle",
                        Value = "My Application",
                        Metadata = new ArbMetadata {
                            Description = "The title of the application"
                        }
                    }
                }, {
                    "welcomeMessage", new ArbEntry {
                        Key = "welcomeMessage",
                        Value = "Welcome, {username}!",
                        Metadata = new ArbMetadata {
                            Description = "Welcome message with username placeholder",
                            Placeholders = new Dictionary<string, ArbPlaceholder> {
                                {
                                    "username", new ArbPlaceholder {
                                        Type = "String"
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        var generatedCode = new ArbCodeGenerator().GenerateClass(document, "AppLocalizations", "MyApp.Localizations");

        Assert.That(generatedCode, Does.Contain("class AppLocalizations"));
        Assert.That(generatedCode, Does.Contain("AppTitle"));
        Assert.That(generatedCode, Does.Contain("WelcomeMessage"));
        Assert.That(generatedCode, Does.Contain("string username"));
        Assert.That(generatedCode, Does.Contain("string.Format"));
    }
}