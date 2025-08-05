using System.Data;

namespace _1Dev.Pagin8.Input;

public class QueryBuilderParameters
{
    public static QueryBuilderParameters Create(IDbConnection connection, FormattableString baseQuery, QueryInputParameters inputParameters)
    {
        return new QueryBuilderParameters
        {
            Connection = connection,
            BaseQuery = baseQuery,
            InputParameters = inputParameters
        };
    }

    public IDbConnection Connection { get; private init; }

    public FormattableString BaseQuery { get; private init; }

    public QueryInputParameters InputParameters { get; init; }
}