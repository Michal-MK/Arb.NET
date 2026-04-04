namespace Arb.NET.Test;

[TestFixture]
public class ArbEntryTests {

    [Test]
    public void ParametricArbEntry_JustParameterParsing() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "{param}"
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(1));

        Assert.That(defs[0].Name, Is.EqualTo("param"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(0));
        Assert.That(defs[0].EndIndex, Is.EqualTo("{param}".Length));
    }  
    
    [Test]
    public void ParametricArbEntry_ParameterParsing() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "value '{param}' continuation"
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(1));

        Assert.That(defs[0].Name, Is.EqualTo("param"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(7));
        Assert.That(defs[0].EndIndex, Is.EqualTo(14));
    }

    [Test]
    public void ParametricArbEntry_ParameterParsingWithEscapedBraces() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "\\{value} '{param}' continuation"
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(1));

        Assert.That(defs[0].Name, Is.EqualTo("param"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(10));
        Assert.That(defs[0].EndIndex, Is.EqualTo(17));
    }

    [Test]
    public void ParametricArbEntry_ParameterParsingWithEscapedBraces2() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "{value '{param}' continuation}"
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(1));

        Assert.That(defs[0].Name, Is.EqualTo("param"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(8));
        Assert.That(defs[0].EndIndex, Is.EqualTo(15));
    }

    [Test]
    public void ParametricArbEntry_ParameterParsingWithEscapedBraces3() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "{value '{{param}}' continuation}"
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(1));

        Assert.That(defs[0].Name, Is.EqualTo("param"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(9));
        Assert.That(defs[0].EndIndex, Is.EqualTo(16));
    }

    [Test]
    public void ParametricArbEntry_ParameterParsingTwoDefs() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "value '{param}', {other}, continuation"
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(2));

        Assert.That(defs[0].Name, Is.EqualTo("param"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(7));
        Assert.That(defs[0].EndIndex, Is.EqualTo(14));

        Assert.That(defs[1].Name, Is.EqualTo("other"));
        Assert.That(defs[1].StartIndex, Is.EqualTo(17));
        Assert.That(defs[1].EndIndex, Is.EqualTo(24));
    }

    [Test]
    public void ParametricArbEntry_ParameterParsingOnlyClosing() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "value} '{param}', {other}, continuation"
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(2));

        Assert.That(defs[0].Name, Is.EqualTo("param"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(8));
        Assert.That(defs[0].EndIndex, Is.EqualTo(15));

        Assert.That(defs[1].Name, Is.EqualTo("other"));
        Assert.That(defs[1].StartIndex, Is.EqualTo(18));
        Assert.That(defs[1].EndIndex, Is.EqualTo(25));
    }

    [Test]
    public void ParametricArbEntry_OpeningBeforeParameter() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "'{{param}', continuation"
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(1));

        Assert.That(defs[0].Name, Is.EqualTo("param"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(2));
        Assert.That(defs[0].EndIndex, Is.EqualTo(9));
    }

    [Test]
    public void RenamePlaceholder_UpdatesValueAndMetadata() {
        ArbEntry entry = new() {
            Key = "items",
            Value = "{count, plural, =0{No items} other{{count} items for {name}}}",
            Metadata = new ArbMetadata {
                Placeholders = new Dictionary<string, ArbPlaceholder> {
                    ["count"] = new() { Type = "int" },
                    ["name"] = new() { Type = "String" }
                }
            }
        };

        bool changed = entry.RenamePlaceholder("count", "itemCount");

        Assert.That(changed, Is.True);
        Assert.That(entry.Value, Is.EqualTo("{itemCount, plural, =0{No items} other{{itemCount} items for {name}}}"));
        Assert.That(entry.Metadata?.Placeholders, Does.ContainKey("itemCount"));
        Assert.That(entry.Metadata?.Placeholders, Does.Not.ContainKey("count"));
        Assert.That(entry.GetPlaceholderNames(), Is.EquivalentTo(new[] { "itemCount", "name" }));
    }

    [Test]
    public void RenamePlaceholder_InvalidNewName_DoesNothing() {
        ArbEntry entry = new() {
            Key = "hello",
            Value = "Hello {name}!"
        };

        bool changed = entry.RenamePlaceholder("name", "bad-name");

        Assert.That(changed, Is.False);
        Assert.That(entry.Value, Is.EqualTo("Hello {name}!"));
    }

    [Test]
    public void RenamePlaceholder_DoesNotTouchEscapedBraces() {
        ArbEntry entry = new() {
            Key = "hello",
            Value = "\\{name} and {name}"
        };

        bool changed = entry.RenamePlaceholder("name", "userName");

        Assert.That(changed, Is.True);
        Assert.That(entry.Value, Is.EqualTo("\\{name} and {userName}"));
    }

    [Test]
    public void RenamePlaceholder_NumericPlaceholder_IsSupported() {
        ArbEntry entry = new() {
            Key = "legacy",
            Value = "Value {0} and again {0}",
            Metadata = new ArbMetadata {
                Placeholders = new Dictionary<string, ArbPlaceholder> {
                    ["0"] = new() { Type = "Object" }
                }
            }
        };

        bool changed = entry.RenamePlaceholder("0", "param0");

        Assert.That(changed, Is.True);
        Assert.That(entry.Value, Is.EqualTo("Value {param0} and again {param0}"));
        Assert.That(entry.Metadata?.Placeholders, Does.ContainKey("param0"));
    }

    [Test]
    public void RenamePlaceholder_DoesNotRenameLongerPlaceholderPrefix() {
        ArbEntry entry = new() {
            Key = "counts",
            Value = "{counter} and {count}"
        };

        bool changed = entry.RenamePlaceholder("count", "itemCount");

        Assert.That(changed, Is.True);
        Assert.That(entry.Value, Is.EqualTo("{counter} and {itemCount}"));
    }
}