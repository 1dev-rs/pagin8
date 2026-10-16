using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Attributes;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests;

public class CursorTransliterationTests
{
    public static IEnumerable<object[]> StringCursorCases() =>
        from database in new[] { DatabaseType.PostgreSql, DatabaseType.SqlServer }
        from field in new[] { "name", "label", "original", "searchable" }
        from direction in new[] { "asc", "desc" }
        from cursor in new (string Original, string Latin)[] { ("Ана", "Ana"), ("Љиљана Petrović", "LJiljana Petrović"), ("Ana", "Ana"), ("", "") }
        select new object[] { database, field, direction, cursor.Original, field is "original" or "searchable" ? cursor.Original : cursor.Latin };

    [Theory]
    [MemberData(nameof(StringCursorCases))]
    public void StringCursors_ShouldFollowEachPropertyPolicy(DatabaseType database, string field, string direction, string cursor, string expected)
    {
        var cursorToken = cursor.Length == 0 ? "$empty" : Uri.EscapeDataString(cursor);
        var result = BuildSqlQuery(database, $"paging=(sort({field}.{direction}.{cursorToken},$key.42),limit.10)");

        AssertCursorComparisons(result, field, direction, expected);
        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain($"ORDER BY {field} {direction.ToUpperInvariant()},id ASC");
        sql.Should().NotContain("_translit");
    }

    public static IEnumerable<object[]> CompositeCursorCases() =>
        from database in new[] { DatabaseType.PostgreSql, DatabaseType.SqlServer }
        from originalFirst in new[] { false, true }
        from firstDirection in new[] { "asc", "desc" }
        from secondDirection in new[] { "asc", "desc" }
        select new object[] { database, originalFirst, firstDirection, secondDirection };

    [Theory]
    [MemberData(nameof(CompositeCursorCases))]
    public void CompositeCursors_ShouldMixLegacyAndOriginalValues(DatabaseType database, bool originalFirst, string firstDirection, string secondDirection)
    {
        var first = originalFirst ? "searchable" : "label";
        var second = originalFirst ? "label" : "searchable";
        var cursor = Uri.EscapeDataString("Љиљана Petrović");
        var result = BuildSqlQuery(database, $"paging=(sort({first}.{firstDirection}.{cursor},{second}.{secondDirection}.{cursor},$key.42),limit.10)");

        AssertCursorComparisons(result, "label", originalFirst ? secondDirection : firstDirection, "LJiljana Petrović");
        AssertCursorComparisons(result, "searchable", originalFirst ? firstDirection : secondDirection, "Љиљана Petrović");
        var sql = result.Builder!.AsSql().Sql;
        sql.Should().Contain($"ORDER BY {first} {firstDirection.ToUpperInvariant()},{second} {secondDirection.ToUpperInvariant()},id ASC");
        sql.Should().NotContain("_translit");
        Regex.Matches(sql, @"\b(?:label|searchable|id) (?:>|<|=) @p\d+").Should().HaveCount(6);
    }

    public static IEnumerable<object[]> SearchCases() =>
        from database in new[] { DatabaseType.PostgreSql, DatabaseType.SqlServer }
        from field in new[] { "name", "label", "original", "searchable" }
        from input in new[] { "Љиљана", "Ljiljana" }
        select new object[] { database, field, input };

    [Theory]
    [MemberData(nameof(SearchCases))]
    public void Search_ShouldRemainIndependentOfCursorPolicy(DatabaseType database, string field, string input)
    {
        var result = BuildSqlQuery(database, $"{field}=eq.{Uri.EscapeDataString(input)}");
        var sql = result.Builder!.AsSql().Sql;
        var searchColumn = field is "label" or "searchable" ? $"{field}_translit" : field;

        sql.Should().Contain(database == DatabaseType.PostgreSql ? $"{searchColumn} ILIKE @p0" : $"{searchColumn} LIKE @p0");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be("ljiljana");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Metadata_ShouldFindInheritedOptInRegardlessOfSearchMode(bool useTranslit)
    {
        var metadata = new Pagin8MetadataProvider(new MetadataProvider());
        var column = metadata.GetColumnInfo<CursorTestEntity>("sEaRcHaBlE", useTranslit);

        column.SkipCursorTransliteration.Should().BeTrue();
        column.IsTranslit.Should().Be(useTranslit);
        column.Name.Should().Be(useTranslit ? "Searchable_translit" : "Searchable");
        metadata.GetColumnInfo<CursorTestEntity>("label", useTranslit).SkipCursorTransliteration.Should().BeFalse();
    }

    private static QueryBuilderResult BuildSqlQuery(DatabaseType database, string query)
    {
        if (database == DatabaseType.PostgreSql)
        {
            PostgreSqlTestBootstrap.Init();
        }
        else
        {
            SqlServerTestBootstrap.Init();
        }

        var metadata = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor = new DateProcessor();
        var tokenizer = new TokenizationService(new Tokenizer(), new PassThroughContextValidator(), metadata);
        ISqlTokenVisitor visitor = database == DatabaseType.PostgreSql
            ? new NpgsqlTokenVisitor(metadata, dateProcessor)
            : new SqlServerTokenVisitor(metadata, dateProcessor);
        var builder = new SqlQueryBuilder(tokenizer, visitor);

        return builder.BuildSqlQuery<CursorTestEntity>(new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create("SELECT * FROM cursor_test_entity", query, string.Empty, ignoreLimit: false)
        });
    }

    private static void AssertCursorComparisons(QueryBuilderResult result, string field, string direction, string expected)
    {
        var sql = result.Builder!.AsSql().Sql;
        var parameters = result.Builder.Build().SqlParameters;
        var comparisons = Regex.Matches(sql, $@"\b{field} (?<op>>|<|=) @p(?<index>\d+)");

        comparisons.Select(match => match.Groups["op"].Value).Should().Contain(direction == "asc" ? ">" : "<").And.Contain("=");
        foreach (Match comparison in comparisons)
        {
            var index = int.Parse(comparison.Groups["index"].Value);
            parameters[index].Argument.Should().Be(expected, $"the cursor policy for {field} applies to every comparison, including equality tie-breakers");
        }
    }

    private class OriginalCursorFields
    {
        [SkipCursorTransliteration]
        public string Original { get; set; } = string.Empty;

        [SkipCursorTransliteration]
        [UseTranslit]
        public string Searchable { get; set; } = string.Empty;
    }

    private class CursorTestEntity : OriginalCursorFields
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;

        [UseTranslit]
        public string Label { get; set; } = string.Empty;
    }
}
