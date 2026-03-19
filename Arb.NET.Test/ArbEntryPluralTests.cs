namespace Arb.NET.Test;

[TestFixture]
public class ArbEntryPluarlTests {

    [Test]
    public void ParametricArbEntry_FailedPluralDueToMissingComma() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "Leading... {arg, plural =0{Zero} =5{Five} other{Some Number}}, Trailing..."
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(2));

        Assert.That(defs[0].Name, Is.EqualTo("Zero"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(26));
        Assert.That(defs[0].EndIndex, Is.EqualTo(32));

        Assert.That(defs[1].Name, Is.EqualTo("Five"));
        Assert.That(defs[1].StartIndex, Is.EqualTo(35));
        Assert.That(defs[1].EndIndex, Is.EqualTo(41));
    }

    [Test]
    public void ParametricArbEntry_Plural() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "Leading... {arg, plural, =0{Zero} =5{Five} other{Some Number}}, Trailing..."
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(1));

        Assert.That(defs[0], Is.InstanceOf<ArbPluralizationParameterDefinition>());

        Assert.That(defs[0].Name, Is.EqualTo("arg"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(11));
        Assert.That(defs[0].EndIndex, Is.EqualTo(62));

        var castDef = (ArbPluralizationParameterDefinition)defs[0];


        Assert.That(castDef.CountableParameters, Has.Count.EqualTo(2));
        Assert.That(castDef.CountableParameters, Contains.Key(0));
        Assert.That(castDef.CountableParameters[0], Is.EqualTo("Zero"));
        Assert.That(castDef.CountableParameters, Contains.Key(5));
        Assert.That(castDef.CountableParameters[5], Is.EqualTo("Five"));
        Assert.That(castDef.OtherParameter, Is.EqualTo("Some Number"));
    }
    
    [Test]
    public void ParametricArbEntry_JustOtherPlural() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "Leading... {arg, plural, other{Some Number}}, Trailing..."
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(1));

        Assert.That(defs[0], Is.InstanceOf<ArbPluralizationParameterDefinition>());

        Assert.That(defs[0].Name, Is.EqualTo("arg"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(11));
        Assert.That(defs[0].EndIndex, Is.EqualTo(44));

        var castDef = (ArbPluralizationParameterDefinition)defs[0];

        Assert.That(castDef.CountableParameters, Has.Count.EqualTo(0));
        Assert.That(castDef.OtherParameter, Is.EqualTo("Some Number"));
    }  
    
    [Test]
    public void ParametricArbEntry_InvalidPlural() {
        ArbEntry entry = new() {
            Key = "key",
            Value = "Leading... {arg, plural, }, Trailing..."
        };

        Assert.That(entry.IsParametric(out var defs), Is.True);
        Assert.That(defs, Has.Count.EqualTo(1));

        Assert.That(defs[0], Is.InstanceOf<ArbPluralizationParameterDefinition>());

        Assert.That(defs[0].Name, Is.EqualTo("arg"));
        Assert.That(defs[0].StartIndex, Is.EqualTo(11));
        Assert.That(defs[0].EndIndex, Is.EqualTo(26));

        var castDef = (ArbPluralizationParameterDefinition)defs[0];

        Assert.That(castDef.CountableParameters, Has.Count.EqualTo(0));
        Assert.That(castDef.OtherParameter, Is.EqualTo(""));
    }

    // ── TryDetectMangledPlural tests ────────────────────────────────────────────

    [Test]
    public void TryDetectMangledPlural_CommasBetweenForms_ReturnsTrue() {
        ArbEntry entry = new() {
            Key = "items",
            Value = "{count, plural, =0{No items}, =1{{count} item}, other{{count} items}}"
        };

        Assert.That(entry.TryDetectMangledPlural(out string? diagnostic), Is.True);
        Assert.That(diagnostic, Does.Contain("'items'"));
        Assert.That(diagnostic, Does.Contain("'count'"));
    }

    [Test]
    public void TryDetectMangledPlural_ValidPlural_ReturnsFalse() {
        ArbEntry entry = new() {
            Key = "items",
            Value = "{count, plural, =0{No items} =1{{count} item} other{{count} items}}"
        };

        Assert.That(entry.TryDetectMangledPlural(out _), Is.False);
    }

    [Test]
    public void TryDetectMangledPlural_NonPluralParametric_ReturnsFalse() {
        ArbEntry entry = new() {
            Key = "greeting",
            Value = "Hello, {username}!"
        };

        Assert.That(entry.TryDetectMangledPlural(out _), Is.False);
    }

    [Test]
    public void TryDetectMangledPlural_PlainText_ReturnsFalse() {
        ArbEntry entry = new() {
            Key = "title",
            Value = "My Application"
        };

        Assert.That(entry.TryDetectMangledPlural(out _), Is.False);
    }
}