namespace Arb.NET.Test;

[TestFixture]
public class SimpleArbsTests {
    [Test]
    public void Parse_EmptyArb() {
        string arbContent = """
                            {
                            }
                            """;

        ArbParseResult result = TestHelpers.ParseInvalid(arbContent);

        Assert.That(result.ValidationResults.IsValid, Is.False);
        Assert.That(result.ValidationResults.Errors, Has.Count.EqualTo(1));
        Assert.That(result.ValidationResults.Errors,
                    Has.Some.Matches<ArbValidationError>(s => s.Message.Contains("Required properties [\"@@locale\"] are not present"))
        );

        Assert.That(result.Document, Is.Null);
    }

    [Test]
    public void Parse_EmptyArbFileWithJustLanguageSpecifier() {
        string arbContent = """
                            {
                              "@@locale": "en"
                            }
                            """;

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Entries, Has.Count.EqualTo(0));
    }

    [Test]
    public void Parse_ArbFileWithJustLocaleString() {
        string arbContent = """
                            {
                              "@@locale": "en",
                              "key": "translation"
                            }
                            """;

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Context, Is.Empty);
        Assert.That(document.Entries, Has.Count.EqualTo(1));

        Assert.That(document.Entries.ContainsKey("key"), Is.True);
        Assert.That(document.Entries["key"].Value, Is.EqualTo("translation"));
    }

    [Test]
    public void Parse_ArbFileWithContextAndOneString() {
        string arbContent = """
                            {
                              "@@locale": "en",
                              "@@context": "Context",
                              "key": "translation"
                            }
                            """;

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Context, Is.EqualTo("Context"));
        Assert.That(document.Entries, Has.Count.EqualTo(1));

        Assert.That(document.Entries.ContainsKey("key"), Is.True);
        Assert.That(document.Entries["key"].Value, Is.EqualTo("translation"));
    }

    [Test]
    public void Parse_ArbFileWithParametrizedStringByName() {
        string arbContent = """
                            {
                              "@@locale": "en",
                              "key": "translation with param: {param}"
                            }
                            """;

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Entries, Has.Count.EqualTo(1));

        Assert.That(document.Entries.ContainsKey("key"), Is.True);
        Assert.That(document.Entries["key"].Value, Is.EqualTo("translation with param: {param}"));
    }

    [Test]
    public void Parse_ArbFileWithParametrizedStringByIndex() {
        string arbContent = """
                            {
                              "@@locale": "en",
                              "key": "translation with param: {0}"
                            }
                            """;

        ArbDocument document = TestHelpers.ParseValid(arbContent).Document!;

        Assert.That(document.Locale, Is.EqualTo("en"));
        Assert.That(document.Entries, Has.Count.EqualTo(1));

        Assert.That(document.Entries.ContainsKey("key"), Is.True);
        Assert.That(document.Entries["key"].Value, Is.EqualTo("translation with param: {0}"));
    }
}