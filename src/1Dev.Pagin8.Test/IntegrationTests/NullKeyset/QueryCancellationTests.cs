using System.Diagnostics;
using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Extensions.Backend.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace _1Dev.Pagin8.Test.IntegrationTests.NullKeyset;

/// <summary>
/// Cancelling the token of a running query stops it on the database and throws
/// OperationCanceledException for that token, the same on every provider: Npgsql and SqlClient
/// cancel the statement on the server, DuckDB interrupts it.
/// </summary>
public abstract class QueryCancellationTestsBase
{
    // Six copies of the 60 rows, 4.7e10 rows to sort for the first page: no engine finishes it
    // within a test, so only the cancellation can end it.
    private static readonly string EndlessView =
        $"(SELECT a.* FROM {NullRowData.Table} a CROSS JOIN {NullRowData.Table} b CROSS JOIN {NullRowData.Table} c " +
        $"CROSS JOIN {NullRowData.Table} d CROSS JOIN {NullRowData.Table} e CROSS JOIN {NullRowData.Table} f) main";

    private static readonly TimeSpan CancelAfter = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan StoppedWithin = TimeSpan.FromSeconds(15);

    protected abstract IFilterProvider CreateProvider();

    [Fact]
    public async Task Cancelling_a_running_page_query_stops_it()
    {
        var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource(CancelAfter);
        var watch = Stopwatch.StartNew();

        var act = () => provider.GetAsync<NullRow>(EndlessView, FilteredDataQuery.Create("paging=(sort(label.asc,id.asc),limit.5)"), cancellation.Token, commandTimeout: 300);

        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.CancellationToken.Should().Be(cancellation.Token);
        watch.Elapsed.Should().BeLessThan(StoppedWithin);
    }

    [Fact]
    public async Task Cancelling_a_running_count_stops_it()
    {
        var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource(CancelAfter);
        var watch = Stopwatch.StartNew();

        var act = () => provider.GetCountAsync<NullRow>(EndlessView, FilteredDataQuery.Create("label=not.eq.zzz"), cancellation.Token, commandTimeout: 300);

        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.CancellationToken.Should().Be(cancellation.Token);
        watch.Elapsed.Should().BeLessThan(StoppedWithin);
    }

    [Fact]
    public async Task A_cancelled_token_runs_no_query()
    {
        var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var act = () => provider.GetAsync<NullRow>(EndlessView, FilteredDataQuery.Create(""), cancellation.Token);

        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.CancellationToken.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task A_token_that_is_not_cancelled_changes_nothing()
    {
        var provider = CreateProvider();
        using var cancellation = new CancellationTokenSource();
        var query = FilteredDataQuery.Create("amount=gt.0&paging=(sort(id.asc),limit.100)");

        var withToken = await provider.GetAsync<NullRow>(NullRowData.Table, query, cancellation.Token);
        var without = await provider.GetAsync<NullRow>(NullRowData.Table, query);
        var count = await provider.GetCountAsync<NullRow>(NullRowData.Table, query, cancellation.Token);

        withToken.Data.Select(r => r.Id).Should().Equal(without.Data.Select(r => r.Id));
        count.Should().Be(NullRowData.Rows.Count(r => r.Amount > 0));
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "PostgreSql")]
public class QueryCancellationPostgreSqlTests(NullKeysetPostgreSqlFixture fixture) : QueryCancellationTestsBase, IClassFixture<NullKeysetPostgreSqlFixture>
{
    protected override IFilterProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddPagin8();
        services.AddPagin8Backend(fixture.ConnectionString);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IFilterProvider>();
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "SqlServer")]
public class QueryCancellationSqlServerTests(NullKeysetSqlServerFixture fixture) : QueryCancellationTestsBase, IClassFixture<NullKeysetSqlServerFixture>
{
    protected override IFilterProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddPagin8Sql();
        services.AddPagin8BackendSqlServer(fixture.ConnectionString);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<ISqlServerFilterProvider>();
    }
}

[Collection("NullKeyset")]
[Trait("Category", "Integration")]
[Trait("Database", "DuckDb")]
public class QueryCancellationDuckDbTests(NullKeysetDuckDbFixture fixture) : QueryCancellationTestsBase, IClassFixture<NullKeysetDuckDbFixture>
{
    protected override IFilterProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddPagin8();
        services.AddPagin8BackendDuckDb(_ => fixture.CreateConnection(), options => options.CommandTimeout = 300);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IDuckDbFilterProvider>();
    }
}
