using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Extensions;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Validators.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Data;

namespace _1Dev.Pagin8.Test;

public class Pagin8DiSetupTests
{
    [Fact]
    public void AddPagin8_Should_Resolve_All_Core_Services()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddPagin8(options =>
        {
            options.DatabaseType = DatabaseType.PostgreSql;
            options.MaxNestingLevel = 5;
            options.PagingSettings = new PagingSettings
            {
                DefaultPerPage = 50,
                MaxItemsPerPage = 5000,
                MaxSafeItemCount = 1000000
            };
        });

        var provider = services.BuildServiceProvider();

        // Act & Assert
        provider.GetRequiredService<ITokenizer>();
        provider.GetRequiredService<IDateProcessor>();
        provider.GetRequiredService<ITokenizationService>();
        provider.GetRequiredService<IContextValidator>();
        provider.GetRequiredService<IPagin8MetadataProvider>();
        provider.GetRequiredService<IMetadataProvider>();
        provider.GetRequiredService<ISqlQueryBuilder>();
        provider.GetRequiredService<ISqlTokenVisitor>();

        // Example of generic
        var linqVisitor = typeof(ILinqTokenVisitor<>).MakeGenericType(typeof(object));
        var queryableProcessor = typeof(IQueryableTokenProcessor<>).MakeGenericType(typeof(object));

        Assert.NotNull(provider.GetRequiredService(linqVisitor));
        Assert.NotNull(provider.GetRequiredService(queryableProcessor));
    }

    [Fact]
    public void AddPagin8BackendSqlServer_WithConnectionString_Should_Resolve_DefaultSqlServerFilterProvider()
    {
        var provider = BuildServices(services =>
        {
            services.AddPagin8();
            services.AddPagin8BackendSqlServer("Server=.;Database=Pagin8;Trusted_Connection=True;");
        });

        using var scope = provider.CreateScope();

        var filterProvider = scope.ServiceProvider.GetRequiredService<ISqlServerFilterProvider>();
        var factoryProvider = scope.ServiceProvider.GetRequiredService<ISqlServerDbConnectionFactoryProvider>();

        Assert.NotNull(filterProvider);
        Assert.NotNull(factoryProvider.Get("default"));
    }

    [Fact]
    public void AddPagin8BackendSqlServer_WithNamedFactory_Should_Resolve_NamedProvider_WithoutCircularDependency()
    {
        var provider = BuildServices(services =>
        {
            services.AddPagin8();
            services.AddPagin8BackendSqlServer("archive", () => new FakeSqlServerDbConnectionFactory());
        });

        using var scope = provider.CreateScope();

        var filterProviderFactory = scope.ServiceProvider.GetRequiredService<ISqlServerFilterProviderFactory>();
        var namedProvider = filterProviderFactory.Create("archive");

        Assert.NotNull(namedProvider);
    }

    [Fact]
    public void AddPagin8BackendSqlServer_GenericOverload_Should_Register_DefaultProviderMapping()
    {
        var provider = BuildServices(services =>
        {
            services.AddPagin8();
            services.AddPagin8BackendSqlServer<FakeSqlServerDbConnectionFactory>();
        });

        using var scope = provider.CreateScope();

        var filterProvider = scope.ServiceProvider.GetRequiredService<ISqlServerFilterProvider>();
        var factoryProvider = scope.ServiceProvider.GetRequiredService<ISqlServerDbConnectionFactoryProvider>();

        Assert.NotNull(filterProvider);
        Assert.IsType<FakeSqlServerDbConnectionFactory>(factoryProvider.Get("default"));
    }

    [Fact]
    public void SqlServerFilterProviderFactory_Should_Throw_WhenNamedProviderDoesNotExist()
    {
        var provider = BuildServices(services =>
        {
            services.AddPagin8();
            services.AddPagin8BackendSqlServer("default", () => new FakeSqlServerDbConnectionFactory());
        });

        using var scope = provider.CreateScope();
        var filterProviderFactory = scope.ServiceProvider.GetRequiredService<ISqlServerFilterProviderFactory>();

        var action = () => filterProviderFactory.Create("missing");

        var ex = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("missing", ex.Message);
    }

    private static ServiceProvider BuildServices(Action<ServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    private sealed class FakeSqlServerDbConnectionFactory : ISqlServerDbConnectionFactory
    {
        public int? CommandTimeout => 15;

        public IDbConnection Create() => throw new NotSupportedException();
    }
}