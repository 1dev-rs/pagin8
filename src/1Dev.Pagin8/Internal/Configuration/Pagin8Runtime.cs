using _1Dev.Pagin8.Internal.Configuration;

namespace Internal.Configuration;

public static class Pagin8Runtime
{
    private static ServiceConfiguration? _config;

    public static void Initialize(ServiceConfiguration config)
    {
        _config = config;
    }

    public static ServiceConfiguration Config =>
        _config ?? throw new InvalidOperationException("Pagin8 not initialized. Call AddPagin8() in your DI setup.");
}