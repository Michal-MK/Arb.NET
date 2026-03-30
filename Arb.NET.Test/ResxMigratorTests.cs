using Arb.NET.Models;
using Arb.NET.Tool.Migration;

namespace Arb.NET.Test;

[TestFixture]
public class ResxMigratorTests
{
    private string sourceDir = null!;
    private string outputDir = null!;

    [SetUp]
    public void SetUp()
    {
        string baseDir = Path.Combine(Path.GetTempPath(), "arb-resx-test-" + Guid.NewGuid().ToString("N")[..8]);
        sourceDir = Path.Combine(baseDir, "source");
        outputDir = Path.Combine(baseDir, "output");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(outputDir);
    }

    [TearDown]
    public void TearDown()
    {
        string baseDir = Path.GetDirectoryName(sourceDir)!;
        if (Directory.Exists(baseDir)) {
            Directory.Delete(baseDir, true);
        }
    }

    private static L10nConfig DefaultConfig() => new() {
        ArbDir = L10nConfig.DEFAULT_ARB_DIR,
        TemplateArbFile = "en.arb"
    };

    /// <summary>
    /// Standard .resx group: Base.resx + Base.cs.resx + Base.en.resx etc.
    /// All keys should be merged into a single arbs/ folder with one .arb per locale.
    /// </summary>
    [Test]
    public void SimpleLocales_MergedIntoSingleArbsFolder()
    {
        // Arrange: two resource groups in different subdirectories
        string controlsDir = Path.Combine(sourceDir, "Texts", "Controls");
        string viewsDir = Path.Combine(sourceDir, "Texts", "Views");
        Directory.CreateDirectory(controlsDir);
        Directory.CreateDirectory(viewsDir);

        WriteResx(Path.Combine(controlsDir, "AlertsText.resx"), ("Title", "Titulek"));
        WriteResx(Path.Combine(controlsDir, "AlertsText.en.resx"), ("Title", "Title"));
        WriteResx(Path.Combine(controlsDir, "AlertsText.cs.resx"), ("Title", "Titulek CS"));
        WriteResx(Path.Combine(controlsDir, "AlertsText.sk.resx"), ("Title", "Titulok"));

        WriteResx(Path.Combine(viewsDir, "LoginText.resx"), ("Submit", "Odeslat"));
        WriteResx(Path.Combine(viewsDir, "LoginText.en.resx"), ("Submit", "Submit"));
        WriteResx(Path.Combine(viewsDir, "LoginText.hu.resx"), ("Submit", "Beküldés"));

        // Act
        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false);

        // Assert: no errors
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        // All .arb files should be in arbs/ inside the output folder
        string arbsDir = Path.Combine(outputDir, "arbs");
        Assert.That(Directory.Exists(arbsDir), Is.True, "Expected arbs/ directory in output folder");

        // Should NOT have arbs/ subdirectories scattered in source
        Assert.That(Directory.Exists(Path.Combine(controlsDir, "arbs")), Is.False,
            "Should not create arbs/ inside source subdirectories");
        Assert.That(Directory.Exists(Path.Combine(viewsDir, "arbs")), Is.False,
            "Should not create arbs/ inside source subdirectories");

        // Check locale files exist
        Assert.That(File.Exists(Path.Combine(arbsDir, "en.arb")), Is.True);
        Assert.That(File.Exists(Path.Combine(arbsDir, "cs.arb")), Is.True);
        Assert.That(File.Exists(Path.Combine(arbsDir, "sk.arb")), Is.True);
        Assert.That(File.Exists(Path.Combine(arbsDir, "hu.arb")), Is.True);

