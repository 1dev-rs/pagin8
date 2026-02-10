# ?? Configuration Guide (test-config.json)

## ?? Overview

Pagin8 integration tests now support **configuration files** for better management of test settings. This is more maintainable, version-control friendly, and easier for teams to manage.

---

## ?? Configuration File

### Location

```
1Dev.Pagin8.Test/IntegrationTests/test-config.json
```

### Schema

JSON schema with IntelliSense support:
```
1Dev.Pagin8.Test/IntegrationTests/test-config.schema.json
```

---

## ?? Configuration Priority

Settings are applied in this order (highest to lowest priority):

1. **Environment Variables** (highest priority)
   - `PAGIN8_TEST_DATASET_SIZE`
   - `PAGIN8_TEST_SEED`
   - `PAGIN8_TEST_PRESET`

2. **test-config.json** (middle priority)
   - All settings from configuration file

3. **Code Defaults** (lowest priority)
   - Hardcoded fallback values

---

## ?? Configuration Options

### Complete Example

```json
{
  "$schema": "./test-config.schema.json",
  "testConfiguration": {
    "datasetSize": 5000,
    "seed": 42,
    "enablePerformanceMetrics": true,
    "databases": {
      "sqlServer": {
        "enabled": true,
        "image": "mcr.microsoft.com/mssql/server:2022-latest",
        "password": "YourStrong@Passw0rd"
      },
      "postgreSql": {
        "enabled": true,
        "image": "postgres:16-alpine",
        "database": "testdb",
        "username": "postgres",
        "password": "postgres"
      }
    },
    "performance": {
      "presets": {
        "quick": {
          "datasetSize": 5000,
          "description": "Quick validation (20-30s)"
        },
        "realistic": {
          "datasetSize": 50000,
          "description": "Realistic production load (60-90s)"
        },
        "stress": {
          "datasetSize": 100000,
          "description": "Stress testing (2-3m)"
        }
      },
      "thresholds": {
        "excellentMs": 100,
        "goodMs": 500,
        "acceptableMs": 1000
      }
    },
    "docker": {
      "cleanupOnExit": true,
      "reuseContainers": false
    }
  }
}
```

### Settings Explained

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `datasetSize` | int | 5000 | Number of products to generate |
| `seed` | int | 42 | Random seed for reproducible data |
| `enablePerformanceMetrics` | bool | true | Show performance metrics in output |
| `databases.sqlServer.enabled` | bool | true | Enable SQL Server tests |
| `databases.sqlServer.image` | string | `mssql/server:2022-latest` | Docker image for SQL Server |
| `databases.sqlServer.password` | string | `YourStrong@Passw0rd` | SQL Server SA password |
| `databases.postgreSql.enabled` | bool | true | Enable PostgreSQL tests |
| `databases.postgreSql.image` | string | `postgres:16-alpine` | Docker image for PostgreSQL |
| `databases.postgreSql.database` | string | `testdb` | PostgreSQL database name |
| `databases.postgreSql.username` | string | `postgres` | PostgreSQL username |
| `databases.postgreSql.password` | string | `postgres` | PostgreSQL password |
| `docker.cleanupOnExit` | bool | true | Cleanup containers after tests |
| `docker.reuseContainers` | bool | false | Reuse existing containers |

---

## ?? Usage Examples

### Example 1: Quick Validation (5k products)

**test-config.json**:
```json
{
  "testConfiguration": {
    "datasetSize": 5000,
    "seed": 42
  }
}
```

**Run**:
```powershell
dotnet test --filter "Container=Testcontainers"
```

### Example 2: Realistic Load (50k products)

**test-config.json**:
```json
{
  "testConfiguration": {
    "datasetSize": 50000,
    "seed": 42,
    "enablePerformanceMetrics": true
  }
}
```

### Example 3: Stress Testing (100k products)

**test-config.json**:
```json
{
  "testConfiguration": {
    "datasetSize": 100000,
    "seed": 42,
    "enablePerformanceMetrics": true,
    "performance": {
      "thresholds": {
        "excellentMs": 200,
        "goodMs": 1000,
        "acceptableMs": 2000
      }
    }
  }
}
```

