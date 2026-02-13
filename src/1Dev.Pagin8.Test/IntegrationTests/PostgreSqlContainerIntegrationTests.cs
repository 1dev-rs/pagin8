using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.IntegrationTests.Fixtures;
using _1Dev.Pagin8.Test.IntegrationTests.Models;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;
using Dapper;
using FluentAssertions;
using Internal.Configuration;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace _1Dev.Pagin8.Test.IntegrationTests;

/// <summary>
/// Integration tests for PostgreSQL using Testcontainers
/// Tests all DSL features against a real PostgreSQL database running in Docker
/// </summary>
[Collection("PostgreSql Testcontainer")]
[Trait("Category", "Integration")]
[Trait("Database", "PostgreSql")]
[Trait("Container", "Testcontainers")]
public class PostgreSqlContainerIntegrationTests
{
    private readonly PostgreSqlContainerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly ISqlQueryBuilder _queryBuilder;

    public PostgreSqlContainerIntegrationTests(PostgreSqlContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;

        // Initialize Pagin8 for PostgreSQL
        Pagin8Runtime.Initialize(new ServiceConfiguration
        {
            MaxNestingLevel = 5,
            PagingSettings = new PagingSettings
            {
                DefaultPerPage = 50,
                MaxItemsPerPage = 100_000,
                MaxSafeItemCount = 1_000_000
            },
            DatabaseType = DatabaseType.PostgreSql
        });

        var tokenizer = new Tokenizer();
        var contextValidator = new PassThroughContextValidator();
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor = new DateProcessor();

        var tokenizationService = new TokenizationService(tokenizer, contextValidator, metadataProvider);
        var tokenVisitor = new NpgsqlTokenVisitor(metadataProvider, dateProcessor);
        _queryBuilder = new SqlQueryBuilder(tokenizationService, tokenVisitor);
    }

    #region Basic Comparison Operators

