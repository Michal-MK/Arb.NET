using System.CommandLine;
using Arb.NET;
using Arb.NET.Models;
using Arb.NET.Tool.Migration;

RootCommand rootCommand = new("Arb.NET Tool — utilities for working with .arb localization files.");

Argument<DirectoryInfo> sourceArg = new("source") {
    Description = "Path to the folder containing .resx files to migrate.",
    Arity = ArgumentArity.ExactlyOne
};

Argument<DirectoryInfo> outputArg = new("output") {
    Description = "Path to the output project folder (will contain l10n.yaml and arb directory).",
    Arity = ArgumentArity.ExactlyOne
};

Option<bool> dryRunOption = new("--dry-run") {
    Description = "List the files that would be written without actually writing them."
};

Option<string> arbDirOption = new("--arb-dir") {
    Description = $"Relative path for the arb directory inside the output folder (default: \"{L10nConfig.DEFAULT_ARB_DIR}\")."
};

Option<string> templateOption = new("--template-arb-file") {
    Description = "Template .arb file name (default: \"en.arb\")."
};

Option<string?> outputClassOption = new("--output-class") {
    Description = "Base name for generated locale classes and the dispatcher."
};

Option<string?> outputNamespaceOption = new("--output-namespace") {
    Description = "Namespace for all generated code."
};

Option<bool> deduplicateOption = new("--deduplicate") {
    Description = "Merge colliding keys that have identical values across all locales into a single key. " +
                  "Keys with any differing value are still suffixed with their source file name."
};

Command migrateCommand = new("migrate", "Migrate .resx files in a solution to .arb format.") {
    sourceArg,
    outputArg,
    dryRunOption,
    arbDirOption,
    templateOption,
    outputClassOption,
    outputNamespaceOption,
    deduplicateOption
};

migrateCommand.SetAction(parseResult => {
    DirectoryInfo source = parseResult.GetValue(sourceArg)!;
    DirectoryInfo output = parseResult.GetValue(outputArg)!;
    bool dryRun = parseResult.GetValue(dryRunOption);

    if (!source.Exists) {
        Console.Error.WriteLine($"Error: source directory not found: {source.FullName}");
        return 1;
    }

    L10nConfig config = new() {
        ArbDir = parseResult.GetValue(arbDirOption) ?? L10nConfig.DEFAULT_ARB_DIR,
        TemplateArbFile = parseResult.GetValue(templateOption) ?? "en.arb",
        OutputClass = parseResult.GetValue(outputClassOption),
        OutputNamespace = parseResult.GetValue(outputNamespaceOption)
    };

    Console.WriteLine($"Migrating .resx files in: {source.FullName}");
    Console.WriteLine($"Output folder: {output.FullName}");
    if (dryRun) {
        Console.WriteLine("(dry run — no files will be written)");
    }

    bool deduplicate = parseResult.GetValue(deduplicateOption);
    MigrationResult result = ResxMigrator.Migrate(source.FullName, output.FullName, config, dryRun, deduplicate);

    if (dryRun) {
        if (result.PlannedWrites.Count == 0) {
            Console.WriteLine("No .resx files found.");
        }
        else {
            Console.WriteLine($"\nWould write {result.PlannedWrites.Count} file(s):");
            foreach (string f in result.PlannedWrites) {
                Console.WriteLine($"  {f}");
            }
        }
    }
    else {
        if (result.WrittenFiles.Count == 0 && !result.HasErrors) {
            Console.WriteLine("No .resx files found.");
        }
        else {
            Console.WriteLine($"\nWrote {result.WrittenFiles.Count} file(s):");
            foreach (string f in result.WrittenFiles) {
                Console.WriteLine($"  {f}");
            }
        }
    }

    if (result.HasErrors) {
        Console.Error.WriteLine($"\n{result.Errors.Count} error(s):");
        foreach (string e in result.Errors) {
            Console.Error.WriteLine($"  {e}");
        }
        return 2;
    }

    return 0;
});

// ── generate command ──────────────────────────────────────────────────────────

Argument<DirectoryInfo> generatePathArg = new("project-dir") {
    Description = "Directory containing l10n.yaml (walks up if omitted or not found there).",
    Arity = ArgumentArity.ZeroOrOne
};

Command generateCommand = new("generate", "Generate .g.cs files from .arb files and l10n.yaml.") {
    generatePathArg
};

generateCommand.SetAction(parseResult => {
    DirectoryInfo? dir = parseResult.GetValue(generatePathArg);
    string projectDir = dir?.FullName ?? Directory.GetCurrentDirectory();

    if (!Directory.Exists(projectDir)) {
        Console.Error.WriteLine($"Error: directory not found: {projectDir}");
        return 1;
    }

    Console.WriteLine($"Generating from: {projectDir}");

    ArbProjectGenerator.Result result = ArbProjectGenerator.Generate(projectDir);

    if (result.WrittenFiles.Count > 0) {
        Console.WriteLine($"\nWrote {result.WrittenFiles.Count} file(s):");
        foreach (string f in result.WrittenFiles) {
            Console.WriteLine($"  {f}");
        }
    }

    if (result.HasErrors) {
        Console.Error.WriteLine($"\n{result.Errors.Count} error(s):");
        foreach (string e in result.Errors) {
            Console.Error.WriteLine($"  {e}");
        }
        return 2;
    }

    return 0;
});

rootCommand.Subcommands.Add(migrateCommand);
rootCommand.Subcommands.Add(generateCommand);

return rootCommand.Parse(args).Invoke();