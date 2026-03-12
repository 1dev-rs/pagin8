using System.Text.Json;
using System.Text.Json.Serialization;

namespace _1Dev.Pagin8.Test.IntegrationTests.Configuration;

/// <summary>
/// Configuration reader for integration tests
/// Reads from test-config.json with fallback to environment variables
/// </summary>
public class TestConfiguration
{
    private static TestConfiguration? _instance;
    private static readonly object _lock = new();

    [JsonPropertyName("testConfiguration")]
    public TestSettings TestSettings { get; set; } = new();

    /// <summary>
    /// Get singleton instance of test configuration
    /// Priority: Environment Variables > test-config.json > Defaults
    /// </summary>
    public static TestConfiguration Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= LoadConfiguration();
                }
            }
            return _instance;
        }
    }

    private static TestConfiguration LoadConfiguration()
    {
        var config = LoadFromFile() ?? new TestConfiguration();
        
        // Override with environment variables (highest priority)
        config.ApplyEnvironmentVariables();
        
        return config;
    }

    private static TestConfiguration? LoadFromFile()
    {
        try
        {
            // Look for test-config.json in multiple locations
            var possiblePaths = new[]
            {
                "test-config.json",
                "IntegrationTests/test-config.json",
                "../IntegrationTests/test-config.json",
                "../../IntegrationTests/test-config.json",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-config.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "IntegrationTests", "test-config.json")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Console.WriteLine($"?? Loading configuration from: {Path.GetFullPath(path)}");
                    var json = File.ReadAllText(path);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip,
                        AllowTrailingCommas = true
                    };
                    return JsonSerializer.Deserialize<TestConfiguration>(json, options);
                }
            }

            Console.WriteLine("??  test-config.json not found, using defaults and environment variables");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"??  Error loading test-config.json: {ex.Message}");
            return null;
        }
    }

    private void ApplyEnvironmentVariables()
    {
        // Dataset size
        var datasetSizeStr = Environment.GetEnvironmentVariable("PAGIN8_TEST_DATASET_SIZE");
        if (!string.IsNullOrEmpty(datasetSizeStr) && int.TryParse(datasetSizeStr, out var datasetSize))
        {
            Console.WriteLine($"?? Environment override: Dataset size = {datasetSize:N0}");
            TestSettings.DatasetSize = datasetSize;
        }

        // Seed
        var seedStr = Environment.GetEnvironmentVariable("PAGIN8_TEST_SEED");
        if (!string.IsNullOrEmpty(seedStr) && int.TryParse(seedStr, out var seed))
        {
            Console.WriteLine($"?? Environment override: Seed = {seed}");
            TestSettings.Seed = seed;
        }

        // Performance preset
        var preset = Environment.GetEnvironmentVariable("PAGIN8_TEST_PRESET");
        if (!string.IsNullOrEmpty(preset) && TestSettings.Performance.Presets.TryGetValue(preset, out var presetConfig))
        {
            Console.WriteLine($"?? Environment override: Using preset '{preset}' ({presetConfig.DatasetSize:N0} records)");
            TestSettings.DatasetSize = presetConfig.DatasetSize;
        }
    }

    /// <summary>
    /// Reset singleton instance (useful for testing)
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _instance = null;
        }
    }
}

public class TestSettings
{
    [JsonPropertyName("datasetSize")]
    public int DatasetSize { get; set; } = 5000;

    [JsonPropertyName("seed")]
    public int Seed { get; set; } = 42;

    [JsonPropertyName("enablePerformanceMetrics")]
    public bool EnablePerformanceMetrics { get; set; } = true;

    [JsonPropertyName("databases")]
    public DatabaseSettings Databases { get; set; } = new();

    [JsonPropertyName("performance")]
    public PerformanceSettings Performance { get; set; } = new();

    [JsonPropertyName("docker")]
    public DockerSettings Docker { get; set; } = new();
}

public class DatabaseSettings
{
    [JsonPropertyName("sqlServer")]
    public SqlServerSettings SqlServer { get; set; } = new();

    [JsonPropertyName("postgreSql")]
    public PostgreSqlSettings PostgreSql { get; set; } = new();
}

public class SqlServerSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("image")]
    public string Image { get; set; } = "mcr.microsoft.com/mssql/server:2022-latest";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "YourStrong@Passw0rd";
}

public class PostgreSqlSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("image")]
    public string Image { get; set; } = "postgres:16-alpine";

    [JsonPropertyName("database")]
    public string Database { get; set; } = "testdb";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "postgres";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "postgres";
}

public class PerformanceSettings
{
    [JsonPropertyName("presets")]
    public Dictionary<string, PerformancePreset> Presets { get; set; } = new()
    {
        ["quick"] = new() { DatasetSize = 5000, Description = "Quick validation (20-30s)" },
        ["standard"] = new() { DatasetSize = 10000, Description = "Standard testing (30-40s)" },
        ["realistic"] = new() { DatasetSize = 50000, Description = "Realistic production load (60-90s)" },
        ["stress"] = new() { DatasetSize = 100000, Description = "Stress testing (2-3m)" },
        ["extreme"] = new() { DatasetSize = 500000, Description = "Extreme performance testing (5-10m)" }
    };

    [JsonPropertyName("thresholds")]
    public PerformanceThresholds Thresholds { get; set; } = new();
}

public class PerformancePreset
{
    [JsonPropertyName("datasetSize")]
    public int DatasetSize { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class PerformanceThresholds
{
    [JsonPropertyName("excellentMs")]
    public int ExcellentMs { get; set; } = 100;

    [JsonPropertyName("goodMs")]
    public int GoodMs { get; set; } = 500;

    [JsonPropertyName("acceptableMs")]
    public int AcceptableMs { get; set; } = 1000;
}

public class DockerSettings
{
    [JsonPropertyName("cleanupOnExit")]
    public bool CleanupOnExit { get; set; } = true;

    [JsonPropertyName("reuseContainers")]
    public bool ReuseContainers { get; set; } = false;
}
