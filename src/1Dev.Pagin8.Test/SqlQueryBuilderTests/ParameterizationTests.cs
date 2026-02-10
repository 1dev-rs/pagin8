using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using FluentAssertions;
using Internal.Configuration;
using Xunit;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests;

/// <summary>
/// Tests to verify SQL injection protection via parameterized queries
/// </summary>
public class ParameterizationTests
{
    private readonly ISqlQueryBuilder _sqlServerQueryBuilder;
    private readonly ISqlQueryBuilder _postgresQueryBuilder;

    public ParameterizationTests()
    {
        Pagin8Runtime.Initialize(new ServiceConfiguration
        {
            MaxNestingLevel = 5,
            PagingSettings = new PagingSettings
            {
                DefaultPerPage = 50,
                MaxItemsPerPage = 100_000,
                MaxSafeItemCount = 1_000_000
            },
            DatabaseType = DatabaseType.SqlServer
        });

        var tokenizer = new Tokenizer();
        var contextValidator = new PassThroughContextValidator();
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor = new DateProcessor();
        var tokenizationService = new TokenizationService(tokenizer, contextValidator, metadataProvider);

        // SQL Server visitor
        var sqlServerVisitor = new SqlServerTokenVisitor(metadataProvider, dateProcessor);
        _sqlServerQueryBuilder = new SqlQueryBuilder(tokenizationService, sqlServerVisitor);

        // PostgreSQL visitor
        var postgresVisitor = new NpgsqlTokenVisitor(metadataProvider, dateProcessor);
        _postgresQueryBuilder = new SqlQueryBuilder(tokenizationService, postgresVisitor);
    }

