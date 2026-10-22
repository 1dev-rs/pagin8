using System.Data;
using System.Globalization;
using Dapper;

namespace _1Dev.Pagin8.Extensions.Backend.Implementations;

/// <summary>
/// Dapper has no built-in DateOnly mapping, and Npgsql/SqlClient return <c>date</c> columns as DateTime,
/// so materializing an entity with a DateOnly/DateOnly? property fails without this handler.
/// </summary>
internal sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    private static readonly object Sync = new();

    /// <summary>
    /// Registers the handler unless the application already registered its own DateOnly handler.
    /// </summary>
    public static void EnsureRegistered()
    {
        lock (Sync)
        {
            if (SqlMapper.HasTypeHandler(typeof(DateOnly))) return;
            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
        }
    }

    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value)
    {
        return value switch
        {
            DateOnly dateOnly => dateOnly,
            DateTime dateTime => DateOnly.FromDateTime(dateTime),
            DateTimeOffset dateTimeOffset => DateOnly.FromDateTime(dateTimeOffset.DateTime),
            string text => DateOnly.FromDateTime(DateTime.Parse(text, CultureInfo.InvariantCulture)),
            _ => throw new DataException($"Cannot convert value of type {value.GetType().Name} to DateOnly.")
        };
    }
}
