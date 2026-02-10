# ?? Pagin8 Integration Tests

## ?? Quick Start

### Prerequisites
- **Docker Desktop** running
- **.NET 9 SDK** installed

### Run Tests
```powershell
# Run all integration tests (5,000 products, ~30 seconds)
dotnet test --filter "Container=Testcontainers"
```

**That's it!** ?? Containers start automatically, databases are created, data is seeded, and tests run.

---

## ?? Performance Testing

### Run with Different Dataset Sizes

```powershell
# Quick (5k products - default)
dotnet test --filter "Container=Testcontainers"

# Realistic (50k products)
$env:PAGIN8_TEST_PRESET = "realistic"
dotnet test --filter "Container=Testcontainers"

# Stress (100k products)
$env:PAGIN8_TEST_PRESET = "stress"
dotnet test --filter "Container=Testcontainers"
```

---

## ?? Configuration

### Edit test-config.json
```json
{
  "testConfiguration": {
    "datasetSize": 50000,
    "seed": 42,
    "enablePerformanceMetrics": true
  }
}
```

---

## ?? **Complete Documentation**

All documentation has been moved to the **[Documentation](Documentation/)** folder:

### ?? Start Here
- **[README.md](Documentation/README.md)** ? - Complete guide and quick start
- **[INDEX.md](Documentation/INDEX.md)** - Documentation navigation

### ?? Configuration
- **[CONFIGURATION_GUIDE.md](Documentation/CONFIGURATION_GUIDE.md)** - Complete test-config.json guide
- **[CONFIGURATION_SUMMARY.md](Documentation/CONFIGURATION_SUMMARY.md)** - Quick reference

### ?? Performance Testing
- **[PERFORMANCE_TESTING.md](Documentation/PERFORMANCE_TESTING.md)** - Testing strategies
- **[PERFORMANCE_QUICK_REF.md](Documentation/PERFORMANCE_QUICK_REF.md)** - Quick reference
- **[PERFORMANCE_METRICS_GUIDE.md](Documentation/PERFORMANCE_METRICS_GUIDE.md)** - Understanding metrics

### ?? Testcontainers
- **[TESTCONTAINERS.md](Documentation/TESTCONTAINERS.md)** - Complete guide
- **[TESTCONTAINERS_SUMMARY.md](Documentation/TESTCONTAINERS_SUMMARY.md)** - Quick reference

---

## ?? What Gets Tested

- ? **50 Integration Tests** (25 SQL Server + 25 PostgreSQL)
- ? **All DSL Operators** (eq, gt, lt, gte, lte, cs, stw, enw, like, in, notin, is)
- ? **Complex Queries** (AND, OR, nested conditions)
- ? **Sorting & Pagination**
- ? **Real Databases** (SQL Server 2022, PostgreSQL 16 in Docker)
- ? **Realistic Data** (5,000-500,000 products via Bogus)

---

## ?? Performance Metrics

When you run tests, you'll see detailed metrics:

```
??????????????????????????????????????????????????????????????????????
?  ?? Performance Report - SQL Server                                ?
??????????????????????????????????????????????????????????????????????
?  Dataset Size:         50,000 records                              ?
?  Total Tests:              25 tests                                ?
?  Average:              130.00 ms  ? Good                          ?
??????????????????????????????????????????????????????????????????????
```

See **[PERFORMANCE_METRICS_GUIDE.md](Documentation/PERFORMANCE_METRICS_GUIDE.md)** for details.

---

## ?? Troubleshooting

**Docker not running?**
```powershell
docker ps
# If this fails, start Docker Desktop
```

**Tests slow?**
- First run downloads Docker images (~30-60s)
- Subsequent runs are fast (~10-15s startup)

**Need help?**
See **[Documentation/README.md](Documentation/README.md)** for complete troubleshooting guide.

---

## ?? CI/CD Integration

### GitHub Actions
```yaml
- name: Integration Tests
  run: dotnet test --filter "Container=Testcontainers"
```

### Azure DevOps
```yaml
- script: dotnet test --filter "Container=Testcontainers"
  displayName: 'Integration Tests'
```

No Docker setup needed - CI/CD runners have Docker pre-installed!

---

## ?? Learn More

- **[Complete Documentation](Documentation/)** - All guides in one place
- **[Quick Start](Documentation/README.md)** - Detailed quick start guide
- **[Configuration Guide](Documentation/CONFIGURATION_GUIDE.md)** - All configuration options
- **[Performance Testing](Documentation/PERFORMANCE_TESTING.md)** - Testing strategies

---

**Status**: ? Production Ready  
**Tests**: 50 comprehensive integration tests  
**Databases**: SQL Server 2022 + PostgreSQL 16  
**Setup Time**: 0 minutes (automatic)  
**Documentation**: ?? [Complete docs in Documentation folder](Documentation/)
