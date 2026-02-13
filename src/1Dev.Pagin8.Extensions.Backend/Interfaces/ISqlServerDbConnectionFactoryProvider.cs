namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

/// <summary>
/// Provider that stores named SQL Server connection factories.
/// </summary>
public interface ISqlServerDbConnectionFactoryProvider
{
    void Add(string name, ISqlServerDbConnectionFactory factory);
    ISqlServerDbConnectionFactory? Get(string name);
}