    [Fact(DisplayName = "SQL Server IN operator should use parameterized queries for string values")]
    public void SqlServer_InOperator_ShouldParameterizeStringValues()
    {
        // Arrange
        var queryString = "name=in.(Alice,Bob,Charlie)";
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM TestEntities WHERE 1=1",
                queryString: queryString,
                defaultQueryString: "",
                ignoreLimit: false
            )
        };

        // Act
        var result = _sqlServerQueryBuilder.BuildSqlQuery<TestEntity>(parameters);
        var builtQuery = result.Builder!.AsSql();

        // Assert
        builtQuery.Sql.Should().NotBeEmpty();
        
        // Verify that the SQL contains parameter placeholders, not literal values
        builtQuery.Sql.Should().Contain("@p", "SQL should contain parameter placeholders");
        builtQuery.Sql.Should().NotContain("'Alice'", "SQL should not contain literal string values");
        builtQuery.Sql.Should().NotContain("'Bob'", "SQL should not contain literal string values");
        builtQuery.Sql.Should().NotContain("'Charlie'", "SQL should not contain literal string values");
        
        // Verify parameters are created
        builtQuery.SqlParameters.Should().NotBeEmpty("Parameters should be generated");
        var paramValues = builtQuery.SqlParameters.Select(p => p.Argument?.ToString()?.ToLower()).ToList();
        paramValues.Should().Contain("alice");
        paramValues.Should().Contain("bob");
        paramValues.Should().Contain("charlie");
    }

    [Fact(DisplayName = "PostgreSQL IN operator should use parameterized queries for string values")]
    public void Postgres_InOperator_ShouldParameterizeStringValues()
    {
        // Arrange
        var queryString = "name=in.(Alice,Bob,Charlie)";
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entities WHERE 1=1",
                queryString: queryString,
                defaultQueryString: "",
                ignoreLimit: false
            )
        };

        // Act
        var result = _postgresQueryBuilder.BuildSqlQuery<TestEntity>(parameters);
        var builtQuery = result.Builder!.AsSql();

        // Assert
        builtQuery.Sql.Should().NotBeEmpty();
        
        // Verify that the SQL contains parameter placeholders, not literal values
        builtQuery.Sql.Should().Contain("@p", "SQL should contain parameter placeholders");
        builtQuery.Sql.Should().NotContain("'alice'", "SQL should not contain literal string values");
        builtQuery.Sql.Should().NotContain("'bob'", "SQL should not contain literal string values");
        builtQuery.Sql.Should().NotContain("'charlie'", "SQL should not contain literal string values");
        
        // Verify parameters are created
        builtQuery.SqlParameters.Should().NotBeEmpty("Parameters should be generated");
        var paramValues = builtQuery.SqlParameters.Select(p => p.Argument?.ToString()?.ToLower()).ToList();
        paramValues.Should().Contain("alice");
        paramValues.Should().Contain("bob");
        paramValues.Should().Contain("charlie");
    }

    [Fact(DisplayName = "SQL Server IN operator with SQL injection attempt should be safely parameterized")]
    public void SqlServer_InOperator_WithInjectionAttempt_ShouldBeSafe()
    {
        // Arrange
        // Attempt SQL injection via IN operator
        var queryString = "name=in.(Alice' OR '1'='1,Bob)";
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM TestEntities WHERE 1=1",
                queryString: queryString,
                defaultQueryString: "",
                ignoreLimit: false
            )
        };

        // Act
        var result = _sqlServerQueryBuilder.BuildSqlQuery<TestEntity>(parameters);
        var builtQuery = result.Builder!.AsSql();

        // Assert
        builtQuery.Sql.Should().NotBeEmpty();
        
        // The injection attempt should be treated as a literal parameter value
        var paramValues = builtQuery.SqlParameters.Select(p => p.Argument?.ToString()?.ToLower()).ToList();
        var hasInjectionAttemptAsParam = paramValues.Any(v => v != null && v.Contains("alice' or '1'='1"));
        hasInjectionAttemptAsParam.Should().BeTrue("Injection attempt should be treated as a literal string parameter");
        
        // SQL should use parameter placeholders
        builtQuery.Sql.Should().Contain("@p");
        builtQuery.Sql.Should().NotContain("OR '1'='1'", "Injection attempt should not be executed");
    }

    [Fact(DisplayName = "PostgreSQL IN operator with SQL injection attempt should be safely parameterized")]
    public void Postgres_InOperator_WithInjectionAttempt_ShouldBeSafe()
    {
        // Arrange
        // Attempt SQL injection via IN operator
        var queryString = "name=in.(Alice' OR '1'='1,Bob)";
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entities WHERE 1=1",
                queryString: queryString,
                defaultQueryString: "",
                ignoreLimit: false
            )
        };

        // Act
        var result = _postgresQueryBuilder.BuildSqlQuery<TestEntity>(parameters);
        var builtQuery = result.Builder!.AsSql();

        // Assert
        builtQuery.Sql.Should().NotBeEmpty();
        
        // The injection attempt should be treated as a literal parameter value
        var paramValues = builtQuery.SqlParameters.Select(p => p.Argument?.ToString()?.ToLower()).ToList();
        var hasInjectionAttemptAsParam = paramValues.Any(v => v != null && v.Contains("alice' or '1'='1"));
        hasInjectionAttemptAsParam.Should().BeTrue("Injection attempt should be treated as a literal string parameter");
        
        // SQL should use parameter placeholders
        builtQuery.Sql.Should().Contain("@p");
        builtQuery.Sql.Should().NotContain("OR '1'='1'", "Injection attempt should not be executed");
    }

    [Fact(DisplayName = "PostgreSQL IN operator should use ANY(ARRAY) for performance")]
    public void Postgres_InOperator_ShouldUseAnyArray()
    {
        // Arrange
        var queryString = "name=in.(Alice,Bob,Charlie)";
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM test_entities WHERE 1=1",
                queryString: queryString,
                defaultQueryString: "",
                ignoreLimit: false
            )
        };

        // Act
        var result = _postgresQueryBuilder.BuildSqlQuery<TestEntity>(parameters);
        var builtQuery = result.Builder!.AsSql();

        // Assert
        builtQuery.Sql.Should().NotBeEmpty();
        
        // Verify it uses PostgreSQL's efficient ANY(ARRAY[...]) syntax instead of OR conditions
        builtQuery.Sql.Should().Contain("ANY(ARRAY[", "Should use PostgreSQL's ANY operator for efficiency");
        builtQuery.Sql.Should().NotContain(" OR ", "Should not use multiple OR conditions");
    }

    [Fact(DisplayName = "SQL Server IN operator should use efficient IN clause")]
    public void SqlServer_InOperator_ShouldUseInClause()
    {
        // Arrange
        var queryString = "name=in.(Alice,Bob,Charlie)";
        var parameters = new QueryBuilderParameters
        {
            InputParameters = QueryInputParameters.Create(
                sql: "SELECT * FROM TestEntities WHERE 1=1",
                queryString: queryString,
                defaultQueryString: "",
                ignoreLimit: false
            )
        };

        // Act
        var result = _sqlServerQueryBuilder.BuildSqlQuery<TestEntity>(parameters);
        var builtQuery = result.Builder!.AsSql();

        // Assert
        builtQuery.Sql.Should().NotBeEmpty();
        
        // Verify it uses SQL Server's IN clause, not multiple OR conditions
        builtQuery.Sql.Should().Contain(" IN (", "Should use SQL Server's IN operator");
        builtQuery.Sql.Should().Contain("LOWER(", "Should use LOWER() for case-insensitive comparison");
        builtQuery.Sql.Should().NotContain(" OR ", "Should not use multiple OR conditions");
    }
}
