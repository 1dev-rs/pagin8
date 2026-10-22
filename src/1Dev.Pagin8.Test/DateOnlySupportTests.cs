using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Runtime.CompilerServices;
using _1Dev.Pagin8.Extensions.Backend.Implementations;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;

namespace _1Dev.Pagin8.Test;

public class TestDateOnlyEntity
{
    [Key]
    public int Id { get; set; }
    public DateOnly BirthDate { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public TestDateOnlyChild? Child { get; set; }
    public List<TestDateOnlyChild> Children { get; set; } = [];
    public List<DateOnly> Holidays { get; set; } = [];
}

public class TestDateOnlyChild
{
    [Key]
    public int Id { get; set; }
    public DateOnly IssuedOn { get; set; }
}

internal static class DateOnlyTestHelper
{
    public static QueryBuilderParameters Build(string queryString) => new()
    {
        InputParameters = QueryInputParameters.Create(
            sql: "SELECT * FROM vw_test",
            queryString: queryString,
            defaultQueryString: "",
            ignoreLimit: true)
    };

    public static ISqlQueryBuilder CreateBuilder(Func<Pagin8MetadataProvider, DateProcessor, ISqlTokenVisitor> visitorFactory)
    {
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var tokenizationSvc = new TokenizationService(new Tokenizer(), new PassThroughContextValidator(), metadataProvider);
        return new SqlQueryBuilder(tokenizationSvc, visitorFactory(metadataProvider, new DateProcessor()));
    }
}

[Collection("PostgreSQL QueryBuilder Tests")]
public class DateOnlyPostgreSqlTests
{
    private readonly ISqlQueryBuilder _sut;

    public DateOnlyPostgreSqlTests()
    {
        PostgreSqlTestBootstrap.Init();
        _sut = DateOnlyTestHelper.CreateBuilder((m, d) => new NpgsqlTokenVisitor(m, d));
    }

    [Theory]
    [InlineData("eq", "=")]
    [InlineData("gt", ">")]
    [InlineData("gte", ">=")]
    [InlineData("lt", "<")]
    [InlineData("lte", "<=")]
    public void Comparison_OnDateOnly_ComparesDates(string op, string sqlOp)
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build($"birthDate={op}.2024-01-15"));

