using FluentAssertions;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Strategy;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;

namespace _1Dev.Pagin8.Test;

/// <summary>
/// Tests for issue #10 (LINQ NestedFilterToken) and issue #11 (JSON-path IN tokenization).
/// </summary>
public class LinqNestedFilterAndJsonPathInTests : Pagin8TestBase
{
    // -----------------------------------------------------------------------
    // #11 – InTokenizationStrategy.Tokenize(query, jsonPath)
    // -----------------------------------------------------------------------

    [Fact]
    public void InTokenStrategy_Tokenize_WithJsonPath_SetsJsonPathOnToken()
    {
        var strategy = new InTokenizationStrategy();
        var jsonPath = "tags";

        var tokens = strategy.Tokenize("name=in.(a,b,c)", jsonPath);

        tokens.Should().HaveCount(1);
        var token = tokens[0].Should().BeOfType<InToken>().Subject;
        token.JsonPath.Should().Be(jsonPath);
        token.Field.Should().Be("name");
    }

    [Fact]
    public void InTokenStrategy_Tokenize_WithJsonPath_NegatedToken_SetsJsonPath()
    {
        var strategy = new InTokenizationStrategy();
        var jsonPath = "items";

        var tokens = strategy.Tokenize("status=not.in.(active,pending)", jsonPath);

        tokens.Should().HaveCount(1);
        var token = tokens[0].Should().BeOfType<InToken>().Subject;
        token.JsonPath.Should().Be(jsonPath);
        token.IsNegated.Should().BeTrue();
    }

    [Fact]
    public void IsTokenStrategy_Tokenize_WithJsonPath_SetsJsonPathOnToken()
    {
        var strategy = new IsTokenizationStrategy();
        var jsonPath = "details";

        var tokens = strategy.Tokenize("active=is.true", jsonPath);

        tokens.Should().HaveCount(1);
        var token = tokens[0].Should().BeOfType<IsToken>().Subject;
        token.JsonPath.Should().Be(jsonPath);
    }

    // -----------------------------------------------------------------------
    // #10 – LinqTokenVisitor.Visit(NestedFilterToken) – regular nested object
    // -----------------------------------------------------------------------

    [Fact]
    public void LinqVisitor_NestedFilterToken_RegularObject_FiltersByNestedProperty()
    {
        PostgreSqlTestBootstrap.Init();
        var visitor = CreateLinqVisitor<TestNestedEntity>();

        var data = new List<TestNestedEntity>
        {
            new() { Id = 1, TestEntity = new TestEntity { Id = 10, Status = "active", Name = "Alpha" } },
            new() { Id = 2, TestEntity = new TestEntity { Id = 20, Status = "inactive", Name = "Beta" } },
            new() { Id = 3, TestEntity = new TestEntity { Id = 30, Status = "active", Name = "Gamma" } },
        }.AsQueryable();

        var childToken = new ComparisonToken("Status", ComparisonOperator.Equals, "active", 2);
        var nestedToken = new NestedFilterToken("TestEntity", [childToken]);

        var result = visitor.Visit(nestedToken, data).ToList();

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(e => e.TestEntity!.Status.Should().Be("active"));
    }

    [Fact]
    public void LinqVisitor_NestedFilterToken_RegularObject_MultipleChildren_CombinesWithAnd()
    {
        PostgreSqlTestBootstrap.Init();
        var visitor = CreateLinqVisitor<TestNestedEntity>();

        var data = new List<TestNestedEntity>
        {
            new() { Id = 1, TestEntity = new TestEntity { Id = 10, Status = "active", Name = "Alpha" } },
            new() { Id = 2, TestEntity = new TestEntity { Id = 20, Status = "active", Name = "Beta" } },
            new() { Id = 3, TestEntity = new TestEntity { Id = 30, Status = "inactive", Name = "Alpha" } },
        }.AsQueryable();

        var statusToken = new ComparisonToken("Status", ComparisonOperator.Equals, "active", 2);
        var nameToken = new ComparisonToken("Name", ComparisonOperator.Equals, "Alpha", 2);
        var nestedToken = new NestedFilterToken("TestEntity", [statusToken, nameToken]);

        var result = visitor.Visit(nestedToken, data).ToList();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(1);
    }

    [Fact]
    public void LinqVisitor_NestedFilterToken_Collection_UsesAnySemantics()
    {
        PostgreSqlTestBootstrap.Init();
        var visitor = CreateLinqVisitor<TestJsonArrayEntity>();

        var data = new List<TestJsonArrayEntity>
        {
            new()
            {
                Id = 1,
                TariffAmounts = [new TestTariffAmount { TariffNumber = 1, Name = "Alpha", Amount = 100 }]
            },
            new()
            {
                Id = 2,
                TariffAmounts = [new TestTariffAmount { TariffNumber = 2, Name = "Beta", Amount = 200 }]
            },
            new()
            {
                Id = 3,
                TariffAmounts =
                [
                    new TestTariffAmount { TariffNumber = 3, Name = "Alpha", Amount = 300 },
                    new TestTariffAmount { TariffNumber = 4, Name = "Gamma", Amount = 400 }
                ]
            },
        }.AsQueryable();

        var nameToken = new ComparisonToken("Name", ComparisonOperator.Equals, "Alpha", 2);
        var nestedToken = new NestedFilterToken("TariffAmounts", [nameToken]);

        var result = visitor.Visit(nestedToken, data).ToList();

        result.Should().HaveCount(2);
        result.Select(e => e.Id).Should().BeEquivalentTo([1, 3]);
    }

    [Fact]
    public void LinqVisitor_NestedFilterToken_Collection_InToken_FiltersCorrectly()
    {
        PostgreSqlTestBootstrap.Init();
        var visitor = CreateLinqVisitor<TestJsonArrayEntity>();

        var data = new List<TestJsonArrayEntity>
        {
            new()
            {
                Id = 1,
                TariffAmounts = [new TestTariffAmount { TariffNumber = 1, Name = "A", Amount = 10 }]
            },
            new()
            {
                Id = 2,
                TariffAmounts = [new TestTariffAmount { TariffNumber = 2, Name = "B", Amount = 20 }]
            },
            new()
            {
                Id = 3,
                TariffAmounts = [new TestTariffAmount { TariffNumber = 3, Name = "C", Amount = 30 }]
            },
        }.AsQueryable();

        var inToken = new InToken("TariffNumber", "(1,3)", 2);
        var nestedToken = new NestedFilterToken("TariffAmounts", [inToken]);

        var result = visitor.Visit(nestedToken, data).ToList();

        result.Should().HaveCount(2);
        result.Select(e => e.Id).Should().BeEquivalentTo([1, 3]);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static LinqTokenVisitor<T> CreateLinqVisitor<T>() where T : class
    {
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor = new DateProcessor();
        return new LinqTokenVisitor<T>(metadataProvider, dateProcessor);
    }
}
