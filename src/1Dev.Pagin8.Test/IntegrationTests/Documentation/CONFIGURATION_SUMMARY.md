# ? Configuration System - Summary

## ?? What Changed

Replaced environment variables with a **configuration file system** (`test-config.json`) for better management of test settings.

---

## ?? New Files Created

### 1. **test-config.json** ?
Main configuration file with all test settings:
- Dataset size
- Random seed
- Database configurations (SQL Server, PostgreSQL)
- Performance presets
- Performance thresholds
- Docker settings

### 2. **test-config.schema.json**
JSON schema for IntelliSense support in VS Code and Visual Studio

### 3. **TestConfiguration.cs**
Configuration reader class that:
- Loads from JSON file
- Applies environment variable overrides
- Provides singleton access

### 4. **CONFIGURATION_GUIDE.md**
Comprehensive documentation explaining:
- How to use test-config.json
- Configuration priority
- All available settings
- Usage examples
- Best practices

---

## ?? Quick Start

### Method 1: JSON Configuration (Easiest) ?

**Edit** `1Dev.Pagin8.Test/IntegrationTests/test-config.json`:
```json
{
  "testConfiguration": {
    "datasetSize": 50000,
    "seed": 42
  }
}
```

**Run**:
```powershell
dotnet test --filter "Container=Testcontainers"
```

### Method 2: Environment Variables (Still Works!)

```powershell
# Override specific settings
$env:PAGIN8_TEST_DATASET_SIZE = "50000"
dotnet test --filter "Container=Testcontainers"
```

### Method 3: Performance Presets (New!)

```powershell
# Use predefined presets
$env:PAGIN8_TEST_PRESET = "realistic"  # 50k products
dotnet test --filter "Container=Testcontainers"
```

**Available presets**:
- `quick` - 5,000 products (20-30s)
- `standard` - 10,000 products (30-40s)
- `realistic` - 50,000 products (60-90s)
- `stress` - 100,000 products (2-3m)
- `extreme` - 500,000 products (5-10m)

---

## ?? Configuration Priority

Settings are applied in this order:

1. **Environment Variables** (highest) - `$env:PAGIN8_TEST_DATASET_SIZE`
2. **test-config.json** (middle)
3. **Code Defaults** (lowest) - 5000 products, seed 42

This means you can:
- Set defaults in `test-config.json`
- Override for specific runs with environment variables
- Share team configuration via version control

---

## ?? Example Configurations

### Development (Fast)
```json
{
  "testConfiguration": {
    "datasetSize": 5000,
    "seed": 42
  }
}
```

### CI/CD (Quick Validation)
```json
{
  "testConfiguration": {
    "datasetSize": 10000,
    "seed": 42,
    "enablePerformanceMetrics": true
  }
}
```

### Performance Testing (Realistic)
```json
{
  "testConfiguration": {
    "datasetSize": 50000,
    "seed": 42,
    "enablePerformanceMetrics": true,
    "performance": {
      "thresholds": {
        "excellentMs": 100,
        "goodMs": 500,
        "acceptableMs": 1000
      }
    }
  }
}
```

### Stress Testing
```json
{
  "testConfiguration": {
    "datasetSize": 100000,
    "enablePerformanceMetrics": true
  }
}
```

---

## ?? Modified Files

| File | Changes |
|------|---------|
| **SqlServerContainerFixture.cs** | Now reads from `TestConfiguration.Instance` |
| **PostgreSqlContainerFixture.cs** | Now reads from `TestConfiguration.Instance` |
| **1Dev.Pagin8.Test.csproj** | Copies JSON files to output directory |
| **README.md** | Updated with configuration methods |

---

## ? Benefits

### Before (Environment Variables Only)
```powershell
# Had to set multiple variables
$env:PAGIN8_TEST_DATASET_SIZE = "50000"
$env:PAGIN8_TEST_SEED = "42"
dotnet test
```