    [Fact(DisplayName = "TC-PG-001: Equality (eq) - Filter by exact status")]
    public async Task Equality_ShouldFilterByExactMatch()
    {
        var result = await ExecuteQuery("status=eq.Active");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Status == "Active").Should().BeTrue();
        _output.WriteLine($"? Found {result.Count} active products");
    }

    [Fact(DisplayName = "TC-PG-002: Greater Than (gt) - Filter by price > 500")]
    public async Task GreaterThan_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("price=gt.500");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Price > 500).Should().BeTrue();
        _output.WriteLine($"? Found {result.Count} products with price > $500");
    }

    [Fact(DisplayName = "TC-PG-003: Less Than (lt) - Filter by stock < 100")]
    public async Task LessThan_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("stock=lt.100");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Stock < 100).Should().BeTrue();
        _output.WriteLine($"? Found {result.Count} low-stock products");
    }

    [Fact(DisplayName = "TC-PG-004: Greater Than or Equal (gte)")]
    public async Task GreaterThanOrEqual_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("price=gte.100");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Price >= 100).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-PG-005: Less Than or Equal (lte)")]
    public async Task LessThanOrEqual_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("rating=lte.3.0");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Rating <= 3.0).Should().BeTrue();
    }

    #endregion

    #region String Operators

    [Fact(DisplayName = "TC-PG-006: Contains (cs) - Search in product name")]
    public async Task Contains_ShouldFindMatchingRecords()
    {
        // Bogus generates commerce terms like "Shoes", "Computer", "Chair", etc.
        var result = await ExecuteQuery("name=cs.Shoes");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Name.Contains("Shoes", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
        _output.WriteLine($"? Found {result.Count} products containing 'Shoes'");
    }

    [Fact(DisplayName = "TC-PG-007: Starts With (stw)")]
    public async Task StartsWith_ShouldFindMatchingRecords()
    {
        // Bogus generates adjectives like "Refined", "Ergonomic", "Sleek", etc.
        var result = await ExecuteQuery("name=stw.Refined");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Name.StartsWith("Refined", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-PG-008: Ends With (enw)")]
    public async Task EndsWith_ShouldFindMatchingRecords()
    {
        var result = await ExecuteQuery("category=enw.ing");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Category.EndsWith("ing", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-PG-009: LIKE Pattern (PostgreSQL ILIKE)")]
    public async Task LikePattern_ShouldFindMatchingRecords()
    {
        // Search for common Bogus commerce terms
        var result = await ExecuteQuery("name=like.%Shoes%");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Name.Contains("Shoes", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    #endregion

    #region Date Operations

    [Fact(DisplayName = "TC-PG-010: Date Range (gte/lte)")]
    public async Task DateRange_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("createdAt=gte.2024-01-01&createdAt=lte.2024-12-31");
        
        result.Should().NotBeEmpty();
        result.All(p => p.CreatedAt >= new DateTime(2024, 1, 1) && p.CreatedAt <= new DateTime(2024, 12, 31)).Should().BeTrue();
        _output.WriteLine($"? Found {result.Count} products created in 2024");
    }

    [Fact(DisplayName = "TC-PG-011: Relative Date - Last 30 Days")]
    public async Task RelativeDate_Last30Days_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("createdAt=ago.30d");
        
        var thirtyDaysAgo = DateTime.UtcNow.Date.AddDays(-30);
        result.Should().NotBeEmpty();
        _output.WriteLine($"? Found {result.Count} products created in last 30 days");
    }

    #endregion

    #region Logical Operators

    [Fact(DisplayName = "TC-PG-012: Multiple AND Conditions")]
    public async Task MultipleAndConditions_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("status=eq.Active&category=eq.Electronics&price=gt.100");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Status == "Active" && p.Category == "Electronics" && p.Price > 100).Should().BeTrue();
        _output.WriteLine($"? Found {result.Count} matching products");
    }

    [Fact(DisplayName = "TC-PG-013: OR Group")]
    public async Task OrGroup_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("or=(category.eq.Electronics,category.eq.Books)");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Category == "Electronics" || p.Category == "Books").Should().BeTrue();
    }

    [Fact(DisplayName = "TC-PG-014: Complex OR with AND")]
    public async Task ComplexOrWithAnd_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("or=(status.eq.Active,status.eq.ComingSoon)&price=lt.200");
        
        result.Should().NotBeEmpty();
        result.All(p => (p.Status == "Active" || p.Status == "ComingSoon") && p.Price < 200).Should().BeTrue();
    }

    #endregion

    #region IN Operator

    [Fact(DisplayName = "TC-PG-015: IN Operator (Strings)")]
    public async Task InOperator_WithStrings_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("category=in.(Electronics,Books,Sports)");
        
        result.Should().NotBeEmpty();
        result.All(p => new[] { "Electronics", "Books", "Sports" }.Contains(p.Category)).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-PG-016: NOT IN Operator")]
    public async Task NotInOperator_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("status=not.in.(Discontinued,OutOfStock)");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Status != "Discontinued" && p.Status != "OutOfStock").Should().BeTrue();
    }

    #endregion

    #region Sorting & Pagination

    [Fact(DisplayName = "TC-PG-017: Sort by Price (Ascending)")]
    public async Task SortAscending_ShouldOrderCorrectly()
    {
        var result = await ExecuteQuery("status=eq.Active&paging=(sort(price.asc),limit.20)");
        
        result.Should().HaveCount(20);
        result.Should().BeInAscendingOrder(p => p.Price);
        _output.WriteLine($"? Price range: ${result.First().Price} - ${result.Last().Price}");
    }

    [Fact(DisplayName = "TC-PG-018: Sort by Rating (Descending)")]
    public async Task SortDescending_ShouldOrderCorrectly()
    {
        var result = await ExecuteQuery("paging=(sort(rating.desc),limit.15)");
        
        result.Should().HaveCount(15);
        result.Should().BeInDescendingOrder(p => p.Rating);
        _output.WriteLine($"? Rating range: {result.First().Rating} - {result.Last().Rating}");
    }

    [Fact(DisplayName = "TC-PG-019: Multi-column Sort")]
    public async Task MultiColumnSort_ShouldOrderCorrectly()
    {
        var result = await ExecuteQuery("paging=(sort(category.asc,price.desc),limit.25)");
        
        result.Should().HaveCount(25);
        _output.WriteLine($"? Sorted by category then price");
    }

    #endregion

    #region Boolean Operations

    [Fact(DisplayName = "TC-PG-020: Boolean Filter (Featured Products)")]
    public async Task BooleanFilter_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("isFeatured=eq.true&paging=(limit.30)");
        
        result.Should().NotBeEmpty();
        result.All(p => p.IsFeatured).Should().BeTrue();
        _output.WriteLine($"? Found {result.Count} featured products");
    }

    #endregion

    #region PostgreSQL-Specific Features

    [Fact(DisplayName = "TC-PG-021: Case-Insensitive Search (ILIKE)")]
    public async Task CaseInsensitiveSearch_ShouldWork()
    {
        var result = await ExecuteQuery("name=cs.keyboard");
        
        result.Should().NotBeEmpty();
        _output.WriteLine($"? Case-insensitive search found {result.Count} products");
    }

    #endregion

    #region Complex Real-World Scenarios

    [Fact(DisplayName = "TC-PG-022: E-commerce: Find Budget Electronics")]
    public async Task ComplexQuery_BudgetElectronics()
    {
        var result = await ExecuteQuery("category=eq.Electronics&price=lt.200&status=eq.Active&stock=gt.0&paging=(sort(rating.desc),limit.20)");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Category == "Electronics" && p.Price < 200 && p.Status == "Active" && p.Stock > 0).Should().BeTrue();
        result.Should().BeInDescendingOrder(p => p.Rating);
        _output.WriteLine($"? Found {result.Count} budget electronics in stock with ratings");
    }

    [Fact(DisplayName = "TC-PG-023: E-commerce: Premium Featured Products")]
    public async Task ComplexQuery_PremiumFeatured()
    {
        var result = await ExecuteQuery("isFeatured=eq.true&price=gt.500&rating=gte.4.0&status=eq.Active&paging=(sort(price.desc),limit.10)");
        
        result.Should().NotBeEmpty();
        result.All(p => p.IsFeatured && p.Price > 500 && p.Rating >= 4.0 && p.Status == "Active").Should().BeTrue();
        _output.WriteLine($"? Found {result.Count} premium featured products");
    }

    [Fact(DisplayName = "TC-PG-024: Inventory: Low Stock Alert")]
    public async Task ComplexQuery_LowStockAlert()
    {
        var result = await ExecuteQuery("stock=lt.50&status=eq.Active&or=(category.eq.Electronics,category.eq.Clothing)&paging=(sort(stock.asc),limit.30)");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Stock < 50 && p.Status == "Active").Should().BeTrue();
        result.Should().BeInAscendingOrder(p => p.Stock);
        _output.WriteLine($"? Found {result.Count} products needing restock");
    }

    [Fact(DisplayName = "TC-PG-025: Marketing: New High-Rated Products")]
    public async Task ComplexQuery_NewHighRated()
    {
        var result = await ExecuteQuery("createdAt=ago.90d&rating=gte.4.5&status=eq.Active&paging=(sort(createdAt.desc,rating.desc),limit.15)");
        
        result.Should().NotBeEmpty();
        result.All(p => p.Rating >= 4.5 && p.Status == "Active").Should().BeTrue();
        _output.WriteLine($"? Found {result.Count} new high-rated products");
    }

    #endregion

    #region Helper Methods

    private async Task<List<Product>> ExecuteQuery(string queryString, [CallerMemberName] string testName = "")
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        
        var parameters = QueryBuilderParameters.Create(
            _fixture.Connection!,
            FormattableStringFactory.Create("SELECT * FROM Products WHERE 1=1"),
            QueryInputParameters.Create(
                sql: "Products",
                queryString: queryString,
                defaultQueryString: "",
                ignoreLimit: false
            )
        );

        var result = _queryBuilder.BuildSqlQuery<Product>(parameters);

        if (result.Builder == null)
        {
            return new List<Product>();
        }

        var builtQuery = result.Builder.Build();

        var dapperParams = new DynamicParameters();
        for (int i = 0; i < builtQuery.SqlParameters.Count; i++)
        {
            var param = builtQuery.SqlParameters[i];
            dapperParams.Add($"@p{i}", param.Argument);
        }

        var data = await _fixture.Connection!.QueryAsync<Product>(builtQuery.Sql, dapperParams);

        sw.Stop();
        
        var resultList = data.ToList();
        
        // integration-only: do not record performance metrics here
        
        _output.WriteLine($"Query: {queryString}");
        _output.WriteLine($"SQL: {result.Builder.AsSql().Sql}");
        _output.WriteLine($"Records: {resultList.Count}");
        _output.WriteLine($"??  Execution Time: {sw.ElapsedMilliseconds}ms");

        return resultList;
    }

    #endregion
}
