using System;
using System.Collections.Generic;

namespace Arb.NET.IDE.VisualStudio.Tool.Models;

public class ArbScanResult(Dictionary<string, List<ArbFile>> dirGroupedArbFiles, List<Exception> arbErrors, bool solutionNotLoaded = false) {
    public Dictionary<string, List<ArbFile>> DirGroupedArbFiles { get; } = dirGroupedArbFiles;

    public List<Exception> ArbErrors { get; } = arbErrors;
    
    public bool SolutionNotLoaded { get; } = solutionNotLoaded;
}