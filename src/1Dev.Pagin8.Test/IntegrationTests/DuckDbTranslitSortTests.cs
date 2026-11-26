using System.ComponentModel.DataAnnotations;
using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Extensions.Backend.Models;
using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal.Attributes;
using _1Dev.Pagin8.Internal.Configuration;
using AspNet.Transliterator;
using Attributes;
using Dapper;
using DuckDB.NET.Data;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace _1Dev.Pagin8.Test.IntegrationTests;

public class PlaceRow
{
    [Key]
    public int Id { get; set; }

    [NullsAllowed, UseTranslit]
    public string? Place { get; set; }

    public int Rank { get; set; }
}

public sealed class DuckDbTranslitSortFixture : IAsyncLifetime
{
    public static readonly IReadOnlyDictionary<int, string?> Rows = new Dictionary<int, string?>
    {
        [1] = "Zemun",
        [2] = "beograd",
        [3] = "Čačak",
        [4] = "CACAK",
        [5] = null,
        [6] = "",
        [7] = "Чачак",
        [8] = "Arilje",
        [9] = "Đakovica",
        [10] = "cacak",
        [11] = "Beograd",
    };

    public DuckDBConnection Connection { get; } = new("DataSource=:memory:");

    public async Task InitializeAsync()
    {
        await Connection.OpenAsync();
        await Connection.ExecuteAsync("CREATE TABLE places (id INTEGER PRIMARY KEY, place VARCHAR, place_translit VARCHAR, rank INTEGER)");
        foreach (var (id, place) in Rows)
            await Connection.ExecuteAsync("INSERT INTO places VALUES ($id, $place, $translit, $rank)",
                new { id, place, translit = Translit(place), rank = id % 3 });
    }

    public static string? Translit(string? value) => string.IsNullOrEmpty(value) ? value : Transliteration.ToLowerBoldLatin(value);

    public Task DisposeAsync()
    {
        Connection.Dispose();
        return Task.CompletedTask;
    }
}

/// <summary>
/// With <see cref="Extensions.Backend.Extensions.DuckDbProviderOptions.SortByTranslit"/>, a sort by a
/// [UseTranslit] property orders by its translit column, so case and diacritics do not split the
/// order, and pages through it with a cursor taken from the plain value the client shows, in any
/// script. Without it the plain column is sorted, binary, as before.
/// </summary>
[Collection("DuckDb")]
[Trait("Category", "Integration")]
[Trait("Database", "DuckDb")]
public class DuckDbTranslitSortTests(DuckDbTranslitSortFixture fixture) : IClassFixture<DuckDbTranslitSortFixture>
{
    private IDuckDbFilterProvider Provider(bool sortByTranslit)
    {
        var services = new ServiceCollection();
        services.AddPagin8(cfg => cfg.DatabaseType = DatabaseType.PostgreSql);
        services.AddPagin8BackendDuckDb(_ => fixture.Connection.Duplicate(), options => options.SortByTranslit = sortByTranslit);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDuckDbFilterProvider>();
    }

    // The order of the rows with a value; where NULL lands among '' is the engine's (NullKeysetTests),
    // so those two come first ascending and last descending, in an order not asserted here.
    private static List<int> Expected(Func<string?, string?> key, bool descending)
    {
        var rows = DuckDbTranslitSortFixture.Rows
            .Where(r => !string.IsNullOrEmpty(r.Value))
            .Select(r => (Id: r.Key, Key: key(r.Value)!));
        return (descending
                ? rows.OrderByDescending(r => r.Key, StringComparer.Ordinal).ThenByDescending(r => r.Id)
                : rows.OrderBy(r => r.Key, StringComparer.Ordinal).ThenBy(r => r.Id))
            .Select(r => r.Id)
            .ToList();
    }

    private static void AssertOrder(IReadOnlyList<PlaceRow> rows, Func<string?, string?> key, bool descending)
    {
        var blank = rows.Where(r => string.IsNullOrEmpty(r.Place)).Select(r => r.Id).ToList();
        blank.Should().BeEquivalentTo([5, 6]);
        var valued = descending ? rows.Take(rows.Count - 2) : rows.Skip(2);
        valued.Select(r => r.Id).Should().Equal(Expected(key, descending));
    }

    private static async Task<List<PlaceRow>> QueryAsync(IDuckDbFilterProvider provider, string sort, int limit) =>
        (await provider.GetAsync<PlaceRow>("places", FilteredDataQuery.Create($"paging=(sort({sort}),limit.{limit})"))).Data.ToList();

    [Theory]
    [InlineData("asc")]
    [InlineData("desc")]
    public async Task Sorts_by_the_translit_column(string direction)
    {
        var rows = await QueryAsync(Provider(sortByTranslit: true), $"place.{direction},id.{direction}", 100);

        AssertOrder(rows, DuckDbTranslitSortFixture.Translit, direction == "desc");
    }

    [Theory]
    [InlineData("asc", false)]
    [InlineData("desc", false)]
    [InlineData("asc", true)]
    [InlineData("desc", true)]
    public async Task Pages_by_the_translit_column_with_the_shown_value_as_cursor(string direction, bool cyrillicCursor)
    {
        var provider = Provider(sortByTranslit: true);
        var first = $"place.{direction},id.{direction}";
        var whole = (await QueryAsync(provider, first, 100)).Select(r => r.Id).ToList();
        var seen = new List<int>();
        var sort = first;

        for (var page = 0; page < 20; page++)
        {
            var rows = await QueryAsync(provider, sort, 2);
            if (rows.Count == 0) break;

            seen.AddRange(rows.Select(r => r.Id));
            var last = rows[^1];
            var shown = last.Place switch
            {
                null => "$null",
                "" => "$empty",
                var p => Uri.EscapeDataString(cyrillicCursor ? Transliteration.LatinToCyrl(p) : p)
            };
            sort = $"place.{direction}.{shown},id.{direction}.{last.Id},$key.{last.Id}";
        }

        seen.Should().Equal(whole);
    }

    [Fact]
    public async Task Sorts_the_plain_column_when_off()
    {
        var rows = await QueryAsync(Provider(sortByTranslit: false), "place.asc,id.asc", 100);

        AssertOrder(rows, v => v, descending: false);
    }

    [Fact]
    public async Task A_sort_without_a_translit_property_is_the_usual_one()
    {
        var rows = await QueryAsync(Provider(sortByTranslit: true), "rank.desc,id.asc", 100);

        rows.Select(r => r.Id).Should().Equal(DuckDbTranslitSortFixture.Rows.Keys.OrderByDescending(id => id % 3).ThenBy(id => id));
    }
}
