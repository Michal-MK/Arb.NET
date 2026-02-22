using System;
using System.Collections.Generic;
using System.IO;

namespace Arb.NET.IDE.VisualStudio.Tool.Models;

public class ArbScanResult(Dictionary<string, List<ArbFile>> dirGroupedArbFiles, List<Exception> arbErrors, bool solutionNotLoaded = false, string? solutionDirectory = null) {
    public Dictionary<string, List<ArbFile>> DirGroupedArbFiles => dirGroupedArbFiles;

    public List<Exception> ArbErrors => arbErrors;

    public bool SolutionNotLoaded => solutionNotLoaded;

    public string RelativePath(string absoluteDir) {
        if (solutionDirectory is null) return absoluteDir;
        if (!absoluteDir.StartsWith(solutionDirectory, StringComparison.OrdinalIgnoreCase)) return absoluteDir;
        string relative = absoluteDir.Substring(solutionDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.IsNullOrEmpty(relative) ? "." : relative;
    }
}