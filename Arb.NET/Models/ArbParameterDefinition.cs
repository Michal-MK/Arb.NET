namespace Arb.NET;

public record ArbParameterDefinition {
    /// <summary>
    /// Content of the brackets
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Points to the opening '{' in the original string
    /// </summary>
    public int StartIndex { get; set; }
    
    /// <summary>
    /// Points to the character after the closing '}' in the original string
    /// </summary>
    public int EndIndex { get; set; }
}

public record ArbPluralizationParameterDefinition : ArbParameterDefinition {
    public Dictionary<int, string> CountableParameters { get; set; } = [];
    public string OtherParameter { get; set; } = "";
}