### Example 4: SQL Server Only

**test-config.json**:
```json
{
  "testConfiguration": {
    "datasetSize": 50000,
    "databases": {
      "sqlServer": {
        "enabled": true,
        "image": "mcr.microsoft.com/mssql/server:2022-latest"
      },
      "postgreSql": {
        "enabled": false
      }
    }
  }
}
```

### Example 5: Different SQL Server Version

**test-config.json**:
```json
{
  "testConfiguration": {
    "databases": {
      "sqlServer": {
        "image": "mcr.microsoft.com/mssql/server:2019-latest"
      }
    }
  }
}
```

---

## ?? Environment Variable Overrides

Environment variables **override** test-config.json settings:

### Override Dataset Size

```powershell
# PowerShell
$env:PAGIN8_TEST_DATASET_SIZE = "50000"
dotnet test --filter "Container=Testcontainers"
```

```bash
# Bash
export PAGIN8_TEST_DATASET_SIZE=50000
dotnet test --filter "Container=Testcontainers"
```

### Override Seed

```powershell
$env:PAGIN8_TEST_SEED = "999"
dotnet test
```

### Use Preset

```powershell
# Use predefined preset from test-config.json
$env:PAGIN8_TEST_PRESET = "realistic"
dotnet test --filter "Container=Testcontainers"
```

**Available presets** (defined in test-config.json):
- `quick` - 5,000 products
- `standard` - 10,000 products
- `realistic` - 50,000 products
- `stress` - 100,000 products
- `extreme` - 500,000 products

---

## ?? Performance Presets

### Predefined Presets

test-config.json includes ready-to-use presets:

```json
"performance": {
  "presets": {
    "quick": {
      "datasetSize": 5000,
      "description": "Quick validation (20-30s)"
    },
    "standard": {
      "datasetSize": 10000,
      "description": "Standard testing (30-40s)"
    },
    "realistic": {
      "datasetSize": 50000,
      "description": "Realistic production load (60-90s)"
    },
    "stress": {
      "datasetSize": 100000,
      "description": "Stress testing (2-3m)"
    },
    "extreme": {
      "datasetSize": 500000,
      "description": "Extreme performance testing (5-10m)"
    }
  }
}
```

### Using Presets

**Via environment variable**:
```powershell
$env:PAGIN8_TEST_PRESET = "realistic"
dotnet test --filter "Container=Testcontainers"
```

**Via PowerShell script** (future enhancement):
```powershell
.\run-performance-tests.ps1 -Preset realistic
```

### Custom Presets

Add your own presets to test-config.json:

```json
"performance": {
  "presets": {
    "ci": {
      "datasetSize": 1000,
      "description": "CI/CD quick validation"
    },
    "nightly": {
      "datasetSize": 25000,
      "description": "Nightly regression tests"
    },
    "weekly": {
      "datasetSize": 250000,
      "description": "Weekly performance benchmarks"
    }
  }
}
```

---

## ?? IntelliSense Support

The JSON schema provides IntelliSense in VS Code and Visual Studio:

1. **Open** `test-config.json`
2. **Start typing** - IntelliSense shows available options
3. **Hover** over properties to see descriptions
4. **Validation** - Schema validates your configuration

---

## ?? Configuration Discovery

The system searches for `test-config.json` in these locations (in order):

1. Current directory
2. `IntegrationTests/` subdirectory
3. `../IntegrationTests/`
4. `../../IntegrationTests/`
5. Application base directory
6. Application base directory `/IntegrationTests/`

**Console output** shows which file was loaded:

```
?? Loading configuration from: C:\...\pagin8\src\1Dev.Pagin8.Test\IntegrationTests\test-config.json
```

---

## ?? Multiple Configurations

### Development vs CI/CD

**Option 1: Different files (recommended)**

