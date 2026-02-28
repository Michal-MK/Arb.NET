using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Arb.NET.IDE.VisualStudio.Tool.Models;
using EnvDTE;
using Microsoft.VisualStudio.Shell;

namespace Arb.NET.IDE.VisualStudio.Tool.Services;

public class ArbService(ArbPackage package) {

    private readonly ArbParser parser = new();

    private string? GetSolutionDirectory() {
        ThreadHelper.ThrowIfNotOnUIThread();
        DTE? dte = ((IServiceProvider)package).GetService(typeof(DTE)) as DTE;
        string? solutionFullPath = dte?.Solution?.FullName;
        return string.IsNullOrEmpty(solutionFullPath)
            ? null
            : Path.GetDirectoryName(solutionFullPath);
    }

    public async Task<ArbScanResult> ScanArbFilesAsync() {
        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
        string? solutionDir = GetSolutionDirectory();

        if (string.IsNullOrEmpty(solutionDir)) {
            return new ArbScanResult([], [], solutionNotLoaded: true);
        }

        return await Task.Run(() => {
            Dictionary<string, List<ArbFile>> byDir = new(StringComparer.OrdinalIgnoreCase);
            List<Exception> errors = [];

            foreach (string filePath in Directory.EnumerateFiles(solutionDir, "*.arb", SearchOption.AllDirectories)) {
                try {
                    string content = File.ReadAllText(filePath);
                    ArbParseResult result = parser.ParseContent(content);
                    if (result.Document == null) continue;

                    ArbDocument doc = result.Document;
                    string locale = string.IsNullOrEmpty(doc.Locale)
                        ? Path.GetFileNameWithoutExtension(filePath)
                        : doc.Locale;

                    string? dir = Path.GetDirectoryName(filePath) ?? solutionDir;


                    if (dir is null) {
                        // TODO(handle)
                        continue;
                    }

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

            return new ArbScanResult(byDir, errors, solutionDirectory: solutionDir);
        });
    }
}