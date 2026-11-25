using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Implementations;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Extensions.Backend.Models;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Test.IntegrationTests.Fixtures;
using _1Dev.Pagin8.Test.IntegrationTests.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace _1Dev.Pagin8.Test.IntegrationTests;

/// <summary>
/// Integration tests for DuckDB through the Backend's DuckDB filter provider, against an in-memory
/// database seeded with the same products as the container tests. No Docker needed.
/// </summary>
[Collection("DuckDb")]
[Trait("Category", "Integration")]
[Trait("Database", "DuckDb")]
public class DuckDbIntegrationTests
{
    private readonly DuckDbFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly IDuckDbFilterProvider _provider;

    public DuckDbIntegrationTests(DuckDbFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;

        var services = new ServiceCollection();
        services.AddPagin8(cfg =>
        {
            cfg.DatabaseType = DatabaseType.PostgreSql;
            cfg.PagingSettings = new PagingSettings
            {
                DefaultPerPage = 50,
                MaxItemsPerPage = 100_000,
                MaxSafeItemCount = 1_000_000
            };
        });
        services.AddPagin8BackendDuckDb(fixture.CreateConnection, commandTimeout: 30);
        _provider = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDuckDbFilterProvider>();
    }

    #region Basic Comparison Operators

    [Fact(DisplayName = "TC-DUCK-001: Equality (eq) - Filter by exact status")]
    public async Task Equality_ShouldFilterByExactMatch()
    {
        var result = await ExecuteQuery("status=eq.Active");

        result.Should().NotBeEmpty();
        result.All(p => p.Status == "Active").Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-002: Greater Than (gt)")]
    public async Task GreaterThan_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("price=gt.500");

        result.Should().NotBeEmpty();
        result.All(p => p.Price > 500).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-003: Less Than (lt)")]
    public async Task LessThan_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("stock=lt.100");

        result.Should().NotBeEmpty();
        result.All(p => p.Stock < 100).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-004: Greater Than or Equal (gte)")]
    public async Task GreaterThanOrEqual_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("price=gte.100");

        result.Should().NotBeEmpty();
        result.All(p => p.Price >= 100).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-005: Less Than or Equal (lte)")]
    public async Task LessThanOrEqual_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("rating=lte.3.0");

        result.Should().NotBeEmpty();
        result.All(p => p.Rating <= 3.0).Should().BeTrue();
    }

    #endregion

    #region String Operators

