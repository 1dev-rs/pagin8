namespace _1Dev.Pagin8.Extensions.Backend.Interfaces;

public interface ISqlServerFilterProviderFactory
{
    ISqlServerFilterProvider Create(string name);
}
