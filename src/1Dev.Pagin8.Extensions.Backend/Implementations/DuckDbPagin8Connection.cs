using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using DuckDB.NET.Data;

namespace _1Dev.Pagin8.Extensions.Backend.Implementations;

/// <summary>
/// A DuckDB connection that runs the SQL pagin8 builds. The query text is PostgreSQL, which DuckDB
/// accepts except for the parameter marker: @p0 is rewritten to DuckDB's $p0 before the command runs.
/// </summary>
public sealed class DuckDbPagin8Connection(DuckDBConnection inner) : DbConnection
{
    /// <summary>
    /// Every command starts an activity here, tagged with its SQL, so an application can time DuckDB
    /// queries the way it times Npgsql's.
    /// </summary>
    public const string ActivitySourceName = "1Dev.Pagin8.DuckDb";

    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>The wrapped DuckDB connection.</summary>
    public DuckDBConnection Inner { get; } = inner ?? throw new ArgumentNullException(nameof(inner));

    [AllowNull]
    public override string ConnectionString
    {
        get => Inner.ConnectionString;
        set => Inner.ConnectionString = value;
    }

    public override string Database => Inner.Database;

    public override string DataSource => Inner.DataSource;

    public override string ServerVersion => Inner.ServerVersion;

    public override ConnectionState State => Inner.State;

    public override void Open()
    {
        if (Inner.State != ConnectionState.Open) Inner.Open();
    }

    public override void Close() => Inner.Close();

    public override void ChangeDatabase(string databaseName) => Inner.ChangeDatabase(databaseName);

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => Inner.BeginTransaction(isolationLevel);

    protected override DbCommand CreateDbCommand() => new DuckDbPagin8Command(this, Inner.CreateCommand());

    protected override void Dispose(bool disposing)
    {
        if (disposing) Inner.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>
/// The command behind <see cref="DuckDbPagin8Connection"/>: rewrites parameter markers, drops
/// parameters the text does not use, and enforces <see cref="DbCommand.CommandTimeout"/>.
/// </summary>
public sealed class DuckDbPagin8Command : DbCommand
{
    private readonly DuckDBCommand _inner;
    private DuckDbPagin8Connection _connection;

    internal DuckDbPagin8Command(DuckDbPagin8Connection connection, DuckDBCommand inner)
    {
        _connection = connection;
        _inner = inner;
    }

    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection
    {
        get => _connection;
        set => _connection = (DuckDbPagin8Connection)value!;
    }

    protected override DbParameterCollection DbParameterCollection => _inner.Parameters;

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel() => _inner.Cancel();

    public override void Prepare() { }

    protected override DbParameter CreateDbParameter() => new DuckDBParameter();

    public override int ExecuteNonQuery() => Run(() => _inner.ExecuteNonQuery());

    public override object? ExecuteScalar() => Run(() => _inner.ExecuteScalar());

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => Run(() => _inner.ExecuteReader(behavior));

    private T Run<T>(Func<T> execute)
    {
        _inner.CommandText = TranslateParameters(CommandText, out var referenced);
        NormalizeParameters(referenced);

        using var activity = DuckDbPagin8Connection.ActivitySource.StartActivity("duckdb.query", ActivityKind.Client);
        activity?.SetTag("db.system", "duckdb");
        activity?.SetTag("db.query.text", CommandText);

        // DuckDB has no statement timeout: interrupt the query when it runs out.
        var timedOut = 0;
        using var timer = CommandTimeout > 0
            ? new Timer(_ => { Interlocked.Exchange(ref timedOut, 1); _inner.Cancel(); }, null, TimeSpan.FromSeconds(CommandTimeout), Timeout.InfiniteTimeSpan)
            : null;

        try
        {
            return execute();
        }
        catch (Exception ex) when (Volatile.Read(ref timedOut) == 1)
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw new TimeoutException($"The DuckDB query did not finish within {CommandTimeout} s.", ex);
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error);
            throw;
        }
    }

    // Dapper names parameters p0 and DuckDB binds $p0 by that name. DuckDB refuses a statement whose
    // parameter count differs from its markers, so parameters the text does not mention are dropped.
    private void NormalizeParameters(HashSet<string> referenced)
    {
        for (var i = _inner.Parameters.Count - 1; i >= 0; i--)
        {
            var parameter = _inner.Parameters[i];
            var name = parameter.ParameterName?.TrimStart('@', '$', ':') ?? string.Empty;
            parameter.ParameterName = name;
            if (name.Length > 0 && !referenced.Contains(name))
                _inner.Parameters.RemoveAt(i);
        }
    }

    /// <summary>
    /// Rewrites @name to $name outside string literals, quoted identifiers and comments, and returns
    /// the names the text references.
    /// </summary>
    public static string TranslateParameters(string sql, out HashSet<string> referenced)
    {
        referenced = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(sql)) return sql ?? string.Empty;

        var result = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];

            if (c is '\'' or '"')
            {
                var end = sql.IndexOf(c, i + 1);
                while (end >= 0 && end + 1 < sql.Length && sql[end + 1] == c)
                    end = sql.IndexOf(c, end + 2);
                end = end < 0 ? sql.Length - 1 : end;
                result.Append(sql, i, end - i + 1);
                i = end + 1;
                continue;
            }

            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                var end = sql.IndexOf('\n', i);
                end = end < 0 ? sql.Length : end;
                result.Append(sql, i, end - i);
                i = end;
                continue;
            }

            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                var end = sql.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? sql.Length : end + 2;
                result.Append(sql, i, end - i);
                i = end;
                continue;
            }

            if (c is '@' or '$' && i + 1 < sql.Length && IsNameStart(sql[i + 1]) && (i == 0 || !IsNamePart(sql[i - 1])))
            {
                var start = i + 1;
                var end = start;
                while (end < sql.Length && IsNamePart(sql[end])) end++;
                referenced.Add(sql[start..end]);
                result.Append('$').Append(sql, start, end - start);
                i = end;
                continue;
            }

            result.Append(c);
            i++;
        }

        return result.ToString();
    }

    private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsNamePart(char c) => char.IsLetterOrDigit(c) || c == '_';

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}
