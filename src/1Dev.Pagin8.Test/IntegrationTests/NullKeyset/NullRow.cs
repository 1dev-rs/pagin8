using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using _1Dev.Pagin8.Internal.Attributes;

namespace _1Dev.Pagin8.Test.IntegrationTests.NullKeyset;

/// <summary>
/// A row whose sortable columns are built to break keyset paging on NULL: every nullable type
/// pagin8 sorts, NULLs at the start, in the middle and at the end of the order, runs of NULLs
/// longer than a page, values next to the stand-ins the old COALESCE used ('' for text, -1 for
/// numbers, 1753-01-01 for SQL Server dates), duplicates, a column that is NULL everywhere and
/// one that is never NULL. Text is lower-case ASCII so every engine and collation orders it alike.
/// </summary>
public class NullRow
{
    [Key]
    public int Id { get; set; }

    [NullsAllowed]
    public string? Label { get; set; }

    [NullsAllowed]
    public string? Note { get; set; }

    public int? Quantity { get; set; }

    public decimal? Amount { get; set; }

    public DateTime? Shipped { get; set; }

    public bool? Flag { get; set; }

    public string Code { get; set; } = "";
}

public static class NullRowData
{
    public const string Table = "null_rows";

    public static readonly string[] SortableFields = ["label", "note", "quantity", "amount", "shipped", "flag", "code"];

    public static IReadOnlyList<NullRow> Rows { get; } = Build();

    private static List<NullRow> Build()
    {
        string?[] labels = ["a", "aa", "b", "c", "a"];
        int?[] quantities = [-5, -1, 0, 1, 1, 7];
        decimal?[] amounts = [-1.00m, -2.50m, 0m, 3.25m, 3.25m];
        DateTime[] days = [new(1753, 1, 1), new(2024, 1, 1), new(2024, 1, 2), new(2024, 1, 2), new(2024, 3, 9)];

        var rows = new List<NullRow>();
        for (var id = 1; id <= 60; id++)
        {
            var leadingOrTrailing = id <= 3 || id >= 58;
            rows.Add(new NullRow
            {
                Id = id,
                Label = leadingOrTrailing || id % 7 == 0 ? null : id % 7 == 3 ? "" : labels[id % labels.Length],
                Note = null,
                Quantity = leadingOrTrailing || id % 6 == 0 || id is >= 20 and <= 27 ? null : quantities[id % quantities.Length],
                Amount = id % 5 == 0 || id is 1 or 60 ? null : amounts[id % amounts.Length],
                Shipped = id % 4 == 0 || id == 2 ? null : days[id % days.Length],
                Flag = id % 3 == 0 ? null : id % 3 == 1,
                Code = "c" + (id % 10),
            });
        }

        return rows;
    }

    public static string PostgreSqlCreateTable => $"""
        CREATE TABLE {Table} (
            id INTEGER PRIMARY KEY,
            label TEXT,
            note TEXT,
            quantity INTEGER,
            amount NUMERIC(18,2),
            shipped TIMESTAMP,
            flag BOOLEAN,
            code TEXT NOT NULL)
        """;

    public static string SqlServerCreateTable => $"""
        CREATE TABLE {Table} (
            id INT PRIMARY KEY,
            label NVARCHAR(20) NULL,
            note NVARCHAR(20) NULL,
            quantity INT NULL,
            amount DECIMAL(18,2) NULL,
            shipped DATETIME2 NULL,
            flag BIT NULL,
            code NVARCHAR(20) NOT NULL)
        """;

    public static string DuckDbCreateTable => $"""
        CREATE TABLE {Table} (
            id INTEGER PRIMARY KEY,
            label VARCHAR,
            note VARCHAR,
            quantity INTEGER,
            amount DECIMAL(18,2),
            shipped TIMESTAMP,
            flag BOOLEAN,
            code VARCHAR NOT NULL)
        """;

    public static string Insert =>
        $"INSERT INTO {Table} (id, label, note, quantity, amount, shipped, flag, code) VALUES (@Id, @Label, @Note, @Quantity, @Amount, @Shipped, @Flag, @Code)";

    public static object? Value(NullRow row, string field) =>
        typeof(NullRow).GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)!.GetValue(row);

    /// <summary>The order pagin8 promises: NULL is the smallest value of a nullable column.</summary>
    public static List<int> ExpectedOrder(IEnumerable<NullRow> rows, params (string Field, bool Descending)[] sort) =>
        rows.OrderBy(r => r, Comparer<NullRow>.Create((x, y) =>
            {
                foreach (var (field, descending) in sort)
                {
                    var compared = CompareValues(Value(x, field), Value(y, field));
                    if (compared != 0) return descending ? -compared : compared;
                }

                return x.Id.CompareTo(y.Id);
            }))
            .Select(r => r.Id)
            .ToList();

    private static int CompareValues(object? x, object? y) => (x, y) switch
    {
        (null, null) => 0,
        (null, _) => -1,
        (_, null) => 1,
        (string a, string b) => string.CompareOrdinal(a, b),
        _ => Comparer<object>.Default.Compare(x, y),
    };

    /// <summary>A cursor value the way a client writes it: $null for NULL, $empty for an empty string.</summary>
    public static string Cursor(object? value) => value switch
    {
        null => "$null",
        "" => "$empty",
        DateTime d => d.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()!,
    };
}
