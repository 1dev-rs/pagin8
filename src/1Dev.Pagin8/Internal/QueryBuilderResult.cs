﻿using InterpolatedSql.Dapper.SqlBuilders;

namespace _1Dev.Pagin8.Internal;

public record QueryBuilderResult
{
    public QueryBuilder Builder { get; set; }

    public Meta Meta { get; set; } = new();
}