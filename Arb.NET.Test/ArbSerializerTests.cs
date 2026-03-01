namespace Arb.NET.Test;

[TestFixture]
public class ArbSerializerTests {
    [Test]
    public void SerializeSimple_ProducesCorrectArbFile() {
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

        string arb = ArbSerializer.Serialize(document);

        Console.WriteLine(arb);

        Assert.That(arb, Does.Contain("\"@@locale\": \"en\""));
        Assert.That(arb, Does.Contain("\"key\""));
        Assert.That(arb, Does.Contain("\"A parameter: '{param}' is here.\""));
    }

    [Test]
    public void SerializeWithEscapeChars_ProducesCorrectArbFile() {
        ArbDocument document = new() {
            Locale = "en",
            Entries = new Dictionary<string, ArbEntry> {
                {
                    "key", new ArbEntry {
                        Key = "key",
                        Value = "Some string \"with\" special characters: \n new line, \t tab, and a backslash \\.",
                    }
                }
            }
        };

        string arb = ArbSerializer.Serialize(document);

        Console.WriteLine(arb);

        Assert.That(arb, Does.Contain("\"@@locale\": \"en\""));
        Assert.That(arb, Does.Contain("\"key\""));
        Assert.That(arb, Does.Contain("\"Some string \\\"with\\\" special characters: \\n new line, \\t tab, and a backslash \\\\.\""));
    }
}