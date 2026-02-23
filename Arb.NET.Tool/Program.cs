using System.CommandLine;
using Arb.NET.Tool.Migration;

RootCommand rootCommand = new("Arb.NET Tool — utilities for working with .arb localization files.");

Argument<DirectoryInfo> pathArg = new("path") {
    Description = "Path to the folder containing the solution (.sln) file.",
    Arity = ArgumentArity.ExactlyOne
};

Option<bool> dryRunOption = new("--dry-run") {
    Description = "List the files that would be written without actually writing them."
};

Command migrateCommand = new("migrate", "Migrate .resx files in a solution to .arb format.") {
    pathArg,
    dryRunOption
};

migrateCommand.SetAction(parseResult => {
    DirectoryInfo path = parseResult.GetValue(pathArg)!;
    bool dryRun = parseResult.GetValue(dryRunOption);

    if (!path.Exists) {
        Console.Error.WriteLine($"Error: directory not found: {path.FullName}");
        return 1;
    }

    Console.WriteLine($"Migrating .resx files in: {path.FullName}");
    if (dryRun) {
        Console.WriteLine("(dry run — no files will be written)");
    }

    MigrationResult result = ResxMigrator.Migrate(path.FullName, dryRun);

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

rootCommand.Subcommands.Add(migrateCommand);

return rootCommand.Parse(args).Invoke();