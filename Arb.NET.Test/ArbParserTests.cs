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

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

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
    public void ParseContent_StandaloneClosingBracket() {
        var arbContent = """
                         {
                           "@@locale": "en",
                           "appTitle": "My } Application"
                         }
                         """;

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Entries, Has.Count.EqualTo(1));

        // Lang strings
        Assert.That(document.Entries.ContainsKey("appTitle"), Is.True);

        Assert.That(document.Entries["appTitle"].Value, Is.EqualTo("My } Application"));
    }

    [Test]
    public void ParseContent_StandaloneOpeningBracket() {
        var arbContent = """
                         {
                           "@@locale": "en",
                           "appTitle": "My { Application"
                         }
                         """;

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Entries, Has.Count.EqualTo(1));

        // Lang strings
        Assert.That(document.Entries.ContainsKey("appTitle"), Is.True);

        Assert.That(document.Entries["appTitle"].Value, Is.EqualTo("My { Application"));
    }
    
    [Test]
    public void ParseContent_EmptyGroup() {
        var arbContent = """
                         {
                           "@@locale": "en",
                           "appTitle": "My {} Application"
                         }
                         """;

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Entries, Has.Count.EqualTo(1));

        // Lang strings
        Assert.That(document.Entries.ContainsKey("appTitle"), Is.True);

        Assert.That(document.Entries["appTitle"].Value, Is.EqualTo("My {} Application"));
    } 
    
    [Test]
    public void ParseContent_Escaping() {
        var arbContent = """
                         {
                           "@@locale": "en",
                           "appTitle": "My \" Application"
                         }
                         """;

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Entries, Has.Count.EqualTo(1));

        // Lang strings
        Assert.That(document.Entries.ContainsKey("appTitle"), Is.True);

        Assert.That(document.Entries["appTitle"].Value, Is.EqualTo("My \" Application"));
    }    
}