```
test-config.json              # Default (dev)
test-config.ci.json          # CI/CD
test-config.performance.json # Performance testing
```

Load specific config:
```powershell
# Copy desired config
Copy-Item test-config.performance.json test-config.json
dotnet test
```

**Option 2: Environment variables (simpler)**

Keep one `test-config.json`, override with environment variables:

```yaml
# GitHub Actions
- name: Run Performance Tests
  env:
    PAGIN8_TEST_DATASET_SIZE: 50000
  run: dotnet test --filter "Container=Testcontainers"
```

---

## ?? Troubleshooting

### Configuration not being applied

1. **Check console output** for configuration loading message:
   ```
   ?? Loading configuration from: ...
   ```

2. **Verify file exists**:
   ```powershell
   Test-Path "1Dev.Pagin8.Test\IntegrationTests\test-config.json"
   ```

3. **Check JSON syntax**:
   - Use JSON validator
   - Check for trailing commas
   - Verify schema reference

### Environment variables not working

```powershell
# Verify environment variable is set
echo $env:PAGIN8_TEST_DATASET_SIZE

# Ensure variable is set BEFORE running tests
$env:PAGIN8_TEST_DATASET_SIZE = "50000"
dotnet test
```

### Config file not found

The config file should be copied to output directory automatically (configured in `.csproj`):

```xml
<ItemGroup>
  <None Update="IntegrationTests\test-config.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

---

## ?? Best Practices

### 1. Version Control

? **DO**: Commit `test-config.json` with sensible defaults
```json
{
  "testConfiguration": {
    "datasetSize": 5000,  // Quick default for all developers
    "seed": 42
  }
}
```

? **DON'T**: Commit large dataset sizes that slow down local development

### 2. CI/CD

Use environment variables to override defaults:

```yaml
# Fast for PRs
- name: Quick Tests
  env:
    PAGIN8_TEST_PRESET: quick
  run: dotnet test

# Comprehensive for nightly builds
- name: Nightly Tests
  env:
    PAGIN8_TEST_PRESET: realistic
  run: dotnet test
```

### 3. Team Consistency

Document your presets:
```json
"performance": {
  "presets": {
    "dev": {
      "datasetSize": 5000,
      "description": "Local development - fast feedback"
    },
    "pr": {
      "datasetSize": 10000,
      "description": "Pull request validation"
    },
    "release": {
      "datasetSize": 100000,
      "description": "Pre-release performance validation"
    }
  }
}
```

### 4. Performance Thresholds

Adjust thresholds based on your infrastructure:

```json
"performance": {
  "thresholds": {
    "excellentMs": 100,    // Developer laptops
    "goodMs": 500,
    "acceptableMs": 1000
  }
}
```

For CI/CD servers (slower):
```json
"performance": {
  "thresholds": {
    "excellentMs": 200,    // CI/CD runners
    "goodMs": 1000,
    "acceptableMs": 2000
  }
}
```

---

## ?? Programmatic Access

### In Test Code

```csharp
using _1Dev.Pagin8.Test.IntegrationTests.Configuration;

// Get current configuration
var config = TestConfiguration.Instance;

// Access settings
var datasetSize = config.TestSettings.DatasetSize;
var seed = config.TestSettings.Seed;

// Check thresholds
var excellent = config.TestSettings.Performance.Thresholds.ExcellentMs;

// Database settings
var sqlEnabled = config.TestSettings.Databases.SqlServer.Enabled;
var pgImage = config.TestSettings.Databases.PostgreSql.Image;
```

---

## ?? Related Documentation

- **[README.md](README.md)** - Main documentation
- **[PERFORMANCE_TESTING.md](PERFORMANCE_TESTING.md)** - Performance testing guide
- **[PERFORMANCE_QUICK_REF.md](PERFORMANCE_QUICK_REF.md)** - Quick reference

---

**Status**: ? Configuration System Ready  
**IntelliSense**: ? Full JSON schema support  
**Priority**: Environment Variables > JSON File > Defaults  
**Flexibility**: Complete control via code, config files, or environment
