using System.ComponentModel.DataAnnotations;
using System.Globalization;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;

namespace _1Dev.Pagin8.Test;

public class TestExceptionEntity
{
    [Key]
    public int Id { get; set; }
    public float? Ratio { get; set; }
    public Guid? Reference { get; set; }
    public double Score { get; set; }
    public TestEntity? Child { get; set; }
}

/// <summary>
/// Invalid user input must surface as Pagin8Exception (mappable to 400), never as
/// NotSupportedException / ArgumentException / TargetInvocationException (500).
/// </summary>
public abstract class UserInputExceptionTestsBase
{
    protected abstract ISqlQueryBuilder Sut { get; }

    [Theory]
    [InlineData("this is not a filter")]
    [InlineData("id=incl(1) OR 1=1")]
    [InlineData("id=eq.abc")]
    [InlineData("child.with=(amount.gt.abc)")]
    [InlineData("child.with=(paging=(limit.5))")]
    [InlineData("paging=(sort(reference.asc))")]
    public void InvalidInput_ThrowsPagin8Exception(string query)
    {
        var act = () => Sut.BuildSqlQuery<TestExceptionEntity>(DateOnlyTestHelper.Build(query));

        act.Should().Throw<Pagin8Exception>();
    }

    [Fact]
    public void Sort_OnNullableFloat_IsSupported()
    {
        var result = Sut.BuildSqlQuery<TestExceptionEntity>(DateOnlyTestHelper.Build("paging=(sort(ratio.asc))"));

        result.Builder.AsSql().Sql.Should().Contain("ratio, @p0");
    }

    [Fact]
    public void Double_IsParsedWithInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("sr-Latn-RS");

            var result = Sut.BuildSqlQuery<TestExceptionEntity>(DateOnlyTestHelper.Build("score=gt.1.5"));

            result.Builder.Build().SqlParameters[0].Argument.Should().Be(1.5d);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}

[Collection("PostgreSQL QueryBuilder Tests")]
public class UserInputExceptionPostgreSqlTests : UserInputExceptionTestsBase
{
    public UserInputExceptionPostgreSqlTests()
    {
        PostgreSqlTestBootstrap.Init();
        Sut = DateOnlyTestHelper.CreateBuilder((m, d) => new NpgsqlTokenVisitor(m, d));
    }

    protected override ISqlQueryBuilder Sut { get; }
}

[Collection("SQL Server QueryBuilder Tests")]
public class UserInputExceptionSqlServerTests : UserInputExceptionTestsBase
{
    public UserInputExceptionSqlServerTests()
    {
        SqlServerTestBootstrap.Init();
        Sut = DateOnlyTestHelper.CreateBuilder((m, d) => new SqlServerTokenVisitor(m, d));
    }

    protected override ISqlQueryBuilder Sut { get; }
}
