using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Application.Parts;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.Rd.Tasks;
using JetBrains.ReSharper.Feature.Services.Protocol;
using JetBrains.Rider.Model;

namespace Arb.NET.IDE.Jetbrains.Shared;

/// <summary>
/// Rider backend component that wires up the ArbModel protocol endpoint.
/// When the Kotlin side calls <c>getArbData</c>, this handler scans the
/// solution for *.arb files, parses them with <see cref="ArbParser"/>, and
/// returns the structured data.
/// </summary>
[SolutionComponent(Instantiation.ContainerAsyncPrimaryThread)]
public class ArbModelHost {
    public ArbModelHost(ISolution solution, Lifetime lifetime) {
        ArbModel model = solution.GetProtocolSolution().GetArbModel();

        model.GetArbData.SetSync((_, _) => CollectArbData(solution));
    }

    private static List<ArbLocaleData> CollectArbData(ISolution solution) {
        string solutionDir = solution.SolutionFilePath.Directory.FullPath;
        IEnumerable<string> arbFiles = Directory.EnumerateFiles(solutionDir, "*.arb", SearchOption.AllDirectories);

        List<ArbLocaleData> result = new();
        ArbParser parser = new();

        foreach (string filePath in arbFiles) {
            string content = File.ReadAllText(filePath);
            ArbParseResult parseResult = parser.ParseContent(content);

            if (parseResult.Document == null) continue;

            ArbDocument doc = parseResult.Document;
            string locale = string.IsNullOrEmpty(doc.Locale)
                ? Path.GetFileNameWithoutExtension(filePath)
                : doc.Locale;

            List<JetBrains.Rider.Model.ArbEntry> entries = doc.Entries
                .Select(kvp => new JetBrains.Rider.Model.ArbEntry(kvp.Key, kvp.Value.Value))
                .ToList();

            result.Add(new ArbLocaleData(locale, entries));
        }

        return result;
    }
}