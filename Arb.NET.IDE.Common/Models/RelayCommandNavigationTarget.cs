namespace Arb.NET.IDE.Common.Models;

public class RelayCommandNavigationTarget {
    public string FilePath { get; }
    public int LineNumber { get; }
    public int Column { get; }
    public string MethodName { get; }

    public RelayCommandNavigationTarget(string filePath, int lineNumber, int column, string methodName) {
        FilePath = filePath;
        LineNumber = lineNumber;
        Column = column;
        MethodName = methodName;
    }
}