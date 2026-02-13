using System;
using System.Collections.Concurrent;
using _1Dev.Pagin8.Extensions.Backend.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using _1Dev.Pagin8.Extensions.Backend.Implementations;
using _1Dev.Pagin8;

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
    /// </summary>
    public class SqlServerFilterProviderFactory : ISqlServerFilterProviderFactory
    {
        private readonly IServiceProvider _sp;
        private readonly ISqlServerDbConnectionFactoryProvider _provider;

        public SqlServerFilterProviderFactory(IServiceProvider sp, ISqlServerDbConnectionFactoryProvider provider)
        {
            _sp = sp ?? throw new ArgumentNullException(nameof(sp));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public ISqlServerFilterProvider Create(string name)
        {
            var factory = _provider.Get(name) ?? _provider.Get(SqlServerConstants.DefaultProviderName);
            if (factory == null)
                throw new InvalidOperationException($"SQL Server provider with name '{name}' was not registered.");

            var sqlQueryBuilder = _sp.GetRequiredService<ISqlServerSqlQueryBuilder>();

            return new SqlServerFilterProvider(factory, sqlQueryBuilder);
        }
    }
}
