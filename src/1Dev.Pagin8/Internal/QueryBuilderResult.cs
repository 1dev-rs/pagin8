using InterpolatedSql.Dapper.SqlBuilders;

namespace _1Dev.Pagin8.Internal;

public record QueryBuilderResult
{
    public required QueryBuilder Builder { get; set; }

    public Meta Meta { get; set; } = new();

    public bool ShouldSkipBuilder { get; set; } = false;
}