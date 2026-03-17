using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Extensions.Backend.Models;
using _1Dev.Pagin8.Input;
using Attributes;
using Dapper;
using InterpolatedSql.Dapper;
using System.Data;
using System.Reflection;
using System.Text.Json;

namespace _1Dev.Pagin8.Extensions.Backend.Implementations;

/// <summary>
/// Filter provider implementation using Pagin8 SqlQueryBuilder.
/// </summary>
public class FilterProvider : IFilterProvider
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ISqlQueryBuilder _sqlQueryBuilder;
    private readonly int? _defaultCommandTimeout;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterProvider"/> class.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory.</param>
    /// <param name="sqlQueryBuilder">The Pagin8 SQL query builder.</param>
    /// <param name="defaultCommandTimeout">Optional default SQL command timeout in seconds applied to every query. Null uses the Dapper default (30 s).</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
    public FilterProvider(IDbConnectionFactory connectionFactory, ISqlQueryBuilder sqlQueryBuilder, int? defaultCommandTimeout = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _sqlQueryBuilder = sqlQueryBuilder ?? throw new ArgumentNullException(nameof(sqlQueryBuilder));
        _defaultCommandTimeout = defaultCommandTimeout;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Executes a filtered query and returns paged results.
    /// </summary>
    public async Task<PagedResults<TResponse>> GetAsync<TResponse>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where TResponse : class
    {
        var timeout = commandTimeout ?? _defaultCommandTimeout;
        using var connection = _connectionFactory.Create();

        var inputParams = QueryInputParameters.Create(
            sql: viewName,
            queryString: query.QueryString,
            defaultQueryString: query.DefaultQuery,
            ignoreLimit: query.IgnoreLimit,
            isJson: query.IsJson,
            ignorePaging: false
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

        // Kick off both queries without awaiting — they run concurrently.
        // The count uses its own pooled connection so neither blocks the other.
        var dataTask = FetchDataAsync<TResponse>(buildResult.Builder, query, timeout);
        var countTask = buildResult.Meta.ShowCount
            ? CountOnSeparateConnectionAsync<TResponse>(viewName, query, timeout)
            : Task.FromResult(0);

        await Task.WhenAll(dataTask, countTask);

        return new PagedResults<TResponse>
        {
            Data = dataTask.Result,
            TotalRows = countTask.Result,
            Meta = buildResult.Meta
        };
    }

    private async Task<IEnumerable<TResponse>> FetchDataAsync<TResponse>(
        InterpolatedSql.Dapper.SqlBuilders.QueryBuilder builder,
        FilteredDataQuery query,
        int? timeout) where TResponse : class
    {
        if (query.IsJson)
        {
            var json = await builder.QueryFirstOrDefaultAsync<string>(commandTimeout: timeout);
            return string.IsNullOrEmpty(json) || json == "[]"
                ? []
                : JsonSerializer.Deserialize<IEnumerable<TResponse>>(json, JsonOptions) ?? [];
        }

        return await builder.QueryAsync<TResponse>(commandTimeout: timeout);
    }

    private async Task<int> CountOnSeparateConnectionAsync<TResponse>(
        string viewName,
        FilteredDataQuery query,
        int? timeout) where TResponse : class
    {
        using var countConnection = _connectionFactory.Create();
        return await GetCountInternalAsync<TResponse>(countConnection, viewName, query, timeout);
    }

    /// <summary>
    /// Gets the count of rows matching the filter.
    /// </summary>
    public async Task<int> GetCountAsync<TResponse>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where TResponse : class
    {
        var timeout = commandTimeout ?? _defaultCommandTimeout;
        using var connection = _connectionFactory.Create();
        return await GetCountInternalAsync<TResponse>(connection, viewName, query, timeout);
    }

    private async Task<int> GetCountInternalAsync<TResponse>(
        IDbConnection connection,
        string viewName,
        FilteredDataQuery query,
        int? commandTimeout = null) where TResponse : class
    {
        var inputParams = QueryInputParameters.Create(
            sql: viewName,
            queryString: query.QueryString,
            defaultQueryString: query.DefaultQuery,
            ignoreLimit: true,
            isJson: false,
            ignorePaging: true
        );

        var qbParams = QueryBuilderParameters.Create(
            connection: connection,
            baseQuery: GetBaseCountQuery(viewName),
            inputParameters: inputParams
        );

        var buildResult = _sqlQueryBuilder.BuildSqlQuery<TResponse>(qbParams);

        if (buildResult.Builder is null)
            return 0;

        return await buildResult.Builder.ExecuteScalarAsync<int>(commandTimeout: commandTimeout);
    }

    /// <summary>
    /// Gets aggregate values for properties decorated with any attribute named AggregateAttribute
    /// that exposes an AggregateType property (convention-based, namespace-agnostic).
    /// </summary>
    public async Task<IDictionary<string, decimal>> GetAggregatesAsync<T>(string viewName, FilteredDataQuery query, int? commandTimeout = null)
        where T : class
    {
        var columns = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            .SelectMany(p => p.GetCustomAttributes()
                .Where(a => a.GetType().Name == nameof(AggregateAttribute))
                .Select(attr =>
                {
                    var aggTypeStr = attr.GetType().GetProperty("AggregateType")?.GetValue(attr)?.ToString() ?? "Sum";
                    var func = aggTypeStr switch
                    {
                        "Sum"   => "SUM",
                        "Count" => "COUNT",
                        "Min"   => "MIN",
                        "Max"   => "MAX",
                        "Avg"   => "AVG",
                        _       => "SUM"
                    };
                    return (Property: p, Func: func);
                }))
            .ToList();

        if (columns.Count == 0)
            return new Dictionary<string, decimal>();

        var selectParts = columns.Select(x =>
        {
            var camelName = JsonNamingPolicy.CamelCase.ConvertName(x.Property.Name);
            var alias = $"{camelName}{x.Func[0]}{x.Func[1..].ToLower()}";
            return $"COALESCE({x.Func}({x.Property.Name}), 0) AS \"{alias}\"";
        });

        var selectClause = string.Join(", ", selectParts);
        var timeout = commandTimeout ?? _defaultCommandTimeout;

        using var connection = _connectionFactory.Create();

        var inputParams = QueryInputParameters.Create(
            sql: viewName,
            queryString: query.QueryString,
            defaultQueryString: query.DefaultQuery,
            ignoreLimit: true,
            isJson: false,
            ignorePaging: true
        );

        var qbParams = QueryBuilderParameters.Create(
            connection: connection,
            baseQuery: GetBaseAggregateQuery(viewName, selectClause),
            inputParameters: inputParams
        );

        var buildResult = _sqlQueryBuilder.BuildSqlQuery<T>(qbParams);

        if (buildResult.Builder is null)
            return new Dictionary<string, decimal>();

        var rows = await buildResult.Builder.QueryAsync<dynamic>(commandTimeout: timeout);
        var row = rows.FirstOrDefault();
        if (row is null) return new Dictionary<string, decimal>();

        return ((IDictionary<string, object>)row)
            .ToDictionary(kvp => kvp.Key, kvp => Convert.ToDecimal(kvp.Value ?? 0));
    }

    private static FormattableString GetBaseQuery(string viewName)
        => $"/**select**/ FROM {viewName:raw} WHERE 1=1 /**filters**/";

    private static FormattableString GetBaseCountQuery(string viewName)
        => $"SELECT COUNT(*) FROM {viewName:raw} WHERE 1=1 /**filters**/";

    private static FormattableString GetBaseAggregateQuery(string viewName, string selectClause)
        => $"SELECT {selectClause:raw} FROM {viewName:raw} WHERE 1=1 /**filters**/";
}
