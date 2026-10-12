using System.Text.RegularExpressions;

namespace Arb.NET.Test;

[TestFixture]
public class NewLineTests {
    private static readonly Regex BARE_LF = new("(?<!\r)\n");

    private static ArbDocument SampleDocument() => new() {
        Locale = "en",
        Entries = new Dictionary<string, ArbEntry> {
            ["plain"] = new() { Key = "plain", Value = "Hello", Metadata = new ArbMetadata { Description = "Greeting" } },
            ["withParam"] = new() { Key = "withParam", Value = "Hi {name}" }
        }
    };

    [TestCase("lf", "\n")]
    [TestCase("CRLF", "\r\n")]
    [TestCase(" cr ", "\r")]
    [TestCase("unset", null)]
    [TestCase("", null)]
    [TestCase(null, null)]
    public void FromEndOfLine_MapsEditorConfigValues(string? value, string? expected) {
        Assert.That(NewLineHelper.FromEndOfLine(value), Is.EqualTo(expected));
    }

    [TestCase("a\nb", "\n")]
    [TestCase("a\r\nb", "\r\n")]
    [TestCase("a\rb", "\r")]
    [TestCase("no line break", null)]
    public void Detect_ReturnsFirstLineEnding(string content, string? expected) {
        Assert.That(NewLineHelper.Detect(content), Is.EqualTo(expected));
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public void GenerateClass_UsesRequestedNewLine_Consistently(string newLine) {
        string source = new ArbCodeGenerator(newLine).GenerateClass(SampleDocument(), "Loc", "Test");

        string other = newLine == "\n" ? "\r\n" : "\n";
        Assert.That(source, Does.Contain(newLine));
        Assert.That(source.Replace(newLine, string.Empty), Does.Not.Contain("\n").And.Not.Contain("\r"));
        Assert.That(source, Does.Not.Contain(other).Or.EqualTo(source), "no mixed endings");
    }

    [Test]
    public void GenerateClass_DefaultNewLine_IsNeverMixed() {
        string source = new ArbCodeGenerator().GenerateClass(SampleDocument(), "Loc", "Test");

        Assert.That(BARE_LF.IsMatch(source) && source.Contains("\r\n"), Is.False);
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public void GenerateFiles_AppliesNewLineToLocaleAndDispatcherFiles(string newLine) {
        IReadOnlyList<ArbGeneration.Output> outputs = ArbGeneration.GenerateFiles(
            [new ArbGeneration.Input("app_en", SampleDocument())],
            "Test",
            "AppLocale",
            "en",
            enumLocalizations: null,
            newLine: newLine);

        Assert.That(outputs, Has.Count.EqualTo(2));
        foreach (ArbGeneration.Output output in outputs) {
            Assert.That(output.Content.Replace(newLine, string.Empty), Does.Not.Contain("\n").And.Not.Contain("\r"), output.FileName);
        }
    }

    [Test]
    public void GenerateFiles_DispatcherEnumOrder_FollowsInputOrder() {
        EnumLocalizationInfo[] enums = [
            new() { FullName = "global::A", SimpleName = "A", Members = ["X"] },
            new() { FullName = "global::B", SimpleName = "B", Members = ["Y"] }
        ];

        string dispatcher = ArbGeneration.GenerateFiles(
                [new ArbGeneration.Input("app_en", SampleDocument())],
                "Test", "AppLocale", "en", enums, "\n")
            .Single(o => o.FileName == "AppLocaleDispatcher.g.cs").Content;

        Assert.That(dispatcher.IndexOf("Localize(global::A", StringComparison.Ordinal),
                    Is.LessThan(dispatcher.IndexOf("Localize(global::B", StringComparison.Ordinal)));
    }

    [TestCase("\n")]
    [TestCase("\r\n")]
    public void Serialize_UsesRequestedNewLine(string newLine) {
        string arb = ArbSerializer.Serialize(SampleDocument(), newLine);

        Assert.That(arb, Does.Contain(newLine));
        Assert.That(arb.Replace(newLine, string.Empty), Does.Not.Contain("\n").And.Not.Contain("\r"));
    }

    [Test]
    public void EditorConfig_ResolvesEndOfLine_WithNearestFileWinning() {
        string root = Path.Combine(Path.GetTempPath(), "Arb.NET.Tests", Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(Path.Combine(root, "proj", "arbs"));
            Directory.CreateDirectory(Path.Combine(root, "other"));
            File.WriteAllText(Path.Combine(root, ".editorconfig"), "root = true\n[*]\nend_of_line = crlf\n[*.arb]\nend_of_line = lf\n");
            File.WriteAllText(Path.Combine(root, "proj", ".editorconfig"), "[*.{g.cs,cs}]\nend_of_line = cr\n");

            Assert.That(EditorConfigHelper.ResolveNewLine(Path.Combine(root, "proj", "arbs", "en.g.cs")), Is.EqualTo("\r"));
            Assert.That(EditorConfigHelper.ResolveNewLine(Path.Combine(root, "proj", "arbs", "en.arb")), Is.EqualTo("\n"));
            Assert.That(EditorConfigHelper.ResolveNewLine(Path.Combine(root, "other", "x.cs")), Is.EqualTo("\r\n"));
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void EditorConfig_RootStopsSearch_AndUnsetClearsValue() {
        string root = Path.Combine(Path.GetTempPath(), "Arb.NET.Tests", Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(Path.Combine(root, "a", "b"));
            File.WriteAllText(Path.Combine(root, ".editorconfig"), "[*]\nend_of_line = crlf\n");
            File.WriteAllText(Path.Combine(root, "a", ".editorconfig"), "root = true\n");
            File.WriteAllText(Path.Combine(root, "a", "b", ".editorconfig"), "[*.txt]\nend_of_line = unset\n");

            Assert.That(EditorConfigHelper.ResolveNewLine(Path.Combine(root, "a", "b", "x.cs")), Is.Null);
            Assert.That(EditorConfigHelper.ResolveNewLine(Path.Combine(root, "a", "b", "x.txt")), Is.Null);
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ProjectGenerator_HonorsEditorConfigEndOfLine() {
        string root = Path.Combine(Path.GetTempPath(), "Arb.NET.Tests", Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(Path.Combine(root, "arbs"));
            File.WriteAllText(Path.Combine(root, ".editorconfig"), "root = true\n[*]\nend_of_line = lf\n");
            File.WriteAllText(Path.Combine(root, "Sample.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
            File.WriteAllText(Path.Combine(root, "l10n.yaml"), "arb-dir: arbs\ntemplate-arb-file: app_en.arb\noutput-class: AppLocale\n");
            File.WriteAllText(Path.Combine(root, "arbs", "app_en.arb"), "{ \"@@locale\": \"en\", \"title\": \"Hello\" }");

            ArbProjectGenerator.Result result = ArbProjectGenerator.Generate(root);

            Assert.That(result.HasErrors, Is.False, string.Join(Environment.NewLine, result.Errors));
            foreach (string file in result.WrittenFiles) {
                Assert.That(File.ReadAllText(file), Does.Not.Contain("\r"), file);
            }
        }
        finally {
            Directory.Delete(root, recursive: true);
        }
    }
}
