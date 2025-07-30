using _1Dev.Pagin8.Internal.Configuration;

namespace Pagin8.Internal.Configuration;

public static class EngineDefaults
{
    public static readonly EngineSettings Config = new()
    {
        Negation = "not",
        QueryJoinKeyword = "AND",
        GroupOperators = ["or", "and"],
        ComparisonOperators = ["eq", "gt", "gte", "lt", "lte", "like", "in", "stw", "enw", "cs"],
        DateRangeOperators = ["ago", "for"],
        ArrayOperators = ["incl", "excl"],
        InOperator = "in",
        IsOperator = "is",
        PossibleOrder = ["asc", "desc"],
        MetaInclude = ["filters", "subscriptions", "columns"]
    };
}
