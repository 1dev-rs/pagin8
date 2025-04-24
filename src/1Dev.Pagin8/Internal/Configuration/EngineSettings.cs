namespace _1Dev.Pagin8.Internal.Configuration;

public record EngineSettings 
{
    public required string Negation { get; set; }

    public List<string> GroupOperators { get; init; } = [];

    public required string InOperator { get; set; }

    public List<string> ComparisonOperators { get; init; } = [];

    public required string IsOperator { get; set; }

    public List<string> DateRangeOperators { get; init; } = [];

    public List<string> ArrayOperators { get; init; } = [];

    public List<string> PossibleOrder { get; init; } = [];

    public List<string> MetaInclude { get; init; } = [];

    public required string QueryJoinKeyword { get; set; }
}