    [Fact(DisplayName = "TC-DUCK-006: Contains (cs)")]
    public async Task Contains_ShouldFindMatchingRecords()
    {
        var result = await ExecuteQuery("name=cs.Shoes");

        result.Should().NotBeEmpty();
        result.All(p => p.Name.Contains("Shoes", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-007: Starts With (stw)")]
    public async Task StartsWith_ShouldFindMatchingRecords()
    {
        var result = await ExecuteQuery("name=stw.Refined");

        result.Should().NotBeEmpty();
        result.All(p => p.Name.StartsWith("Refined", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-008: Ends With (enw)")]
    public async Task EndsWith_ShouldFindMatchingRecords()
    {
        var result = await ExecuteQuery("category=enw.ing");

        result.Should().NotBeEmpty();
        result.All(p => p.Category.EndsWith("ing", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-009: LIKE Pattern")]
    public async Task LikePattern_ShouldFindMatchingRecords()
    {
        var result = await ExecuteQuery("name=like.%Shoes%");

        result.Should().NotBeEmpty();
        result.All(p => p.Name.Contains("Shoes", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-021: Case-Insensitive Search")]
    public async Task CaseInsensitiveSearch_ShouldWork()
    {
        var lower = await ExecuteQuery("name=cs.keyboard&paging=(limit.100000)");
        var upper = await ExecuteQuery("name=cs.KEYBOARD&paging=(limit.100000)");

        lower.Should().NotBeEmpty();
        lower.Select(p => p.Id).Should().BeEquivalentTo(upper.Select(p => p.Id));
    }

    #endregion

    #region Date Operations

    [Fact(DisplayName = "TC-DUCK-010: Date Range (gte/lte)")]
    public async Task DateRange_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("createdAt=gte.2025-01-01&createdAt=lte.2025-12-31");

        result.Should().NotBeEmpty();
        result.All(p => p.CreatedAt.Date >= new DateTime(2025, 1, 1) && p.CreatedAt.Date <= new DateTime(2025, 12, 31)).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-011: Relative Date - Last 30 Days")]
    public async Task RelativeDate_Last30Days_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("createdAt=ago.30d");

        result.Should().NotBeEmpty();
    }

    #endregion

    #region Logical Operators

    [Fact(DisplayName = "TC-DUCK-012: Multiple AND Conditions")]
    public async Task MultipleAndConditions_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("status=eq.Active&category=eq.Electronics&price=gt.100");

        result.Should().NotBeEmpty();
        result.All(p => p.Status == "Active" && p.Category == "Electronics" && p.Price > 100).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-013: OR Group")]
    public async Task OrGroup_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("or=(category.eq.Electronics,category.eq.Books)");

        result.Should().NotBeEmpty();
        result.All(p => p.Category == "Electronics" || p.Category == "Books").Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-014: Complex OR with AND")]
    public async Task ComplexOrWithAnd_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("or=(status.eq.Active,status.eq.ComingSoon)&price=lt.200");

        result.Should().NotBeEmpty();
        result.All(p => (p.Status == "Active" || p.Status == "ComingSoon") && p.Price < 200).Should().BeTrue();
    }

    #endregion

    #region IN Operator

    [Fact(DisplayName = "TC-DUCK-015: IN Operator (Strings)")]
    public async Task InOperator_WithStrings_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("category=in.(Electronics,Books,Sports)");

        result.Should().NotBeEmpty();
        result.All(p => new[] { "Electronics", "Books", "Sports" }.Contains(p.Category)).Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-016: NOT IN Operator")]
    public async Task NotInOperator_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("status=not.in.(Discontinued,OutOfStock)");

        result.Should().NotBeEmpty();
        result.All(p => p.Status != "Discontinued" && p.Status != "OutOfStock").Should().BeTrue();
    }

    #endregion

    #region Sorting & Pagination

    [Fact(DisplayName = "TC-DUCK-017: Sort by Price (Ascending)")]
    public async Task SortAscending_ShouldOrderCorrectly()
    {
        var result = await ExecuteQuery("status=eq.Active&paging=(sort(price.asc),limit.20)");

        result.Should().HaveCount(20);
        result.Should().BeInAscendingOrder(p => p.Price);
    }

    [Fact(DisplayName = "TC-DUCK-018: Sort by Rating (Descending)")]
    public async Task SortDescending_ShouldOrderCorrectly()
    {
        var result = await ExecuteQuery("paging=(sort(rating.desc),limit.15)");

        result.Should().HaveCount(15);
        result.Should().BeInDescendingOrder(p => p.Rating);
    }

    [Fact(DisplayName = "TC-DUCK-019: Multi-column Sort")]
    public async Task MultiColumnSort_ShouldOrderCorrectly()
    {
        var result = await ExecuteQuery("paging=(sort(category.asc,price.desc),limit.25)");

        result.Should().HaveCount(25);
    }

    [Fact(DisplayName = "TC-DUCK-026: Keyset cursor - second page continues the first")]
    public async Task KeysetCursor_SecondPage_ShouldContinueFirst()
    {
        var expected = _fixture.Products
            .Where(p => p.Status == "Active")
            .OrderBy(p => p.Price).ThenBy(p => p.Id)
            .Skip(20).Take(20)
            .Select(p => p.Id)
            .ToList();

        var first = await ExecuteQuery("status=eq.Active&paging=(sort(price.asc,$key.asc),limit.20)");
        var last = first[^1];
        var price = last.Price.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var second = await ExecuteQuery($"status=eq.Active&paging=(sort(price.asc.{price},$key.{last.Id}),limit.20)");

        second.Select(p => p.Id).Should().Equal(expected);
    }

    #endregion

    #region Boolean and Null

    [Fact(DisplayName = "TC-DUCK-020: Boolean Filter")]
    public async Task BooleanFilter_ShouldFilterCorrectly()
    {
        var result = await ExecuteQuery("isFeatured=eq.true&paging=(limit.30)");

        result.Should().NotBeEmpty();
        result.All(p => p.IsFeatured).Should().BeTrue();
    }

    #endregion

    #region Complex Real-World Scenarios

    [Fact(DisplayName = "TC-DUCK-022: Budget Electronics")]
    public async Task ComplexQuery_BudgetElectronics()
    {
        var result = await ExecuteQuery("category=eq.Electronics&price=lt.200&status=eq.Active&stock=gt.0&paging=(sort(rating.desc),limit.20)");

        result.Should().NotBeEmpty();
        result.All(p => p.Category == "Electronics" && p.Price < 200 && p.Status == "Active" && p.Stock > 0).Should().BeTrue();
        result.Should().BeInDescendingOrder(p => p.Rating);
    }

    [Fact(DisplayName = "TC-DUCK-023: Premium Featured Products")]
    public async Task ComplexQuery_PremiumFeatured()
    {
        var result = await ExecuteQuery("isFeatured=eq.true&price=gt.500&rating=gte.4.0&status=eq.Active&paging=(sort(price.desc),limit.10)");

        result.Should().NotBeEmpty();
        result.All(p => p.IsFeatured && p.Price > 500 && p.Rating >= 4.0 && p.Status == "Active").Should().BeTrue();
    }

    [Fact(DisplayName = "TC-DUCK-024: Low Stock Alert")]
    public async Task ComplexQuery_LowStockAlert()
    {
        var result = await ExecuteQuery("stock=lt.50&status=eq.Active&or=(category.eq.Electronics,category.eq.Clothing)&paging=(sort(stock.asc),limit.30)");

        result.Should().NotBeEmpty();
        result.All(p => p.Stock < 50 && p.Status == "Active").Should().BeTrue();
        result.Should().BeInAscendingOrder(p => p.Stock);
    }

    [Fact(DisplayName = "TC-DUCK-025: New High-Rated Products")]
    public async Task ComplexQuery_NewHighRated()
    {
        var result = await ExecuteQuery("createdAt=ago.365d&rating=gte.4.5&status=eq.Active&paging=(sort(createdAt.desc,rating.desc),limit.15)");

        result.Should().NotBeEmpty();
        result.All(p => p.Rating >= 4.5 && p.Status == "Active").Should().BeTrue();
    }

    #endregion

    #region Same rows as LINQ over the seeded data

    public static TheoryData<string, Func<Product, bool>> ParityCases => new()
    {
        { "status=eq.active", p => p.Status.Equals("active", StringComparison.OrdinalIgnoreCase) },
        { "status=not.eq.Active", p => p.Status != "Active" },
        { "category=not.in.(Electronics,Books)", p => p.Category is not ("Electronics" or "Books") },
        { "tags=in.(premium,on-sale)", p => p.Tags is "premium" or "on-sale" },
        { "tags=not.in.(premium,on-sale)", p => p.Tags is not ("premium" or "on-sale") },
        { "name=cs.shoes", p => p.Name.Contains("shoes", StringComparison.OrdinalIgnoreCase) },
        { "name=not.cs.shoes", p => !p.Name.Contains("shoes", StringComparison.OrdinalIgnoreCase) },
        { "name=stw.ergonomic", p => p.Name.StartsWith("ergonomic", StringComparison.OrdinalIgnoreCase) },
        { "category=enw.ing", p => p.Category.EndsWith("ing", StringComparison.OrdinalIgnoreCase) },
        { "category=in.(Electronics,Books)", p => p.Category is "Electronics" or "Books" },
        { "stock=in.(0,1,2,3)", p => p.Stock is >= 0 and <= 3 },
        { "or=(category.eq.Toys,price.lt.20)", p => p.Category == "Toys" || p.Price < 20 },
        { "price=gte.100&price=lt.200&isFeatured=eq.true", p => p.Price >= 100 && p.Price < 200 && p.IsFeatured },
        { "brand=is.$empty", p => string.IsNullOrEmpty(p.Brand) },

        { "createdAt=gte.2025-06-01&createdAt=lt.2025-07-01", p => p.CreatedAt >= new DateTime(2025, 6, 1) && p.CreatedAt < new DateTime(2025, 7, 1) },
    };

    [Theory(DisplayName = "TC-DUCK-030: Same rows as LINQ")]
    [MemberData(nameof(ParityCases))]
    public async Task Filter_ShouldReturnTheSameRowsAsLinq(string query, Func<Product, bool> predicate)
    {
        var expected = _fixture.Products.Where(predicate).Select(p => p.Id).ToList();

        var result = await ExecuteQuery($"{query}&paging=(limit.100000)");
        var count = await _provider.GetCountAsync<Product>("Products", FilteredDataQuery.Create(query));

        expected.Should().NotBeEmpty();
        result.Select(p => p.Id).Should().BeEquivalentTo(expected);
        count.Should().Be(expected.Count);
    }

    #endregion

    #region DuckDB execution

    [Fact(DisplayName = "TC-DUCK-040: Command timeout interrupts the query")]
    public async Task CommandTimeout_ShouldInterruptTheQuery()
    {
        var services = new ServiceCollection();
        services.AddPagin8();
        services.AddPagin8BackendDuckDb(_fixture.CreateConnection, commandTimeout: 1);
        var provider = services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDuckDbFilterProvider>();

        // The filter on every joined row keeps DuckDB from shortcutting the count.
        const string slowView = "(SELECT p.* FROM Products p, range(100000000) r WHERE hash(r.range + p.Id) % 7 = 0) AS slow";
        var act = () => provider.GetCountAsync<Product>(slowView, FilteredDataQuery.Create("stock=gte.0"));

        await act.Should().ThrowAsync<TimeoutException>();
    }

    [Fact(DisplayName = "TC-DUCK-041: Parameters are rewritten outside literals and comments only")]
    public void TranslateParameters_ShouldRewriteMarkersOnly()
    {
        const string sql = "SELECT '@p9', \"@p8\" FROM t WHERE a = @p0 AND b = $p1 -- @p7\n AND c = @p2 /* @p6 */";

        var translated = DuckDbPagin8Command.TranslateParameters(sql, out var referenced);

        translated.Should().Be("SELECT '@p9', \"@p8\" FROM t WHERE a = $p0 AND b = $p1 -- @p7\n AND c = $p2 /* @p6 */");
        referenced.Should().BeEquivalentTo("p0", "p1", "p2");
    }

    [Fact(DisplayName = "TC-DUCK-042: Registration requires the PostgreSQL dialect")]
    public void Registration_ShouldRefuseSqlServerDialect()
    {
        var services = new ServiceCollection();
        services.AddPagin8Sql();

        var act = () => services.AddPagin8BackendDuckDb("DataSource=:memory:");

        act.Should().Throw<InvalidOperationException>().WithMessage("*DatabaseType.PostgreSql*");

        // Leave the process-wide runtime as the other tests in this class expect it.
        new ServiceCollection().AddPagin8();
    }

    #endregion

    private async Task<List<Product>> ExecuteQuery(string queryString)
    {
        var result = await _provider.GetAsync<Product>("Products", FilteredDataQuery.Create(queryString));
        var list = result.Data.ToList();
        _output.WriteLine($"Query: {queryString} -> {list.Count} rows");
        return list;
    }
}
