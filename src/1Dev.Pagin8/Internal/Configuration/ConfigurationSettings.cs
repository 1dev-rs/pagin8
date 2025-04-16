namespace _1Dev.Pagin8.Internal.Configuration;

public record ConfigurationSettings
{
    public string Negation { get; set; }

    public List<string> GroupOperators { get; } = [];

    public string InOperator { get; set; }

    public List<string> ComparisonOperators { get; } = [];

    public string IsOperator { get; set; }

    public List<string> DateRangeOperators { get; } = [];

    public List<string> ArrayOperators { get; } = [];

    public List<string> PossibleOrder { get; } = [];

    public List<string> MetaInclude { get; } = [];

    public int MaxNestingLevel { get; set; }

    public PagingSettings PagingSettings { get; set; }

    public string QueryJoinKeyword { get; set; }
}