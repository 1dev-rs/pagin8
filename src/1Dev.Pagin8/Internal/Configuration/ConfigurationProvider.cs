using System.Reflection;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Utils;
using Microsoft.Extensions.Configuration;

namespace _1Dev.Pagin8.Internal.Configuration;

public static class ConfigurationProvider
{
    private const string ConfigPath = "Internal/Configuration/pagin8Settings.json";

    private static readonly string[] UserConfigurableProperties = ["DatabaseType", "MaxNestingLevel", "PagingSettings"];

    static ConfigurationProvider()
    {
        Config = ReadSettings();
    }

    private static ConfigurationSettings ReadSettings()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(ConfigPath, optional: false, reloadOnChange: true)
            .Build();

        var cfg = new ConfigurationSettings();
        configuration.Bind(cfg);

        return cfg;
    }

    public static void LoadSettings(ServiceConfiguration config)
    {
        Guard.AgainstNull(Config);

        UpdateProperties(Config, config);
    }

    public static ConfigurationSettings Config { get; }

    #region Private methods
    private static void UpdateProperties(object targetConfig, object sourceConfig)
    {
        foreach (var property in sourceConfig.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!UserConfigurableProperties.Contains(property.Name))
                continue;

            var sourceValue = property.GetValue(sourceConfig);

            if (sourceValue == null || !IsValueValid(sourceValue))
                continue;

            var targetProperty = targetConfig.GetType().GetProperty(property.Name);

            if (targetProperty != null && targetProperty.CanWrite)
            {
                targetProperty.SetValue(targetConfig, sourceValue);
            }
        }
    }

    private static bool IsValueValid(object value)
    {
        if (value is < 0)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_PropertyValueMustBePositive.Code);
        }

        return true;
    }
    #endregion
}



