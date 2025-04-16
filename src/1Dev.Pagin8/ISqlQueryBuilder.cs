using System.Data;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;

namespace _1Dev.Pagin8;
public interface ISqlQueryBuilder
{
    /// <summary>
    /// Builds an SQL query using the provided parameters, including the database connection, base query, and input parameters.
    /// </summary>
    /// <param name="parameters">An instance of <see cref="QueryBuilderParameters{T}"/> containing the necessary parameters for query construction.</param>
    /// <returns>
    /// A <see cref="QueryBuilderResult"/> object representing the result of the query building process.
    /// The <see cref="QueryBuilderResult.Builder"/> property contains the query builder instance.
    /// </returns>
    /// <typeparam name="T">The type of the entity class.</typeparam>
    /// <remarks>
    /// The <paramref name="parameters"/> object includes the following properties:
    /// <list type="bullet">
    /// <item>
    /// <term>Connection</term>
    /// <description>The <see cref="IDbConnection"/> to use for executing the query.</description>
    /// </item>
    /// <item>
    /// <term>BaseQuery</term>
    /// <description>The base query to build upon, as a <see cref="FormattableString"/>.</description>
    /// </item>
    /// <item>
    /// <term>InputParameters</term>
    /// <description>An instance of <see cref="QueryInputParameters"/> containing additional query parameters.</description>
    /// </item>
    /// </list>
    /// The <see cref="QueryInputParameters"/> class includes:
    /// <list type="bullet">
    /// <item>
    /// <term>QueryString</term>
    /// <description>The input string containing query tokens.</description>
    /// </item>
    /// <item>
    /// <term>Sql</term>
    /// <description>The SQL query string.</description>
    /// </item>
    /// <item>
    /// <term>IgnoreLimit</term>
    /// <description>Specifies whether the query should ignore the limit clause.</description>
    /// </item>
    /// <item>
    /// <term>IsJson</term>
    /// <description>Specifies whether the result should be wrapped in JSON format.</description>
    /// </item>
    /// <item>
    /// <term>IsCount</term>
    /// <description>Specifies whether the query is for counting records.</description>
    /// </item>
    /// <item>
    /// <term>CtePrefix</term>
    /// <description>The common table expression (CTE) prefix used in the query.</description>
    /// </item>
    /// </list>
    /// </remarks>
    public QueryBuilderResult BuildSqlQuery<T>(QueryBuilderParameters parameters) where T : class;

}