        // Verify merged content: en.arb should contain keys from both Controls and Views
        ArbDocument enDoc = ParseArb(Path.Combine(arbsDir, "en.arb"));
        Assert.That(enDoc.Entries.ContainsKey("title"), Is.True, "en.arb should have 'title' from AlertsText");
        Assert.That(enDoc.Entries.ContainsKey("submit"), Is.True, "en.arb should have 'submit' from LoginText");
        Assert.That(enDoc.Entries["title"].Value, Is.EqualTo("Title"));
        Assert.That(enDoc.Entries["submit"].Value, Is.EqualTo("Submit"));
    }

    /// <summary>
    /// Compound locale: Variant_CS.en.resx should produce a cs_en locale.
    /// The _CS suffix in the base name indicates a culture-specific English variant.
    /// </summary>
    [Test]
    public void CompoundLocale_CultureSpecificEnglish_ProducesCorrectLocale()
    {
        // Arrange: a Variant group with compound locales
        string viewsDir = Path.Combine(sourceDir, "Texts", "Views");
        Directory.CreateDirectory(viewsDir);

        // Standard locales
        WriteResx(Path.Combine(viewsDir, "MainViewTextariant.resx"), ("News", "Zprávy"));
        WriteResx(Path.Combine(viewsDir, "MainViewTextariant.cs.resx"), ("News", "Zprávy CS"));
        WriteResx(Path.Combine(viewsDir, "MainViewTextariant.bg.resx"), ("News", "Новини"));
        WriteResx(Path.Combine(viewsDir, "MainViewTextariant.hu.resx"), ("News", "Hírek"));
        WriteResx(Path.Combine(viewsDir, "MainViewTextariant.sk.resx"), ("News", "Správy"));

        // Compound locales: _XX.en.resx = English override for culture XX
        WriteResx(Path.Combine(viewsDir, "MainViewTextVariant_CS.en.resx"), ("News", "News EN-CS"));
        WriteResx(Path.Combine(viewsDir, "MainViewTextVariant_BG.en.resx"), ("News", "News EN-BG"));
        WriteResx(Path.Combine(viewsDir, "MainViewTextVariant_HU.en.resx"), ("News", "News EN-HU"));
        WriteResx(Path.Combine(viewsDir, "MainViewTextVariant_SK.en.resx"), ("News", "News EN-SK"));

        // Act
        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false);

        // Assert: no errors
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        string arbsDir = Path.Combine(outputDir, "arbs");

        // Standard locale files
        Assert.That(File.Exists(Path.Combine(arbsDir, "cs.arb")), Is.True);
        Assert.That(File.Exists(Path.Combine(arbsDir, "bg.arb")), Is.True);
        Assert.That(File.Exists(Path.Combine(arbsDir, "hu.arb")), Is.True);
        Assert.That(File.Exists(Path.Combine(arbsDir, "sk.arb")), Is.True);

        // Compound locale files: culture-specific English
        Assert.That(File.Exists(Path.Combine(arbsDir, "cs_en.arb")), Is.True, "Expected cs_en.arb for _CS.en compound locale");
        Assert.That(File.Exists(Path.Combine(arbsDir, "bg_en.arb")), Is.True, "Expected bg_en.arb for _BG.en compound locale");
        Assert.That(File.Exists(Path.Combine(arbsDir, "hu_en.arb")), Is.True, "Expected hu_en.arb for _HU.en compound locale");
        Assert.That(File.Exists(Path.Combine(arbsDir, "sk_en.arb")), Is.True, "Expected sk_en.arb for _SK.en compound locale");

        // Verify content
        ArbDocument csEnDoc = ParseArb(Path.Combine(arbsDir, "cs_en.arb"));
        Assert.That(csEnDoc.Locale, Is.EqualTo("cs_en"));
        Assert.That(csEnDoc.Entries["news"].Value, Is.EqualTo("News EN-CS"));
    }

    /// <summary>
    /// The base (no-suffix) .resx should map to "en" locale when an explicit .en.resx also exists.
    /// Keys from the base file should still appear in the "en" locale arb.
    /// </summary>
    [Test]
    public void BaseFile_MapsToEnLocale()
    {
        string dir = Path.Combine(sourceDir, "Texts");
        Directory.CreateDirectory(dir);

        WriteResx(Path.Combine(dir, "Strings.resx"), ("Hello", "Hello Base"), ("OnlyInBase", "Base Only"));
        WriteResx(Path.Combine(dir, "Strings.en.resx"), ("Hello", "Hello EN"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        string arbsDir = Path.Combine(outputDir, "arbs");
        ArbDocument enDoc = ParseArb(Path.Combine(arbsDir, "en.arb"));

        // The explicit .en.resx value should win over the base for shared keys
        Assert.That(enDoc.Entries["hello"].Value, Is.EqualTo("Hello EN"));
        // Keys only in the base should still appear
        Assert.That(enDoc.Entries["onlyInBase"].Value, Is.EqualTo("Base Only"));
    }

    /// <summary>
    /// Format strings like {0}, {1:N2} should be converted to named ARB placeholders.
    /// </summary>
    [Test]
    public void FormatStrings_ConvertedToNamedPlaceholders()
    {
        string dir = Path.Combine(sourceDir, "Texts");
        Directory.CreateDirectory(dir);

        WriteResx(Path.Combine(dir, "Strings.en.resx"),
            ("Welcome", "Hello {0}, you have {1} messages"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        string arbsDir = Path.Combine(outputDir, "arbs");
        ArbDocument doc = ParseArb(Path.Combine(arbsDir, "en.arb"));

        Assert.That(doc.Entries["welcome"].Value, Is.EqualTo("Hello {param0}, you have {param1} messages"));
        Assert.That(doc.Entries["welcome"].Metadata, Is.Not.Null);
        Assert.That(doc.Entries["welcome"].Metadata!.Placeholders, Is.Not.Null);
        Assert.That(doc.Entries["welcome"].Metadata!.Placeholders!.ContainsKey("param0"), Is.True);
        Assert.That(doc.Entries["welcome"].Metadata!.Placeholders!.ContainsKey("param1"), Is.True);
    }

    /// <summary>
    /// .resx key "My_Resource.Name" should become camelCase "myResourceName".
    /// </summary>
    [Test]
    public void KeyConversion_UnderscoresAndDots_ToCamelCase()
    {
        string dir = Path.Combine(sourceDir, "Texts");
        Directory.CreateDirectory(dir);

        WriteResx(Path.Combine(dir, "Strings.en.resx"),
            ("Simple_Key", "val1"),
            ("My.Dotted.Key", "val2"),
            ("Already_camelCase", "val3"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        string arbsDir = Path.Combine(outputDir, "arbs");
        ArbDocument doc = ParseArb(Path.Combine(arbsDir, "en.arb"));

        Assert.That(doc.Entries.ContainsKey("simpleKey"), Is.True);
        Assert.That(doc.Entries.ContainsKey("myDottedKey"), Is.True);
        Assert.That(doc.Entries.ContainsKey("alreadyCamelCase"), Is.True);
    }

    /// <summary>
    /// The .resx fallback chain (e.g. en-US → en → default) should be preserved:
    /// each level produces its own arb file, and keys only present in the base/parent
    /// are not duplicated into more specific locales.
    /// </summary>
    [Test]
    public void SubcultureFallbackChain_PreservesHierarchy()
    {
        string dir = Path.Combine(sourceDir, "Texts");
        Directory.CreateDirectory(dir);

        // Base (default locale) has all keys
        WriteResx(Path.Combine(dir, "Strings.resx"),
            ("AppName", "My App"), ("Welcome", "Welcome"), ("Greeting", "Hi"));
        // en overrides some keys
        WriteResx(Path.Combine(dir, "Strings.en.resx"),
            ("AppName", "My App EN"), ("Welcome", "Welcome EN"));
        // en-US overrides only one key
        WriteResx(Path.Combine(dir, "Strings.en-US.resx"),
            ("AppName", "My App US"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        string arbsDir = Path.Combine(outputDir, "arbs");

        // All three locale files should exist
        Assert.That(File.Exists(Path.Combine(arbsDir, "en.arb")), Is.True);
        Assert.That(File.Exists(Path.Combine(arbsDir, "en_US.arb")), Is.True);

        ArbDocument enDoc = ParseArb(Path.Combine(arbsDir, "en.arb"));
        ArbDocument enUsDoc = ParseArb(Path.Combine(arbsDir, "en_US.arb"));

        // en.arb: gets base keys first, then en overrides on top
        Assert.That(enDoc.Entries["appName"].Value, Is.EqualTo("My App EN"));
        Assert.That(enDoc.Entries["welcome"].Value, Is.EqualTo("Welcome EN"));
        Assert.That(enDoc.Entries["greeting"].Value, Is.EqualTo("Hi"), "Base-only key should be present in primary locale");

        // en_US.arb: only contains what was explicitly in en-US.resx, no fallback duplication
        Assert.That(enUsDoc.Entries["appName"].Value, Is.EqualTo("My App US"));
        Assert.That(enUsDoc.Entries.ContainsKey("welcome"), Is.False,
            "en_US should not contain keys only present in parent en.resx");
        Assert.That(enUsDoc.Entries.ContainsKey("greeting"), Is.False,
            "en_US should not contain keys only present in base .resx");
    }

    /// <summary>
    /// Standard .NET subculture .resx files like Strings.en-US.resx, Strings.pt-BR.resx
    /// should produce arb files with underscore-separated locale codes (en_US, pt_BR).
    /// </summary>
    [Test]
    public void StandardSubculture_ProducesCorrectLocale()
    {
        string dir = Path.Combine(sourceDir, "Texts");
        Directory.CreateDirectory(dir);

        WriteResx(Path.Combine(dir, "Strings.resx"), ("Hello", "Hello"));
        WriteResx(Path.Combine(dir, "Strings.en.resx"), ("Hello", "Hello EN"));
        WriteResx(Path.Combine(dir, "Strings.en-US.resx"), ("Hello", "Hello US"));
        WriteResx(Path.Combine(dir, "Strings.pt-BR.resx"), ("Hello", "Olá BR"));
        WriteResx(Path.Combine(dir, "Strings.zh-CN.resx"), ("Hello", "你好"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        string arbsDir = Path.Combine(outputDir, "arbs");

        // Standard subculture arb files should exist with underscore separator
        Assert.That(File.Exists(Path.Combine(arbsDir, "en_US.arb")), Is.True, "Expected en_US.arb for en-US subculture");
        Assert.That(File.Exists(Path.Combine(arbsDir, "pt_BR.arb")), Is.True, "Expected pt_BR.arb for pt-BR subculture");
        Assert.That(File.Exists(Path.Combine(arbsDir, "zh_CN.arb")), Is.True, "Expected zh_CN.arb for zh-CN subculture");

        // Verify content and locale metadata
        ArbDocument enUsDoc = ParseArb(Path.Combine(arbsDir, "en_US.arb"));
        Assert.That(enUsDoc.Locale, Is.EqualTo("en_US"));
        Assert.That(enUsDoc.Entries["hello"].Value, Is.EqualTo("Hello US"));

        ArbDocument ptBrDoc = ParseArb(Path.Combine(arbsDir, "pt_BR.arb"));
        Assert.That(ptBrDoc.Locale, Is.EqualTo("pt_BR"));
    }

    /// <summary>
    /// A l10n.yaml should be generated in the output folder with the config values.
    /// </summary>
    [Test]
    public void L10nYaml_GeneratedInOutputFolder()
    {
        string dir = Path.Combine(sourceDir, "Texts");
        Directory.CreateDirectory(dir);

        WriteResx(Path.Combine(dir, "Strings.en.resx"), ("Key", "Value"));

        L10nConfig config = new() {
            ArbDir = L10nConfig.DEFAULT_ARB_DIR,
            TemplateArbFile = "en.arb",
            OutputClass = "AppLocale",
            OutputNamespace = "My.Namespace"
        };

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, config, dryRun: false);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        string yamlPath = Path.Combine(outputDir, "l10n.yaml");
        Assert.That(File.Exists(yamlPath), Is.True, "Expected l10n.yaml in the output folder");

        string yamlContent = File.ReadAllText(yamlPath);
        Assert.That(yamlContent, Does.Contain("arb-dir: arbs"));
        Assert.That(yamlContent, Does.Contain("template-arb-file: en.arb"));
        Assert.That(yamlContent, Does.Contain("output-class: AppLocale"));
        Assert.That(yamlContent, Does.Contain("output-namespace: My.Namespace"));
    }

    /// <summary>
    /// Custom arb-dir should place arb files in the configured subdirectory.
    /// </summary>
    [Test]
    public void CustomArbDir_ArbsInConfiguredPath()
    {
        string dir = Path.Combine(sourceDir, "Texts");
        Directory.CreateDirectory(dir);

        WriteResx(Path.Combine(dir, "Strings.en.resx"), ("Key", "Value"));

        L10nConfig config = new() {
            ArbDir = "deep/deeper",
            TemplateArbFile = "en.arb"
        };

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, config, dryRun: false);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        string arbsDir = Path.Combine(outputDir, "deep", "deeper");
        Assert.That(File.Exists(Path.Combine(arbsDir, "en.arb")), Is.True,
            "en.arb should be in the custom arb-dir path");

        // l10n.yaml should still be at the output root
        string yamlContent = File.ReadAllText(Path.Combine(outputDir, "l10n.yaml"));
        Assert.That(yamlContent, Does.Contain("arb-dir: deep/deeper"));
    }

    /// <summary>
    /// Dry run should not write any files but should report what would be written.
    /// </summary>
    [Test]
    public void DryRun_NoFilesWritten()
    {
        string dir = Path.Combine(sourceDir, "Texts");
        Directory.CreateDirectory(dir);

        WriteResx(Path.Combine(dir, "Strings.en.resx"), ("Key", "Value"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: true);

        Assert.That(result.WrittenFiles, Is.Empty);
        Assert.That(result.PlannedWrites, Is.Not.Empty);
        Assert.That(Directory.Exists(Path.Combine(outputDir, "arbs")), Is.False);
        Assert.That(File.Exists(Path.Combine(outputDir, "l10n.yaml")), Is.False);
    }

    /// <summary>
    /// Without --deduplicate: keys that appear in more than one source group always get a
    /// _baseStem suffix regardless of whether their values match.
    /// </summary>
    [Test]
    public void Collision_WithoutDeduplicate_AlwaysSuffixed()
    {
        string dirA = Path.Combine(sourceDir, "A");
        string dirB = Path.Combine(sourceDir, "B");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);

        // Both groups have key "Title" with identical values but distinct base stems
        WriteResx(Path.Combine(dirA, "Alpha.en.resx"), ("Title", "Hello"), ("UniqueA", "Only in A"));
        WriteResx(Path.Combine(dirB, "Beta.en.resx"), ("Title", "Hello"), ("UniqueB", "Only in B"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false,
                                                      deduplicate: false);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        ArbDocument doc = ParseArb(Path.Combine(outputDir, "arbs", "en.arb"));

        // Unsuffixed "title" must NOT exist — both copies should be suffixed
        Assert.That(doc.Entries.ContainsKey("title"), Is.False,
            "Colliding key should not appear without suffix when deduplication is off");

        // Both suffixed variants must exist with their distinct base stem suffixes
        Assert.That(doc.Entries.ContainsKey("title_alpha"), Is.True);
        Assert.That(doc.Entries.ContainsKey("title_beta"), Is.True);
        Assert.That(doc.Entries["title_alpha"].Value, Is.EqualTo("Hello"));
        Assert.That(doc.Entries["title_beta"].Value, Is.EqualTo("Hello"));

        // Non-colliding keys are unaffected
        Assert.That(doc.Entries["uniqueA"].Value, Is.EqualTo("Only in A"));
        Assert.That(doc.Entries["uniqueB"].Value, Is.EqualTo("Only in B"));
    }

    /// <summary>
    /// Without --deduplicate: keys from distinct base stems get distinct suffixed keys.
    /// </summary>
    [Test]
    public void Collision_WithoutDeduplicate_DifferentValues_BothSuffixed()
    {
        string dirA = Path.Combine(sourceDir, "A");
        string dirB = Path.Combine(sourceDir, "B");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);

        WriteResx(Path.Combine(dirA, "Alpha.en.resx"), ("Submit", "Send"));
        WriteResx(Path.Combine(dirB, "Beta.en.resx"), ("Submit", "Submit"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false,
                                                      deduplicate: false);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        ArbDocument doc = ParseArb(Path.Combine(outputDir, "arbs", "en.arb"));

        Assert.That(doc.Entries.ContainsKey("submit"), Is.False, "Colliding key should be suffixed");
        Assert.That(doc.Entries.ContainsKey("submit_alpha"), Is.True);
        Assert.That(doc.Entries.ContainsKey("submit_beta"), Is.True);
        Assert.That(doc.Entries["submit_alpha"].Value, Is.EqualTo("Send"));
        Assert.That(doc.Entries["submit_beta"].Value, Is.EqualTo("Submit"));
    }

    /// <summary>
    /// With --deduplicate: keys that are identical across all locales are collapsed to one entry.
    /// </summary>
    [Test]
    public void Collision_WithDeduplicate_IdenticalValues_Collapsed()
    {
        string dirA = Path.Combine(sourceDir, "A");
        string dirB = Path.Combine(sourceDir, "B");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);

        // "Cancel" is identical in both groups across all locales
        WriteResx(Path.Combine(dirA, "Alpha.en.resx"), ("Cancel", "Cancel"));
        WriteResx(Path.Combine(dirA, "Alpha.cs.resx"), ("Cancel", "Zrušit"));
        WriteResx(Path.Combine(dirB, "Beta.en.resx"), ("Cancel", "Cancel"));
        WriteResx(Path.Combine(dirB, "Beta.cs.resx"), ("Cancel", "Zrušit"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false,
                                                      deduplicate: true);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        ArbDocument enDoc = ParseArb(Path.Combine(outputDir, "arbs", "en.arb"));
        ArbDocument csDoc = ParseArb(Path.Combine(outputDir, "arbs", "cs.arb"));

        // Should be collapsed to a single unsuffixed key
        Assert.That(enDoc.Entries.ContainsKey("cancel"), Is.True, "Identical key should be deduplicated");
        Assert.That(enDoc.Entries.ContainsKey("cancel_alpha"), Is.False);
        Assert.That(enDoc.Entries.ContainsKey("cancel_beta"), Is.False);
        Assert.That(enDoc.Entries["cancel"].Value, Is.EqualTo("Cancel"));
        Assert.That(csDoc.Entries["cancel"].Value, Is.EqualTo("Zrušit"));
    }

    /// <summary>
    /// With --deduplicate: if any locale has a differing value, all variants are suffixed.
    /// </summary>
    [Test]
    public void Collision_WithDeduplicate_DifferingLocale_AllSuffixed()
    {
        string dirA = Path.Combine(sourceDir, "A");
        string dirB = Path.Combine(sourceDir, "B");
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);

        // "Title" matches in en but differs in cs
        WriteResx(Path.Combine(dirA, "Alpha.en.resx"), ("Title", "Title"));
        WriteResx(Path.Combine(dirA, "Alpha.cs.resx"), ("Title", "Nadpis A"));
        WriteResx(Path.Combine(dirB, "Beta.en.resx"), ("Title", "Title"));
        WriteResx(Path.Combine(dirB, "Beta.cs.resx"), ("Title", "Nadpis B"));

        MigrationResult result = ResxMigrator.Migrate(sourceDir, outputDir, DefaultConfig(), dryRun: false,
                                                      deduplicate: true);
        Assert.That(result.Errors, Is.Empty, string.Join("; ", result.Errors));

        ArbDocument csDoc = ParseArb(Path.Combine(outputDir, "arbs", "cs.arb"));

        Assert.That(csDoc.Entries.ContainsKey("title"), Is.False, "Differing key should still be suffixed");
        Assert.That(csDoc.Entries.ContainsKey("title_alpha"), Is.True);
        Assert.That(csDoc.Entries.ContainsKey("title_beta"), Is.True);
        Assert.That(csDoc.Entries["title_alpha"].Value, Is.EqualTo("Nadpis A"));
        Assert.That(csDoc.Entries["title_beta"].Value, Is.EqualTo("Nadpis B"));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void WriteResx(string path, params (string Name, string Value)[] entries)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using StreamWriter sw = new(path, false, Constants.UTF8_NO_BOM);
        sw.WriteLine("""<?xml version="1.0" encoding="utf-8"?>""");
        sw.WriteLine("<root>");
        sw.WriteLine("""  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>""");
        sw.WriteLine("""  <resheader name="version"><value>2.0</value></resheader>""");
        foreach ((string name, string value) in entries) {
            sw.WriteLine($"""  <data name="{name}" xml:space="preserve"><value>{value}</value></data>""");
        }
        sw.WriteLine("</root>");
    }

    private static ArbDocument ParseArb(string path)
    {
        Assert.That(File.Exists(path), Is.True, $"Expected file to exist: {path}");
        string content = File.ReadAllText(path);
        ArbParseResult parseResult = new ArbParser().ParseContent(content);
        Assert.That(parseResult.ValidationResults.IsValid, Is.True,
            $"ARB parse failed for {path}: {string.Join("; ", parseResult.ValidationResults.Errors)}");
        return parseResult.Document;
    }
}