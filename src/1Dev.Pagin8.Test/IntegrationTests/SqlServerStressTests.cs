using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using Dapper;
using FluentAssertions;
using Internal.Configuration;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace _1Dev.Pagin8.Test.IntegrationTests;

/// <summary>
/// Performance and stress tests for SQL Server provider with large datasets (300k+ records)
/// These tests verify scalability and performance characteristics under load
/// </summary>
[Collection("Database Collection")]
[Trait("Category", "Performance")]
[Trait("Category", "Stress")]
[Trait("Database", "SqlServer")]
public class SqlServerStressTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ISqlQueryBuilder _queryBuilder;

    public SqlServerStressTests(DatabaseFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;

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
        var tokenVisitor = new SqlServerTokenVisitor(metadataProvider, dateProcessor);
        _queryBuilder = new SqlQueryBuilder(tokenizationService, tokenVisitor);
    }

    #region Simple Indexed Queries

    [Fact(DisplayName = "PERF: Simple Equality (Indexed)")]
    public async Task Performance_SimpleEquality()
    {
        var (result, elapsed) = await ExecuteTimedQuery("status=eq.Active");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().NotBeEmpty();
        elapsed.Should().BeLessThan(50, "indexed equality should be fast");
    }

    [Fact(DisplayName = "PERF: Range Query (Indexed)")]
    public async Task Performance_RangeQuery()
    {
        var (result, elapsed) = await ExecuteTimedQuery("amount=gte.500&amount=lte.800");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().NotBeEmpty();
        elapsed.Should().BeLessThan(100, "indexed range query should be fast");
    }

    [Fact(DisplayName = "PERF: Date Range (Indexed)")]
    public async Task Performance_DateRange()
    {
        var (result, elapsed) = await ExecuteTimedQuery("recordDate=gte.2024-01-01&recordDate=lte.2024-12-31");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().NotBeEmpty();
        elapsed.Should().BeLessThan(100, "indexed date range should be fast");
    }

    [Fact(DisplayName = "PERF: IN Operator (Indexed)")]
    public async Task Performance_InOperator()
    {
        var (result, elapsed) = await ExecuteTimedQuery("category=in.(Premium,Enterprise)");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().NotBeEmpty();
        elapsed.Should().BeLessThan(100, "indexed IN operator should be fast");
    }

    #endregion

    #region String Search Operations

    [Fact(DisplayName = "PERF: Contains Search (LIKE %...%)")]
    public async Task Performance_ContainsSearch()
    {
        var (result, elapsed) = await ExecuteTimedQuery("customerName=cs.John");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().NotBeEmpty();
        elapsed.Should().BeLessThan(200, "string contains should complete within reasonable time");
    }

    [Fact(DisplayName = "PERF: Starts With (LIKE ...%)")]
    public async Task Performance_StartsWith()
    {
        var (result, elapsed) = await ExecuteTimedQuery("customerName=stw.John");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().NotBeEmpty();
        elapsed.Should().BeLessThan(200, "starts with should be reasonably fast");
    }

    #endregion

    #region Complex Filters

    [Fact(DisplayName = "PERF: Multiple AND Conditions")]
    public async Task Performance_MultipleAndConditions()
    {
        var (result, elapsed) = await ExecuteTimedQuery("status=eq.Active&category=eq.Premium&amount=gt.500");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().NotBeEmpty();
        elapsed.Should().BeLessThan(150, "multiple AND conditions should be fast");
    }

    [Fact(DisplayName = "PERF: OR Groups")]
    public async Task Performance_OrGroups()
    {
        var (result, elapsed) = await ExecuteTimedQuery("or=(status.eq.Active,status.eq.Pending)&amount=gt.100");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().NotBeEmpty();
        elapsed.Should().BeLessThan(150, "OR groups should have minimal overhead");
    }

    [Fact(DisplayName = "PERF: Nested OR Groups")]
    public async Task Performance_NestedOrGroups()
    {
        var (result, elapsed) = await ExecuteTimedQuery("or=(status.eq.Active,or(category.eq.Premium,category.eq.Enterprise))");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().NotBeEmpty();
        elapsed.Should().BeLessThan(200, "nested OR should complete within reasonable time");
    }

    [Fact(DisplayName = "PERF: Complex Real-World Query")]
    public async Task Performance_ComplexRealWorld()
    {
        var (result, elapsed) = await ExecuteTimedQuery("status=eq.Active&category=in.(Premium,Enterprise)&amount=gte.200&recordDate=ago.30d");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        elapsed.Should().BeLessThan(300, "complex queries should complete within reasonable time");
    }

    #endregion

    #region Sorting & Pagination

    [Fact(DisplayName = "PERF: Sort Single Column (1000 records)")]
    public async Task Performance_SortSingleColumn()
    {
        var (result, elapsed) = await ExecuteTimedQuery("paging=(sort(amount.desc),limit.1000)");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().HaveCount(1000);
        elapsed.Should().BeLessThan(500, "single column sort should complete within reasonable time");
    }

    [Fact(DisplayName = "PERF: Sort Multiple Columns (1000 records)")]
    public async Task Performance_SortMultipleColumns()
    {
        var (result, elapsed) = await ExecuteTimedQuery("paging=(sort(category.asc,amount.desc),limit.1000)");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        result.Should().HaveCount(1000);
        elapsed.Should().BeLessThan(600, "multi-column sort is expensive but should complete");
    }

    [Fact(DisplayName = "PERF: Large Result Set (10k records)")]
    public async Task Performance_LargeResultSet()
    {
        var (result, elapsed) = await ExecuteTimedQuery("status=eq.Active&paging=(limit.10000)");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms | Records: {result.Count}");
        result.Should().HaveCountLessOrEqualTo(10000);
        elapsed.Should().BeLessThan(300, "fetching 10k records should be fast");
    }

    #endregion

    #region Stress Tests (Large Results)

    [Fact(DisplayName = "STRESS: Fetch 50k Records")]
    public async Task Stress_Fetch50kRecords()
    {
        var (result, elapsed) = await ExecuteTimedQuery("paging=(limit.50000)");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms | Records: {result.Count}");
        result.Should().HaveCountLessOrEqualTo(50000);
        elapsed.Should().BeLessThan(1000, "fetching 50k records should complete within 1 second");
    }

    [Fact(DisplayName = "STRESS: Complex Query + 10k Results")]
    public async Task Stress_ComplexQuery10kResults()
    {
        var (result, elapsed) = await ExecuteTimedQuery("or=(status.eq.Active,status.eq.Pending)&paging=(sort(amount.desc),limit.10000)");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms | Records: {result.Count}");
        result.Should().HaveCountLessOrEqualTo(10000);
        elapsed.Should().BeLessThan(500, "complex query with large results should complete within reasonable time");
    }

    [Fact(DisplayName = "STRESS: String Search + 5k Results")]
    public async Task Stress_StringSearch5kResults()
    {
        var (result, elapsed) = await ExecuteTimedQuery("customerName=stw.J&paging=(limit.5000)");
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms | Records: {result.Count}");
        elapsed.Should().BeLessThan(300, "string search with large results should be acceptable");
    }

    #endregion

    #region Memory Tests

    [Fact(DisplayName = "MEMORY: Large Result Set Memory Usage")]
    public async Task Memory_LargeResultSet()
    {
        var initialMemory = GC.GetTotalMemory(true);
        
        var (result, elapsed) = await ExecuteTimedQuery("paging=(limit.10000)");
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryUsedMB = (finalMemory - initialMemory) / 1024.0 / 1024.0;
        
        _output.WriteLine($"⏱️  Execution time: {elapsed}ms");
        _output.WriteLine($"💾 Memory used: {memoryUsedMB:F2} MB for {result.Count} records");
        
        memoryUsedMB.Should().BeLessThan(50, "memory usage should be efficient");
    }

    #endregion

    #region Helper Methods

    private async Task<(List<ArchiveRecord> Result, long ElapsedMs)> ExecuteTimedQuery(string queryString)
    {
        var stopwatch = Stopwatch.StartNew();

        var parameters = QueryBuilderParameters.Create(
            _fixture.Connection!,
            FormattableStringFactory.Create("SELECT * FROM Archive WHERE 1=1"),
            QueryInputParameters.Create(
                sql: "Archive",
                queryString: queryString,
                defaultQueryString: "",
                ignoreLimit: false
            )
        );

        var result = _queryBuilder.BuildSqlQuery<ArchiveRecord>(parameters);

        if (result.Builder == null)
        {
            stopwatch.Stop();
            return (new List<ArchiveRecord>(), stopwatch.ElapsedMilliseconds);
        }

        var builtQuery = result.Builder.Build();

        var dapperParams = new DynamicParameters();
        for (int i = 0; i < builtQuery.SqlParameters.Count; i++)
        {
            var param = builtQuery.SqlParameters[i];
            dapperParams.Add($"@p{i}", param.Argument);
        }

        var data = await _fixture.Connection!.QueryAsync<ArchiveRecord>(builtQuery.Sql, dapperParams);
        stopwatch.Stop();

        var dataList = data.ToList();

        _output.WriteLine($"Query: {queryString}");
        _output.WriteLine($"SQL: {result.Builder.AsSql().Sql}");

        return (dataList, stopwatch.ElapsedMilliseconds);
    }

    #endregion
}
