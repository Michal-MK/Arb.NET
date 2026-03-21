namespace Arb.NET.Test;

[TestFixture]
public class ArbProjectGeneratorTests {
    [Test]
    public void Generate_UsesProjectRootNamespace_WhenOutputNamespaceIsMissing() {
        string projectDir = CreateTempProject();

        try {
            WriteFile(Path.Combine(projectDir, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <RootNamespace>My.Root.Namespace</RootNamespace>
                  </PropertyGroup>
                </Project>
                """);

            WriteFile(Path.Combine(projectDir, "l10n.yaml"), """
                arb-dir: arbs
                template-arb-file: app_en.arb
                output-class: AppLocale
                """);

            WriteFile(Path.Combine(projectDir, "arbs", "app_en.arb"), """
                {
                  "@@locale": "en",
                  "appTitle": "Hello"
                }
                """);

            ArbProjectGenerator.Result result = ArbProjectGenerator.Generate(projectDir);

            Assert.That(result.HasErrors, Is.False, string.Join(Environment.NewLine, result.Errors));

            string generated = File.ReadAllText(Path.Combine(projectDir, "arbs", "app_en.g.cs"));
            Assert.That(generated, Does.Contain("namespace My.Root.Namespace;"));
        }
        finally {
            DeleteProjectDir(projectDir);
        }
    }

    [Test]
    public void Generate_WritesDispatcherFile_ForOutputClassProjects() {
        string projectDir = CreateTempProject();

        try {
            WriteFile(Path.Combine(projectDir, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            WriteFile(Path.Combine(projectDir, "l10n.yaml"), """
                arb-dir: arbs
                template-arb-file: app_en.arb
                output-class: AppLocale
                """);

            WriteFile(Path.Combine(projectDir, "arbs", "app_en.arb"), """
                {
                  "@@locale": "en",
                  "appTitle": "Hello"
                }
                """);

            WriteFile(Path.Combine(projectDir, "arbs", "app_cs.arb"), """
                {
                  "@@locale": "cs",
                  "appTitle": "Ahoj"
                }
                """);

            ArbProjectGenerator.Result result = ArbProjectGenerator.Generate(projectDir);

            Assert.That(result.HasErrors, Is.False, string.Join(Environment.NewLine, result.Errors));
            Assert.That(File.Exists(Path.Combine(projectDir, "arbs", "AppLocaleDispatcher.g.cs")), Is.True);
        }
        finally {
            DeleteProjectDir(projectDir);
        }
    }

    [Test]
    public void Generate_IncludesEnumDispatcherOverloads_WhenRequested() {
        string projectDir = CreateTempProject();

        try {
            WriteFile(Path.Combine(projectDir, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            WriteFile(Path.Combine(projectDir, "l10n.yaml"), """
                arb-dir: arbs
                template-arb-file: app_en.arb
                output-class: AppLocale
                """);

            WriteFile(Path.Combine(projectDir, "arbs", "app_en.arb"), """
                {
                  "@@locale": "en",
                  "statusActive": "Active"
                }
                """);

            ArbProjectGenerator.Result result = ArbProjectGenerator.Generate(projectDir, [
                new EnumLocalizationInfo {
                    FullName = "My.Status",
                    SimpleName = "Status",
                    Members = ["Active"]
                }
            ]);

            Assert.That(result.HasErrors, Is.False, string.Join(Environment.NewLine, result.Errors));

            string dispatcher = File.ReadAllText(Path.Combine(projectDir, "arbs", "AppLocaleDispatcher.g.cs"));
            Assert.That(dispatcher, Does.Contain("public string Localize(My.Status value)"));
        }
        finally {
            DeleteProjectDir(projectDir);
        }
    }

    [Test]
    public void Generate_WritesGeneratedFiles_WithoutUtf8Bom() {
        string projectDir = CreateTempProject();

        try {
            WriteFile(Path.Combine(projectDir, "Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);

            WriteFile(Path.Combine(projectDir, "l10n.yaml"), """
                arb-dir: arbs
                template-arb-file: app_en.arb
                output-class: AppLocale
                """);

            WriteFile(Path.Combine(projectDir, "arbs", "app_en.arb"), """
                {
                  "@@locale": "en",
                  "appTitle": "Hello"
                }
                """);

            ArbProjectGenerator.Result result = ArbProjectGenerator.Generate(projectDir);

            Assert.That(result.HasErrors, Is.False, string.Join(Environment.NewLine, result.Errors));
            Assert.That(HasUtf8Bom(Path.Combine(projectDir, "arbs", "app_en.g.cs")), Is.False);
            Assert.That(HasUtf8Bom(Path.Combine(projectDir, "arbs", "AppLocaleDispatcher.g.cs")), Is.False);
        }
        finally {
            DeleteProjectDir(projectDir);
        }
    }

    private static string CreateTempProject() {
        string projectDir = Path.Combine(Path.GetTempPath(), "Arb.NET.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(projectDir);
        return projectDir;
    }

    private static void WriteFile(string path, string content) {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory)) {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content.Replace("\r\n", "\n").Replace("\n", Environment.NewLine), Constants.UTF8_NO_BOM);
    }

    private static bool HasUtf8Bom(string path) {
        byte[] bytes = File.ReadAllBytes(path);
        return bytes.Length >= 3 &&
               bytes[0] == 0xEF &&
               bytes[1] == 0xBB &&
               bytes[2] == 0xBF;
    }

    private static void DeleteProjectDir(string projectDir) {
        if (Directory.Exists(projectDir)) {
            Directory.Delete(projectDir, recursive: true);
        }
    }
}