        result.Builder.AsSql().Sql.Should().Contain($"DATE(birthDate) {sqlOp} DATE(@p0)");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be(new DateTime(2024, 1, 15));
    }

    [Fact]
    public void Comparison_OnNullableDateOnly_ComparesDates()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("validUntil=gt.2024-01-15"));

        result.Builder.AsSql().Sql.Should().Contain("DATE(validUntil) > DATE(@p0)");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be(new DateTime(2024, 1, 15));
    }

    [Fact]
    public void NegatedComparison_OnDateOnly_IncludesNulls()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("validUntil=not.eq.2024-01-15"));

        result.Builder.AsSql().Sql.Should().Contain("OR validUntil IS NULL");
    }

    [Fact]
    public void In_OnDateOnly_GeneratesInClause()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("birthDate=in.(2024-01-15,2024-02-01)"));

        result.Builder.AsSql().Sql.Should()
            .Contain("birthDate IN ('2024-01-15 00:00:00.0000000','2024-02-01 00:00:00.0000000')");
    }

    [Fact]
    public void IsEmpty_OnNullableDateOnly_ChecksNullOnly()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("validUntil=is.$empty"));

        var sql = result.Builder.AsSql().Sql;
        sql.Should().Contain("(validUntil IS  NULL)");
        sql.Should().NotContain("''");
    }

    [Theory]
    [InlineData("birthDate=ago.3d")]
    [InlineData("birthDate=for.3d")]
    [InlineData("birthDate=ago.2w")]
    [InlineData("birthDate=for.1m")]
    [InlineData("birthDate=ago.1y")]
    [InlineData("birthDate=ago.3de")]
    [InlineData("birthDate=for.3ds")]
    [InlineData("validUntil=not.ago.3d")]
    public void DateRange_OnDateOnly_UsesWholeDayBounds(string query)
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build(query));

        // Identical start/end (exact ranges) may be collapsed into one parameter when parameter reuse is on
        result.Builder.AsSql().Sql.Should().Contain("BETWEEN @p");
        var dates = result.Builder.Build().SqlParameters.Select(x => x.Argument).OfType<DateTime>().ToList();
        dates.Should().NotBeEmpty();
        dates.Should().OnlyContain(d => d.TimeOfDay == TimeSpan.Zero);
        dates.First().Should().BeOnOrBefore(dates.Last());
    }

    [Fact]
    public void DateRange_For_OnDateOnly_IncludesToday()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("birthDate=for.3d"));

        var p = result.Builder.Build().SqlParameters;
        ((DateTime)p[0].Argument!).Should().Be(DateTime.Today);
        ((DateTime)p[1].Argument!).Should().Be(DateTime.Today.AddDays(3));
    }

    [Fact]
    public void Sort_OnDateOnly_OrdersByColumn()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("paging=(sort(birthDate.desc))"));

        result.Builder.AsSql().Sql.Should().Contain("ORDER BY birthDate DESC");
    }

    [Fact]
    public void Sort_OnNullableDateOnly_CoalescesWithMinDate()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("paging=(sort(validUntil.asc))"));

        result.Builder.AsSql().Sql.Should().Contain("COALESCE(validUntil, @p0) ASC NULLS FIRST");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void KeysetPaging_OnDateOnly_UsesCursorDate()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(
            DateOnlyTestHelper.Build("paging=(sort(birthDate.asc.2024-01-15,$key.5),limit.10)"));

        result.Builder.AsSql().Sql.Should().Contain("birthDate > @p0");
        result.Builder.Build().SqlParameters
            .Should().Contain(sp => sp.Argument != null && sp.Argument.Equals(new DateTime(2024, 1, 15)));
    }

    [Fact]
    public void KeysetPaging_OnNullableDateOnly_UsesCoalesce()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(
            DateOnlyTestHelper.Build("paging=(sort(validUntil.asc.2024-01-15,$key.5),limit.10)"));

        var sql = result.Builder.AsSql().Sql;
        sql.Should().Contain("validUntil IS NULL AND");
        sql.Should().Contain("validUntil IS NOT NULL AND validUntil >");
    }

    [Fact]
    public void NestedFilter_OnDateOnlyJsonField_ComparesDates()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("child.with=(issuedOn.gte.2024-01-15)"));

        result.Builder.AsSql().Sql.Should().Contain("DATE((child ->> 'issuedOn')::text) >= DATE(@p0)");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be(new DateTime(2024, 1, 15));
    }

    [Fact]
    public void InvalidDate_OnDateOnly_Throws()
    {
        var act = () => _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("birthDate=eq.not-a-date"));

        act.Should().Throw<ArgumentException>();
    }
}

[Collection("SQL Server QueryBuilder Tests")]
public class DateOnlySqlServerTests
{
    private readonly ISqlQueryBuilder _sut;

    public DateOnlySqlServerTests()
    {
        SqlServerTestBootstrap.Init();
        _sut = DateOnlyTestHelper.CreateBuilder((m, d) => new SqlServerTokenVisitor(m, d));
    }

