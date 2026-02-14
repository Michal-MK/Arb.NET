namespace Arb.NET.Test;

[TestFixture]
public class ArbEntryTests {

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
}