using System.Data;
using Dapper;
using InterpolatedSql.Dapper.SqlBuilders;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Extensions.Backend.Models;

namespace _1Dev.Pagin8.Extensions.Backend.Implementations;

/// <summary>
/// Filter provider implementation using Pagin8 SqlQueryBuilder.
/// </summary>
public class FilterProvider : IFilterProvider
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISqlQueryBuilder _sqlQueryBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterProvider"/> class.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory.</param>
    /// <param name="sqlQueryBuilder">The Pagin8 SQL query builder.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public FilterProvider(IDbConnectionFactory connectionFactory, ISqlQueryBuilder sqlQueryBuilder)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _sqlQueryBuilder = sqlQueryBuilder ?? throw new ArgumentNullException(nameof(sqlQueryBuilder));
    }

    /// <summary>
    /// Executes a filtered query and returns paged results.
    /// </summary>
    public async Task<PagedResults<TResponse>> GetAsync<TResponse>(string viewName, FilteredDataQuery query)
        where TResponse : class
    {
        using var connection = _connectionFactory.Create();

        var inputParams = QueryInputParameters.Create(
            sql: viewName,
            queryString: query.QueryString,
            defaultQueryString: query.DefaultQuery,
            ignoreLimit: query.IgnoreLimit,
            isJson: false,
            isCount: false
        );

        var qbParams = QueryBuilderParameters.Create(
            connection: connection,
            baseQuery: GetBaseQuery(viewName),
            inputParameters: inputParams
        );

        var buildResult = _sqlQueryBuilder.BuildSqlQuery<TResponse>(qbParams);

        if (buildResult.Builder is null)
        {
            return new PagedResults<TResponse>
            {
                Data = [],
                TotalRows = 0,
                Meta = buildResult.Meta
            };
        }

        var data = await ExecuteQueryAsync<TResponse>(connection, buildResult.Builder);

        var count = buildResult.Meta.ShowCount
            ? await GetCountInternalAsync<TResponse>(connection, viewName, query)
            : 0;

        return new PagedResults<TResponse>
        {
            Data = data,
            TotalRows = count,
            Meta = buildResult.Meta
        };
    }

    /// <summary>
    /// Gets the count of rows matching the filter.
    /// </summary>
    public async Task<int> GetCountAsync<TResponse>(string viewName, FilteredDataQuery query)
        where TResponse : class
    {
        using var connection = _connectionFactory.Create();
        return await GetCountInternalAsync<TResponse>(connection, viewName, query);
    }

    private async Task<int> GetCountInternalAsync<TResponse>(
        IDbConnection connection,
        string viewName,
        FilteredDataQuery query) where TResponse : class
    {
        var inputParams = QueryInputParameters.Create(
            sql: viewName,
            queryString: query.QueryString,
            defaultQueryString: query.DefaultQuery,
            ignoreLimit: true,
            isJson: false,
            isCount: true
        );

        var qbParams = QueryBuilderParameters.Create(
            connection: connection,
            baseQuery: GetBaseCountQuery(viewName),
            inputParameters: inputParams
        );

        var buildResult = _sqlQueryBuilder.BuildSqlQuery<TResponse>(qbParams);

        if (buildResult.Builder is null)
            return 0;

        var builtSql = buildResult.Builder.Build();
        return await connection.ExecuteScalarAsync<int>(
            builtSql.Sql,
            builtSql.SqlParameters);
    }

    private static async Task<IEnumerable<TResponse>> ExecuteQueryAsync<TResponse>(
        IDbConnection connection,
        QueryBuilder builder)
    {
        var builtSql = builder.Build();
        return await connection.QueryAsync<TResponse>(builtSql.Sql, builtSql.SqlParameters);
    }

    private static FormattableString GetBaseQuery(string viewName)
        => $"/**select**/ FROM {viewName:raw} WHERE 1=1 /**filters**/";

    private static FormattableString GetBaseCountQuery(string viewName)
        => $"SELECT COUNT(*) FROM {viewName:raw} WHERE 1=1 /**filters**/";
}