    [Theory]
    [InlineData("eq", "=")]
    [InlineData("gt", ">")]
    [InlineData("lte", "<=")]
    public void Comparison_OnDateOnly_ComparesDates(string op, string sqlOp)
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build($"birthDate={op}.2024-01-15"));

        result.Builder.AsSql().Sql.Should().Contain($"CAST(birthDate AS DATE) {sqlOp} CAST(@p0 AS DATE)");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be(new DateTime(2024, 1, 15));
    }

    [Fact]
    public void In_OnDateOnly_GeneratesInClause()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("birthDate=in.(2024-01-15,2024-02-01)"));

        result.Builder.AsSql().Sql.Should()
            .Contain("birthDate IN ('2024-01-15 00:00:00.0000000','2024-02-01 00:00:00.0000000')");
    }

    [Fact]
    public void IsEmpty_OnNullableDateOnly_ChecksNullOnly()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("validUntil=is.$empty"));

        var sql = result.Builder.AsSql().Sql;
        sql.Should().Contain("validUntil IS");
        sql.Should().Contain("NULL");
        sql.Should().NotContain("''");
    }

    [Theory]
    [InlineData("birthDate=ago.3d")]
    [InlineData("birthDate=for.3d")]
    [InlineData("birthDate=for.1m")]
    [InlineData("validUntil=not.ago.3d")]
    public void DateRange_OnDateOnly_UsesWholeDayBounds(string query)
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build(query));

        result.Builder.AsSql().Sql.Should().Contain("BETWEEN @p");
        var dates = result.Builder.Build().SqlParameters.Select(x => x.Argument).OfType<DateTime>().ToList();
        dates.Should().NotBeEmpty();
        dates.Should().OnlyContain(d => d.TimeOfDay == TimeSpan.Zero);
    }

    [Fact]
    public void Sort_OnNullableDateOnly_UsesIsNull()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(DateOnlyTestHelper.Build("paging=(sort(validUntil.asc))"));

        result.Builder.AsSql().Sql.Should().Contain("ISNULL(validUntil, @p0) ASC");
        result.Builder.Build().SqlParameters[0].Argument.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void KeysetPaging_OnDateOnly_UsesCursorDate()
    {
        var result = _sut.BuildSqlQuery<TestDateOnlyEntity>(
            DateOnlyTestHelper.Build("paging=(sort(birthDate.asc.2024-01-15,$key.5),limit.10)"));

        result.Builder.AsSql().Sql.Should().Contain("birthDate > @p0");
        result.Builder.Build().SqlParameters
            .Should().Contain(sp => sp.Argument != null && sp.Argument.Equals(new DateTime(2024, 1, 15)));
    }
}

