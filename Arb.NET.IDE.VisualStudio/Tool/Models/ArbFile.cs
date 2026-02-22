namespace Arb.NET.IDE.VisualStudio.Tool.Models;

public sealed class ArbFile(string langCode, string filePath) {
    public string LangCode => langCode;
    public string FilePath => filePath;
}