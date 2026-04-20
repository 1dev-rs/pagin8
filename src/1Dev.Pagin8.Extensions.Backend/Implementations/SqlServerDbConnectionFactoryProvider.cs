using System;
using System.Collections.Concurrent;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using _1Dev.Pagin8.Extensions.Backend.Implementations;
using _1Dev.Pagin8;
using _1Dev.Pagin8.Internal;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Visitors;
using Microsoft.Extensions.Logging;

namespace _1Dev.Pagin8.Extensions.Backend
{
    internal static class SqlServerConstants
    {
        public const string DefaultProviderName = "default";
    }

    /// <summary>
    /// Provider that stores named SQL Server connection factories.
    /// </summary>
    public class SqlServerDbConnectionFactoryProvider : ISqlServerDbConnectionFactoryProvider
    {
        private readonly ConcurrentDictionary<string, ISqlServerDbConnectionFactory> _map = new();

        public void Add(string name, ISqlServerDbConnectionFactory factory)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentNullException(nameof(name));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            _map[name] = factory;
        }

        public ISqlServerDbConnectionFactory? Get(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _map.TryGetValue(name, out var f) ? f : null;
        }
    }

    /// <summary>
    /// Factory that creates SQL Server filter providers for a given named connection.
    /// Instances are cached per named provider because the visitor, builder, and filter provider
    /// are all stateless for a fixed connection factory.
    /// </summary>
    public class SqlServerFilterProviderFactory : ISqlServerFilterProviderFactory
    {
        private readonly IServiceProvider _sp;
        private readonly ISqlServerDbConnectionFactoryProvider _provider;
        private readonly ConcurrentDictionary<string, ISqlServerFilterProvider> _cache = new();

        public SqlServerFilterProviderFactory(IServiceProvider sp, ISqlServerDbConnectionFactoryProvider provider)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public ISqlServerFilterProvider Create(string name) =>
            _cache.GetOrAdd(name, BuildProvider);

        private ISqlServerFilterProvider BuildProvider(string name)
        {
            var factory = _provider.Get(name);
            if (factory == null)
                throw new InvalidOperationException($"SQL Server provider with name '{name}' was not registered.");

            // Build SqlServerTokenVisitor + SqlServerSqlQueryBuilder directly so that no DI
            // misconfiguration (e.g. the global ISqlQueryBuilder being PostgreSQL) can leak
            // the NpgsqlTokenVisitor into SQL Server queries.
            var tokenizationService = _sp.GetRequiredService<ITokenizationService>();
            var metadata            = _sp.GetRequiredService<IPagin8MetadataProvider>();
            var dateProcessor       = _sp.GetRequiredService<IDateProcessor>();

            var sqlServerVisitor  = new SqlServerTokenVisitor(metadata, dateProcessor);
            var innerBuilder     = new SqlServerSqlQueryBuilder(tokenizationService, sqlServerVisitor);
            var loggerFactory    = _sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
            var sqlServerBuilder = new LoggingSqlServerSqlQueryBuilder(innerBuilder, loggerFactory);
            var debugLogger = loggerFactory.CreateLogger("Pagin8");
            debugLogger.LogWarning("[PAGIN8-DEBUG] BuildProvider '{Name}': LoggingSqlServerSqlQueryBuilder created, Trace enabled: {TraceEnabled}", name, debugLogger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Trace));

            // Read Command Timeout directly from the factory — no connection string parsing needed.
            // Set it via the commandTimeout parameter when registering with AddPagin8BackendSqlServer().
            return new SqlServerFilterProvider(factory, sqlServerBuilder, factory.CommandTimeout);
        }
    }
}