public class DateOnlyLinqTests : Pagin8TestBase
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    private static IQueryable<TestDateOnlyEntity> Data() => new List<TestDateOnlyEntity>
    {
        new() { Id = 1, BirthDate = new DateOnly(2024, 1, 10), ValidUntil = null, Child = new TestDateOnlyChild { IssuedOn = new DateOnly(2024, 1, 10) }, Holidays = [new DateOnly(2024, 1, 1)] },
        new() { Id = 2, BirthDate = new DateOnly(2024, 1, 15), ValidUntil = new DateOnly(2024, 1, 15), Child = new TestDateOnlyChild { IssuedOn = new DateOnly(2024, 1, 15) }, Holidays = [new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 25)] },
        new() { Id = 3, BirthDate = new DateOnly(2024, 2, 1), ValidUntil = new DateOnly(2024, 2, 1), Child = new TestDateOnlyChild { IssuedOn = new DateOnly(2024, 2, 1) }, Holidays = [] },
    }.AsQueryable();

    [Theory]
    [InlineData(ComparisonOperator.Equals, new[] { 2 })]
    [InlineData(ComparisonOperator.GreaterThan, new[] { 3 })]
    [InlineData(ComparisonOperator.GreaterThanOrEqual, new[] { 2, 3 })]
    [InlineData(ComparisonOperator.LessThan, new[] { 1 })]
    [InlineData(ComparisonOperator.LessThanOrEqual, new[] { 1, 2 })]
    public void Comparison_OnDateOnly_Filters(ComparisonOperator op, int[] expectedIds)
    {
        var result = CreateVisitor().Visit(new ComparisonToken("BirthDate", op, "2024-01-15", 1), Data()).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo(expectedIds);
    }

    [Fact]
    public void Comparison_OnNullableDateOnly_Filters()
    {
        var result = CreateVisitor().Visit(new ComparisonToken("ValidUntil", ComparisonOperator.GreaterThanOrEqual, "2024-01-15", 1), Data()).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void In_OnDateOnly_Filters()
    {
        var result = CreateVisitor().Visit(new InToken("BirthDate", "(2024-01-10,2024-02-01)", 1), Data()).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void In_OnNullableDateOnly_Filters()
    {
        var result = CreateVisitor().Visit(new InToken("ValidUntil", "(2024-01-15)", 1), Data()).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([2]);
    }

    [Fact]
    public void IsEmpty_OnNullableDateOnly_Filters()
    {
        var result = CreateVisitor().Visit(new IsToken("ValidUntil", "$empty", 1, isEmptyQuery: true), Data()).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([1]);
    }

    [Fact]
    public void DateRange_Ago_OnDateOnly_UsesWholeDays()
    {
        var data = new List<TestDateOnlyEntity>
        {
            new() { Id = 1, BirthDate = Today.AddDays(-5) },
            new() { Id = 2, BirthDate = Today.AddDays(-3) },
            new() { Id = 3, BirthDate = Today },
            new() { Id = 4, BirthDate = Today.AddDays(1) },
        }.AsQueryable();

        var token = new DateRangeToken("BirthDate", DateRangeOperator.Ago, 3, DateRange.Day, exact: false, strict: false, nestingLevel: 1);
        var result = CreateVisitor().Visit(token, data).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void DateRange_For_OnDateOnly_IncludesToday()
    {
        var data = new List<TestDateOnlyEntity>
        {
            new() { Id = 1, BirthDate = Today.AddDays(-1) },
            new() { Id = 2, BirthDate = Today },
            new() { Id = 3, BirthDate = Today.AddDays(3) },
            new() { Id = 4, BirthDate = Today.AddDays(4) },
        }.AsQueryable();

        var token = new DateRangeToken("BirthDate", DateRangeOperator.For, 3, DateRange.Day, exact: false, strict: false, nestingLevel: 1);
        var result = CreateVisitor().Visit(token, data).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void NegatedDateRange_OnNullableDateOnly_IncludesNulls()
    {
        var data = new List<TestDateOnlyEntity>
        {
            new() { Id = 1, ValidUntil = null },
            new() { Id = 2, ValidUntil = Today },
            new() { Id = 3, ValidUntil = Today.AddDays(-10) },
        }.AsQueryable();

        var token = new DateRangeToken("ValidUntil", DateRangeOperator.Ago, 3, DateRange.Day, exact: false, strict: false, nestingLevel: 1, isNegated: true);
        var result = CreateVisitor().Visit(token, data).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void KeysetPaging_OnDateOnly_ReturnsRowsAfterCursor()
    {
        var token = new SortToken([
            new SortExpression("birthDate", SortOrder.Ascending, "2024-01-15"),
            new SortExpression("id", SortOrder.Ascending, "2")
        ]);

        var result = CreateVisitor().Visit(token, Data()).ToList();

        result.Select(x => x.Id).Should().Equal(3);
    }

    [Fact]
    public void Sort_OnDateOnly_Orders()
    {
        var token = new SortToken([new SortExpression("birthDate", SortOrder.Descending)]);

        var result = CreateVisitor().Visit(token, Data()).ToList();

        result.Select(x => x.Id).Should().Equal(3, 2, 1);
    }

    [Fact]
    public void NestedFilter_Comparison_OnDateOnly_Filters()
    {
        var nested = new NestedFilterToken("Child", [new ComparisonToken("IssuedOn", ComparisonOperator.GreaterThanOrEqual, "2024-01-15", 2)]);

        var result = CreateVisitor().Visit(nested, Data()).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([2, 3]);
    }

    [Fact]
    public void NestedFilter_In_OnDateOnly_Filters()
    {
        var data = Data().ToList();
        data.ForEach(x => x.Children = [x.Child!]);

        var nested = new NestedFilterToken("Children", [new InToken("IssuedOn", "(2024-01-10,2024-02-01)", 2)]);

        var result = CreateVisitor().Visit(nested, data.AsQueryable()).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void ArrayOperation_Include_OnDateOnlyList_Filters()
    {
        var token = new ArrayOperationToken("Holidays", ["2024-12-25"], ArrayOperator.Include, 1);

        var result = CreateVisitor().Visit(token, Data()).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([2]);
    }

    [Fact]
    public void NegatedDateRange_OnNonNullableDateOnly_Filters()
    {
        var data = new List<TestDateOnlyEntity>
        {
            new() { Id = 1, BirthDate = Today },
            new() { Id = 2, BirthDate = Today.AddDays(-10) },
        }.AsQueryable();

        var token = new DateRangeToken("BirthDate", DateRangeOperator.Ago, 3, DateRange.Day, exact: false, strict: false, nestingLevel: 1, isNegated: true);
        var result = CreateVisitor().Visit(token, data).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([2]);
    }

    [Theory]
    [InlineData(false, new int[0])]
    [InlineData(true, new[] { 1, 2, 3 })]
    public void IsEmpty_OnNonNullableDateOnly_NeverEmpty(bool negated, int[] expectedIds)
    {
        var result = CreateVisitor().Visit(new IsToken("BirthDate", "$empty", 1, isNegated: negated, isEmptyQuery: true), Data()).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo(expectedIds);
    }

    [Fact]
    public void NegatedDateRange_OnNonNullableDateTime_Filters()
    {
        var data = new List<TestEntity>
        {
            new() { Id = 1, CreatedDate = DateTime.Now },
            new() { Id = 2, CreatedDate = DateTime.Now.AddDays(-10) },
        }.AsQueryable();

        var visitor = new LinqTokenVisitor<TestEntity>(new Pagin8MetadataProvider(new MetadataProvider()), new DateProcessor());
        var token = new DateRangeToken("CreatedDate", DateRangeOperator.Ago, 3, DateRange.Day, exact: false, strict: false, nestingLevel: 1, isNegated: true);
        var result = visitor.Visit(token, data).ToList();

        result.Select(x => x.Id).Should().BeEquivalentTo([2]);
    }

    private static LinqTokenVisitor<TestDateOnlyEntity> CreateVisitor()
        => new(new Pagin8MetadataProvider(new MetadataProvider()), new DateProcessor());
}

public class DateOnlyMetadataTests
{
    [Fact]
    public void TypeCode_ForDateOnly_IsDateTime()
    {
        var provider = new MetadataProvider();

        provider.GetTypeCodeForProperty(typeof(TestDateOnlyEntity), "birthDate").Should().Be(TypeCode.DateTime);
        provider.GetTypeCodeForProperty(typeof(TestDateOnlyEntity), "validUntil").Should().Be(TypeCode.DateTime);
    }

    [Fact]
    public void Meta_ForDateOnly_ReportsDateTime()
    {
        var meta = new Pagin8MetadataProvider(new MetadataProvider()).Get(typeof(TestDateOnlyEntity)).ToList();

        meta.Single(x => x.Name == "birthDate").Type.Should().Be("DateTime");
        meta.Single(x => x.Name == "validUntil").Type.Should().Be("DateTime");
        meta.Single(x => x.Name == "holidays").Type.Should().Be("DateTime");
    }

    [Fact]
    public void Meta_ForDateOnly_HasDateOnlyFlag()
    {
        var meta = new Pagin8MetadataProvider(new MetadataProvider()).Get(typeof(TestDateOnlyEntity)).ToList();

        meta.Single(x => x.Name == "birthDate").Flags.Should().Contain("date-only");
        meta.Single(x => x.Name == "validUntil").Flags.Should().Contain("date-only");
        meta.Single(x => x.Name == "holidays").Flags.Should().Contain("date-only").And.Contain("array");
        meta.Single(x => x.Name == "id").Flags.Should().NotContain("date-only");
    }

    [Fact]
    public void Meta_ForDateTime_HasNoDateOnlyFlag()
    {
        var meta = new Pagin8MetadataProvider(new MetadataProvider()).Get(typeof(TestEntity)).ToList();

        meta.Single(x => x.Name == "createdDate").Type.Should().Be("DateTime");
        meta.Single(x => x.Name == "createdDate").Flags.Should().NotContain("date-only");
    }
}

public class DateOnlyDapperMappingTests
{
    [Fact]
    public void FilterProvider_RegistersDateOnlyHandler_SoDateColumnsMapToDateOnly()
    {
        RuntimeHelpers.RunClassConstructor(typeof(FilterProvider).TypeHandle);

        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("BirthDate", typeof(DateTime));
        table.Columns.Add("ValidUntil", typeof(DateTime));
        table.Rows.Add(1, new DateTime(2024, 1, 15), new DateTime(2024, 2, 1));
        table.Rows.Add(2, new DateTime(2024, 3, 10), DBNull.Value);

        using var reader = table.CreateDataReader();
        var rows = Dapper.SqlMapper.Parse<TestDateOnlyEntity>(reader).ToList();

        rows[0].BirthDate.Should().Be(new DateOnly(2024, 1, 15));
        rows[0].ValidUntil.Should().Be(new DateOnly(2024, 2, 1));
        rows[1].BirthDate.Should().Be(new DateOnly(2024, 3, 10));
        rows[1].ValidUntil.Should().BeNull();
    }
}
