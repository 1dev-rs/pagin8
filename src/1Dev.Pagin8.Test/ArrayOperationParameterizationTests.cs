using System.ComponentModel.DataAnnotations;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;

namespace _1Dev.Pagin8.Test;

public class TestArrayEntity
{
    [Key]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<int> Ids { get; set; } = [];
    public List<DateOnly> Holidays { get; set; } = [];
    public List<TestArrayItem> Items { get; set; } = [];
}

public class TestArrayItem
{
    [Key]
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

[Collection("PostgreSQL QueryBuilder Tests")]
public class ArrayOperationParameterizationPostgreSqlTests
{
    private readonly ISqlQueryBuilder _sut;

    public ArrayOperationParameterizationPostgreSqlTests()
    {
        PostgreSqlTestBootstrap.Init();
        _sut = DateOnlyTestHelper.CreateBuilder((m, d) => new NpgsqlTokenVisitor(m, d));
    }

    [Theory]
    [InlineData("tags=incl(O'Brien)", "tags::text[] @> ARRAY[@p0]::text[]")]
    [InlineData("tags=excl(O'Brien)", "NOT (tags::text[] && ARRAY[@p0]::text[])")]
    [InlineData("name=incl(O'Brien)", "name = ANY(ARRAY[@p0]::text[])")]
    [InlineData("name=excl(O'Brien)", "name != ALL(ARRAY[@p0]::text[])")]
    public void StringValues_AreParameterized(string query, string expectedSql)
    {
        var result = _sut.BuildSqlQuery<TestArrayEntity>(DateOnlyTestHelper.Build(query));

        var sql = result.Builder.AsSql().Sql;
        sql.Should().Contain(expectedSql);
        sql.Should().NotContain("O'Brien");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be("O'Brien");
    }

    [Fact]
    public void NumericValues_AreParameterizedAndTyped()
    {
        var result = _sut.BuildSqlQuery<TestArrayEntity>(DateOnlyTestHelper.Build("ids=incl(1,2)"));

        result.Builder.AsSql().Sql.Should().Contain("ids::int[] @> ARRAY[@p0, @p1]::int[]");
        var p = result.Builder.Build().SqlParameters;
        p[0].Argument.Should().Be(1);
        p[1].Argument.Should().Be(2);
    }

    [Theory]
    [InlineData("ids=incl(1;DROP)")]
    [InlineData("holidays=incl(not-a-date)")]
    public void InvalidTypedValues_AreRejected(string query)
    {
        var act = () => _sut.BuildSqlQuery<TestArrayEntity>(DateOnlyTestHelper.Build(query));

        act.Should().Throw<Pagin8Exception>();
    }

    [Fact]
    public void DateOnlyArray_UsesDateArray()
    {
        var result = _sut.BuildSqlQuery<TestArrayEntity>(DateOnlyTestHelper.Build("holidays=incl(2024-12-25)"));

        result.Builder.AsSql().Sql.Should().Contain("holidays::date[] @> ARRAY[@p0]::date[]");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be(new DateTime(2024, 12, 25));
    }

    [Fact]
    public void ComplexArray_JsonLiteralIsEncoded()
    {
        var result = _sut.BuildSqlQuery<TestArrayEntity>(DateOnlyTestHelper.Build("items.with=(label.incl(a\"b))"));

        result.Builder.AsSql().Sql.Should().Contain("items @> @p0::jsonb");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be("[{\"label\": \"a\\u0022b\"}]");
    }
}

[Collection("SQL Server QueryBuilder Tests")]
public class ArrayOperationParameterizationSqlServerTests
{
    private readonly ISqlQueryBuilder _sut;

    public ArrayOperationParameterizationSqlServerTests()
    {
        SqlServerTestBootstrap.Init();
        _sut = DateOnlyTestHelper.CreateBuilder((m, d) => new SqlServerTokenVisitor(m, d));
    }

    [Theory]
    [InlineData("tags=incl(O'Brien)", "(VALUES (@p0))")]
    [InlineData("tags=excl(O'Brien)", "NOT EXISTS(SELECT 1 FROM (VALUES (@p0))")]
    [InlineData("name=incl(O'Brien)", "name IN (@p0)")]
    [InlineData("name=excl(O'Brien)", "name NOT IN (@p0)")]
    public void StringValues_AreParameterized(string query, string expectedSql)
    {
        var result = _sut.BuildSqlQuery<TestArrayEntity>(DateOnlyTestHelper.Build(query));

        var sql = result.Builder.AsSql().Sql;
        sql.Should().Contain(expectedSql);
        sql.Should().NotContain("O'Brien");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be("O'Brien");
    }

    [Theory]
    [InlineData("ids=incl(1;DROP)")]
    [InlineData("holidays=incl(not-a-date)")]
    public void InvalidTypedValues_AreRejected(string query)
    {
        var act = () => _sut.BuildSqlQuery<TestArrayEntity>(DateOnlyTestHelper.Build(query));

        act.Should().Throw<Pagin8Exception>();
    }

    [Fact]
    public void DateOnlyArray_ComparesIsoText()
    {
        var result = _sut.BuildSqlQuery<TestArrayEntity>(DateOnlyTestHelper.Build("holidays=incl(2024-12-25)"));

        result.Builder.AsSql().Sql.Should().NotContain("2024-12-25");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be("2024-12-25");
    }

    [Fact]
    public void ComplexArray_ValuesAreParameterized()
    {
        var result = _sut.BuildSqlQuery<TestArrayEntity>(DateOnlyTestHelper.Build("items.with=(label.incl(O'Brien))"));

        var sql = result.Builder.AsSql().Sql;
        sql.Should().Contain("JSON_VALUE(x.value, '$.label')");
        sql.Should().NotContain("O'Brien");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be("O'Brien");
    }
}
