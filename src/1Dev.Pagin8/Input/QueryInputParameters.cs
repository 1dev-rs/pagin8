namespace _1Dev.Pagin8.Input;

public class QueryInputParameters
{
    public static QueryInputParameters Create(string sql, string queryString, string defaultQueryString, bool ignoreLimit, bool isJson = false, bool isCount = false, bool isDefault = false, string ctePrefix = "")
    {
        return new QueryInputParameters
        {
            Sql = sql,
            QueryString = queryString,
            DefaultQueryString = defaultQueryString,
            IgnoreLimit = ignoreLimit,
            IsJson = isJson,
            IsCount = isCount,
            IsDefault = isDefault,
            CtePrefix = ctePrefix
        };
    }

    public static QueryInputParameters CreateWithQueryString(string queryString)
    {
        return new QueryInputParameters
        {
            QueryString = queryString
        };
    }

    public string QueryString { get; init; }

    public string DefaultQueryString { get; init; }

    public string Sql { get; init; }

    public bool IgnoreLimit { get; init; }

    public bool IsJson { get; init; }

    public bool IsCount { get; set; }

    public bool IsDefault { get; set; }

    public string CtePrefix { get; init; }

    public void SetCount()
    {
        IsCount = true;
    }

    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
    }
}