using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Arb.NET.IDE.Common.Services;
using Arb.NET.IDE.VisualStudio.Tool.Models;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace Arb.NET.IDE.VisualStudio.Tool.Services;

public class ArbService(ArbPackage package) {

    private readonly ArbParser parser = new();

    private IReadOnlyList<string> GetScanRoots() {
        ThreadHelper.ThrowIfNotOnUIThread();
        if (((IServiceProvider)package).GetService(typeof(DTE)) is not DTE dte) {
            return [];
        }

        HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

        string? solutionFullPath = dte.Solution?.FullName;
        if (!string.IsNullOrEmpty(solutionFullPath)) {
            string? solutionDir = Path.GetDirectoryName(solutionFullPath);
            if (!string.IsNullOrWhiteSpace(solutionDir)) {
                roots.Add(solutionDir);
            }
        }

        AddProjectRoots(dte.Solution?.Projects, roots);

        return roots.ToList();
    }

    public async Task<ArbScanResult> ScanArbFilesAsync() {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        IReadOnlyList<string> scanRoots = GetScanRoots();

        if (scanRoots.Count == 0) {
            return new ArbScanResult([], [], solutionNotLoaded: true);
        }

        string displayRoot = scanRoots[0];

        return await Task.Run(() => {
            Dictionary<string, List<ArbFile>> byDir = new(StringComparer.OrdinalIgnoreCase);
            List<Exception> errors = [];

            foreach (string filePath in ArbKeyService.FindArbFiles(scanRoots)) {
                try {
                    string content = File.ReadAllText(filePath);
                    ArbParseResult result = parser.ParseContent(content);
                    if (result.Document == null) continue;

                    ArbDocument doc = result.Document;
                    string locale = string.IsNullOrEmpty(doc.Locale)
                        ? Path.GetFileNameWithoutExtension(filePath)
                        : doc.Locale;

                    string dir = Path.GetDirectoryName(filePath) ?? displayRoot;

                    if (!byDir.TryGetValue(dir, out List<ArbFile> list)) {
                        list = [];
                        byDir[dir] = list;
                    }
                    list.Add(new ArbFile(locale, filePath));
                }
                catch (Exception ex) {
                    errors.Add(ex);
                }
            }

            return new ArbScanResult(byDir, errors, solutionDirectory: displayRoot);
        });
    }

    private static void AddProjectRoots(Projects? projects, HashSet<string> roots) {
        if (projects == null) return;

        foreach (Project project in projects) {
            AddProjectRoot(project, roots);
        }
    }

    private static void AddProjectRoot(Project? project, HashSet<string> roots) {
        if (project == null) return;

        try {
            if (!string.IsNullOrWhiteSpace(project.FullName)) {
                string? projectDir = Path.GetDirectoryName(project.FullName);
                if (!string.IsNullOrWhiteSpace(projectDir)) {
                    roots.Add(projectDir);
                }
            }
        }
        catch {
            // Ignore transient DTE/project-model failures and keep scanning other roots.
        }

        try {
            ProjectItems? projectItems = project.ProjectItems;
            if (projectItems == null) return;

            foreach (ProjectItem item in projectItems) {
                AddProjectRoot(item.SubProject, roots);
            }
        }
        catch {
            // Solution folders and CPS projects can throw here; that's fine.
        }
    }
}