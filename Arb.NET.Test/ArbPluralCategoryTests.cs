namespace Arb.NET.Test;

[TestFixture]
public class ArbPluralCategoryTests {

    private static ArbPluralizationParameterDefinition ParsePlural(string value, out List<ArbParameterDefinition> defs) {
        ArbEntry entry = new() { Key = "key", Value = value };
        Assert.That(entry.IsParametric(out defs), Is.True);
        return defs.OfType<ArbPluralizationParameterDefinition>().Single();
    }

    private static string Generate(string value, string locale = "en") {
        ArbDocument document = new() {
            Locale = locale,
            Entries = new Dictionary<string, ArbEntry> { { "key", new ArbEntry { Key = "key", Value = value } } }
        };
        string code = new ArbCodeGenerator().GenerateClass(document, "L_" + locale, "Test");
        Console.WriteLine(code);
        return code;
    }

    // ── Parser ──────────────────────────────────────────────────────────────────

    [Test]
    public void Parse_CldrCategories() {
        ArbPluralizationParameterDefinition plural =
            ParsePlural("{count, plural, =0{None} one{One} few{Few} many{Many} other{{count} items}}", out _);

        Assert.That(plural.Name, Is.EqualTo("count"));
        Assert.That(plural.CountableParameters, Is.EquivalentTo(new Dictionary<int, string> { [0] = "None" }));
        Assert.That(plural.CategoryParameters["one"], Is.EqualTo("One"));
        Assert.That(plural.CategoryParameters["few"], Is.EqualTo("Few"));
        Assert.That(plural.CategoryParameters["many"], Is.EqualTo("Many"));
        Assert.That(plural.OtherParameter, Is.EqualTo("{count} items"));
    }

    [Test]
    public void Parse_WhitespaceTolerant() {
        const string VALUE = "{count,plural,\n  one {One}\n  other  {Many}\n}";
        ArbPluralizationParameterDefinition plural = ParsePlural(VALUE, out _);

        Assert.That(plural.CategoryParameters["one"], Is.EqualTo("One"));
        Assert.That(plural.OtherParameter, Is.EqualTo("Many"));
        Assert.That(plural.EndIndex, Is.EqualTo(VALUE.Length));
    }

    [Test]
    public void Parse_OtherBeforeExactMatch() {
        ArbPluralizationParameterDefinition plural = ParsePlural("{n, plural, other{Many} =1{One}}", out _);

        Assert.That(plural.CountableParameters[1], Is.EqualTo("One"));
        Assert.That(plural.OtherParameter, Is.EqualTo("Many"));
    }

    [Test]
    public void Parse_PlaceholdersInsideFormsBecomeParameters() {
        const string VALUE = "{count, plural, =1{One item, owner {name}} other{{count} items, owner {name}}}";
        ParsePlural(VALUE, out List<ArbParameterDefinition> defs);

        Assert.That(defs.Select(d => d.Name), Is.EqualTo(new[] { "count", "name", "name" }));
        ArbParameterDefinition first = defs[1];
        Assert.That(VALUE.Substring(first.StartIndex, first.EndIndex - first.StartIndex), Is.EqualTo("{name}"));
        Assert.That(new ArbEntry { Key = "k", Value = VALUE }.GetPlaceholderNames(), Is.EqualTo(new[] { "count", "name" }));
    }

    [Test]
    public void Parse_NotAPlural_IsUnchanged() {
        ArbEntry entry = new() { Key = "k", Value = "{x, pluralize, a} {y}" };
        Assert.That(entry.IsParametric(out List<ArbParameterDefinition> defs), Is.True);
        Assert.That(defs.Select(d => d.Name), Is.EqualTo(new[] { "y" }));
        Assert.That(entry.TryDetectMangledPlural(out _), Is.False);
    }

    // ── Diagnostics ─────────────────────────────────────────────────────────────

    [TestCase("{count, plural, =1{One}, other{Many}}", "separated by spaces, not commas")]
    [TestCase("{count, plural, 1={One} other{Many}}", "'1=' is not valid")]
    [TestCase("{count, plural, single{One} other{Many}}", "'single' is not a plural category")]
    [TestCase("{count, plural, one{One} one{Uno} other{Many}}", "'one' is defined more than once")]
    [TestCase("{count, plural, one{One} other{Many}", "missing the closing '}' of the plural")]
    [TestCase("{count, plural, one One}", "expected '{' after 'one'")]
    [TestCase("{count, plural, offset:1 other{Many}}", "'offset:' is not supported")]
    public void Mangled_ReportsSpecificReason(string value, string expectedReason) {
        ArbEntry entry = new() { Key = "items", Value = value };

        Assert.That(entry.TryDetectMangledPlural(out string? diagnostic), Is.True);
        Assert.That(diagnostic, Does.Contain(expectedReason));
        Assert.That(diagnostic, Does.Contain("'items'"));
        Assert.That(diagnostic, Does.Contain("'count'"));
    }

    // ── Generation ──────────────────────────────────────────────────────────────