### After (Configuration File)
```json
// Edit once in test-config.json
{
  "testConfiguration": {
    "datasetSize": 50000,
    "seed": 42
  }
}
```
```powershell
# Just run tests
dotnet test --filter "Container=Testcontainers"
```

### Advantages
? **IntelliSense Support** - JSON schema provides auto-completion  
? **Version Control** - Share configuration with team  
? **Validation** - Schema validates your config  
? **Documentation** - Self-documenting with schema descriptions  
? **Flexibility** - Still supports environment variable overrides  
? **Presets** - Predefined configurations for common scenarios  
? **Easy Discovery** - One file to find all settings  

---

## ?? Documentation

| Document | Purpose |
|----------|---------|
| **CONFIGURATION_GUIDE.md** ? | Complete configuration guide |
| **PERFORMANCE_TESTING.md** | Performance testing strategies |
| **PERFORMANCE_QUICK_REF.md** | Quick reference cheat sheet |
| **README.md** | Main documentation (updated) |

---

## ?? Common Use Cases

### 1. Developer Local Testing
```json
// test-config.json (committed to repo)
{
  "testConfiguration": {
    "datasetSize": 5000  // Fast for development
  }
}
```

### 2. Performance Analysis
```powershell
# Override for one-time performance test
$env:PAGIN8_TEST_DATASET_SIZE = "100000"
dotnet test --filter "Container=Testcontainers"
```

### 3. CI/CD Pipeline
```yaml
# GitHub Actions
- name: Integration Tests (Quick)
  env:
    PAGIN8_TEST_PRESET: quick
  run: dotnet test --filter "Container=Testcontainers"

- name: Nightly Performance Tests
  env:
    PAGIN8_TEST_PRESET: realistic
  run: dotnet test --filter "Container=Testcontainers"
```

### 4. Different Database Versions
```json
{
  "testConfiguration": {
    "databases": {
      "sqlServer": {
        "image": "mcr.microsoft.com/mssql/server:2019-latest"
      },
      "postgreSql": {
        "image": "postgres:15-alpine"
      }
    }
  }
}
```

---

## ?? Getting Started

1. **Open** `1Dev.Pagin8.Test/IntegrationTests/test-config.json`
2. **Edit** `datasetSize` to your preference
3. **Run** `dotnet test --filter "Container=Testcontainers"`
4. **Done!** Configuration is automatically loaded

---

## ?? Advanced Features

### Custom Presets

Add your own presets to `test-config.json`:

```json
"performance": {
  "presets": {
    "ci": {
      "datasetSize": 1000,
      "description": "CI/CD quick check"
    },
    "my-custom": {
      "datasetSize": 75000,
      "description": "My custom performance test"
    }
  }
}
```

Use it:
```powershell
$env:PAGIN8_TEST_PRESET = "my-custom"
dotnet test
```

### Performance Thresholds

Customize what counts as "excellent", "good", or "acceptable":

```json
"performance": {
  "thresholds": {
    "excellentMs": 50,   // Very fast machines
    "goodMs": 200,
    "acceptableMs": 500
  }
}
```

### Docker Configuration

```json
"docker": {
  "cleanupOnExit": true,        // Cleanup containers after tests
  "reuseContainers": false      // Start fresh each time
}
```

---

## ?? Learn More

- **Full Guide**: [CONFIGURATION_GUIDE.md](CONFIGURATION_GUIDE.md)
- **Performance Testing**: [PERFORMANCE_TESTING.md](PERFORMANCE_TESTING.md)
- **Quick Reference**: [PERFORMANCE_QUICK_REF.md](PERFORMANCE_QUICK_REF.md)
- **Main README**: [README.md](README.md)

---

**Status**: ? Configuration System Ready  
**Backward Compatible**: ? Environment variables still work  
**IntelliSense**: ? Full JSON schema support  
**Team Ready**: ? Version control friendly
