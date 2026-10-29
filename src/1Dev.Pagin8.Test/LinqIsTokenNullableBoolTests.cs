using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Metadata;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Visitors;
using _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;

namespace _1Dev.Pagin8.Test;

/// <summary>
/// LINQ mode: `is.true` / `is.false` on nullable bool properties must not throw and must match SQL `IS [NOT] TRUE|FALSE` semantics.
/// </summary>
public class LinqIsTokenNullableBoolTests : Pagin8TestBase
{
    [Theory]
    [InlineData("true", false, new[] { 1 })]
    [InlineData("false", false, new[] { 2 })]
    [InlineData("true", true, new[] { 2, 3 })]
    [InlineData("false", true, new[] { 1, 3 })]
    public void LinqVisitor_IsToken_NullableBool_FiltersLikeSql(string value, bool isNegated, int[] expectedIds)
    {
        PostgreSqlTestBootstrap.Init();
        var visitor = CreateLinqVisitor<NullableBoolEntity>();

        var token = new IsToken(nameof(NullableBoolEntity.Flag), value, 1, isNegated);

        var result = visitor.Visit(token, CreateData()).ToList();

        result.Select(e => e.Id).Should().BeEquivalentTo(expectedIds);
    }

    [Theory]
    [InlineData("true", false, new[] { 1 })]
    [InlineData("false", false, new[] { 2, 3 })]
    public void LinqVisitor_IsToken_Bool_StillWorks(string value, bool isNegated, int[] expectedIds)
    {
        PostgreSqlTestBootstrap.Init();
        var visitor = CreateLinqVisitor<NullableBoolEntity>();

        var token = new IsToken(nameof(NullableBoolEntity.Active), value, 1, isNegated);

        var result = visitor.Visit(token, CreateData()).ToList();

        result.Select(e => e.Id).Should().BeEquivalentTo(expectedIds);
    }

    [Theory]
    [InlineData("false", false, new[] { 2 })]
    [InlineData("false", true, new[] { 1, 3 })]
    public void LinqVisitor_NestedIsToken_NullableBool_FiltersLikeSql(string value, bool isNegated, int[] expectedIds)
    {
        PostgreSqlTestBootstrap.Init();
        var visitor = CreateLinqVisitor<NullableBoolParent>();

        var data = CreateData()
            .Select(child => new NullableBoolParent { Id = child.Id, Child = child })
            .ToList()
            .AsQueryable();

        var childToken = new IsToken(nameof(NullableBoolEntity.Flag), value, 2, isNegated);
        var nestedToken = new NestedFilterToken(nameof(NullableBoolParent.Child), [childToken]);

        var result = visitor.Visit(nestedToken, data).ToList();

        result.Select(e => e.Id).Should().BeEquivalentTo(expectedIds);
    }

    private static IQueryable<NullableBoolEntity> CreateData() => new List<NullableBoolEntity>
    {
        new() { Id = 1, Flag = true, Active = true },
        new() { Id = 2, Flag = false, Active = false },
        new() { Id = 3, Flag = null, Active = false },
    }.AsQueryable();

    private static LinqTokenVisitor<T> CreateLinqVisitor<T>() where T : class
    {
        var metadataProvider = new Pagin8MetadataProvider(new MetadataProvider());
        var dateProcessor = new DateProcessor();
        return new LinqTokenVisitor<T>(metadataProvider, dateProcessor);
    }

    public class NullableBoolEntity
    {
        [Key]
        public int Id { get; set; }
        public bool? Flag { get; set; }
        public bool Active { get; set; }
    }

    public class NullableBoolParent
    {
        [Key]
        public int Id { get; set; }
        public NullableBoolEntity? Child { get; set; }
    }
}