    [Test]
    public void Generate_CldrCategory_English() {
        string code = Generate("{count, plural, one{One item} other{{count} items}}");

        Assert.That(code, Does.Not.Contain("#warning"));
        Assert.That(code, Does.Contain("public static string Key(int count)"));
        // Flutter-compatible: 'one' is also an exact match for 1
        Assert.That(code, Does.Contain("1 => \"One item\","));
        Assert.That(code, Does.Contain("_ => __ArbPluralCategory(count) switch {"));
        Assert.That(code, Does.Contain("\"one\" => \"One item\","));
        Assert.That(code, Does.Contain("_ => \"\" + count.ToString() + \" items\""));
        Assert.That(code, Does.Contain("private static string __ArbPluralCategory(int count)"));
        Assert.That(code, Does.Contain("if (n == 1) return \"one\";"));
    }

    [Test]
    public void Generate_CldrCategory_CzechUsesFew() {
        string code = Generate("{count, plural, one{{count} položka} few{{count} položky} other{{count} položek}}", "cs");

        Assert.That(code, Does.Contain("\"few\" => \"\" + count.ToString() + \" položky\","));
        Assert.That(code, Does.Contain("if (n >= 2 && n <= 4) return \"few\";"));
    }

    [Test]
    public void Generate_ExactMatchWinsOverCategory() {
        string code = Generate("{count, plural, =1{Exactly one} one{One} other{Many}}");

        Assert.That(code, Does.Contain("1 => \"Exactly one\","));
        Assert.That(code, Does.Not.Contain("1 => \"One\","));
        Assert.That(code, Does.Contain("\"one\" => \"One\","));
    }

    [Test]
    public void Generate_NoCategories_NoHelperAndUnchangedShape() {
        string code = Generate("{count, plural, =0{No items} =1{{count} item} other{{count} items}}");

        Assert.That(code, Does.Not.Contain("__ArbPluralCategory"));
        Assert.That(code, Does.Contain("var @selectedContent_count = (count) switch {"));
        Assert.That(code, Does.Contain("1 => \"\" + count.ToString() + \" item\","));
        Assert.That(code, Does.Contain("return \"\" + @selectedContent_count + \"\";"));
    }

    [Test]
    public void Generate_PlaceholderInsideForm_IsParameterAndSubstituted() {
        string code = Generate("{count, plural, =1{One item, owner {name}} other{{count} items, owner {name}}}");

        Assert.That(code, Does.Contain("public static string Key(int count, object name)"));
        Assert.That(code, Does.Contain("1 => \"One item, owner \" + name?.ToString() + \"\","));
        Assert.That(code, Does.Contain("_ => \"\" + count.ToString() + \" items, owner \" + name?.ToString() + \"\""));
        Assert.That(code, Does.Not.Contain("=> \"One item, owner {name}"));
    }

    [Test]
    public void Generate_RepeatedPlaceholder_SingleParameter() {
        string code = Generate("{a} and {a}");

        Assert.That(code, Does.Contain("public static string Key(object a)"));
    }

    [Test]
    public void Generate_PlainReferenceToPluralVariable_IsInt() {
        string code = Generate("{count} files: {count, plural, =1{one} other{many}}");

        Assert.That(code, Does.Contain("public static string Key(int count)"));
        Assert.That(code, Does.Contain("count.ToString()"));
        Assert.That(code, Does.Not.Contain("count?.ToString()"));
    }

    [Test]
    public void Generate_NestedPlural_IsInlineSwitch() {
        string code = Generate("{files, plural, =1{One file} other{{files} files in {dirs, plural, =1{one folder} other{{dirs} folders}}}}");

        Assert.That(code, Does.Contain("public static string Key(int files, int dirs)"));
        Assert.That(code, Does.Contain("((dirs) switch { 1 => \"one folder\", _ => \"\" + dirs.ToString() + \" folders\" })"));
    }

    [Test]
    public void Dispatcher_PlaceholderInsideForm_ForwardsAllArguments() {
        ArbDocument en = new() {
            Locale = "en",
            Entries = new Dictionary<string, ArbEntry> {
                { "key", new ArbEntry { Key = "key", Value = "{count, plural, one{One item, {name}} other{{count} items, {name}}}" } }
            }
        };
        ArbDocument cs = new() {
            Locale = "cs",
            Entries = new Dictionary<string, ArbEntry> {
                { "key", new ArbEntry { Key = "key", Value = "{count, plural, one{{name}: položka} few{{name}: {count} položky} other{{name}: {count} položek}}" } }
            }
        };

        string dispatcher = new ArbCodeGenerator().GenerateDispatcherClass([(en, "L_en"), (cs, "L_cs")], en, "L", "Test");
        Console.WriteLine(dispatcher);

        Assert.That(dispatcher, Does.Contain("public string Key(int count, object name)"));
        Assert.That(dispatcher, Does.Contain("L_en.Key(count, name)"));
        Assert.That(dispatcher, Does.Contain("L_cs.Key(count, name)"));
    }

    // ── Rules ───────────────────────────────────────────────────────────────────

    [TestCase("en", true)]
    [TestCase("en_US", true)]
    [TestCase("cs-CZ", true)]
    [TestCase("pt_PT", true)]
    [TestCase("xx", false)]
    public void Rules_LocaleLookup(string locale, bool known) {
        PluralRules.GetRules(locale, out bool isKnown);
        Assert.That(isKnown, Is.EqualTo(known));
    }

    [Test]
    public void Rules_PortugalDiffersFromBrazil() {
        Assert.That(PluralRules.GetRules("pt_PT", out _)[0].Condition, Is.EqualTo("n == 1"));
        Assert.That(PluralRules.GetRules("pt_BR", out _)[0].Condition, Is.EqualTo("n <= 1"));
    }
}
