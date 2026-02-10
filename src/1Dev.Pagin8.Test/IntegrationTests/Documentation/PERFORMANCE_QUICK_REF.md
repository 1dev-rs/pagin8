# ?? Performance Testing - Quick Reference

## ? Configure Dataset Size

### Method 1: PowerShell Script (Easiest) ?

```powershell
# Navigate to test directory
cd 1Dev.Pagin8.Test\IntegrationTests

# Default (5,000 products)
.\run-performance-tests.ps1

# 50,000 products (recommended for performance testing)
.\run-performance-tests.ps1 -DatasetSize 50000

# 100,000 products (stress testing)
.\run-performance-tests.ps1 -DatasetSize 100000

# SQL Server only with 50k products
.\run-performance-tests.ps1 -DatasetSize 50000 -Database SqlServer

# With different random seed
.\run-performance-tests.ps1 -DatasetSize 50000 -Seed 999

# Verbose output (detailed logging)
.\run-performance-tests.ps1 -DatasetSize 50000 -Verbose
```

### Method 2: Environment Variables

```powershell
# PowerShell
$env:PAGIN8_TEST_DATASET_SIZE = "50000"
$env:PAGIN8_TEST_SEED = "42"
dotnet test --filter "Container=Testcontainers"

# Clean up
Remove-Item Env:\PAGIN8_TEST_DATASET_SIZE
Remove-Item Env:\PAGIN8_TEST_SEED
```

```bash
# Bash (Linux/macOS)
export PAGIN8_TEST_DATASET_SIZE=50000
export PAGIN8_TEST_SEED=42
dotnet test --filter "Container=Testcontainers"
```

### Method 3: Windows Batch

```batch
# Simple
run-performance-tests.bat 50000

# With database selection
run-performance-tests.bat 100000 SqlServer
```

---

## ?? Dataset Size Recommendations

| Dataset Size | Use Case | Execution Time | Rating |
|--------------|----------|----------------|--------|
| **5,000** (default) | Quick validation during development | ~20-30s | ? Fast |
| **10,000** | Standard integration testing | ~30-40s | ? Good |
| **50,000** | Realistic production load | ~60-90s | ? Recommended |
| **100,000** | Stress testing | ~2-3m | ?? Stress |
| **500,000** | Performance limits | ~5-10m | ?? Extreme |
| **1,000,000** | Breaking point analysis | ~15-20m | ?? Extreme |

---

## ?? Common Scenarios

### Development Workflow

```powershell
# Quick validation (during development)
dotnet test --filter "Container=Testcontainers"

# Before commit (realistic testing)
.\run-performance-tests.ps1 -DatasetSize 50000

# Before PR (stress testing)
.\run-performance-tests.ps1 -DatasetSize 100000 -Verbose
```

### Performance Analysis

```powershell
# Baseline (5k)
.\run-performance-tests.ps1 -DatasetSize 5000

# Medium load (50k)
.\run-performance-tests.ps1 -DatasetSize 50000

# Heavy load (100k)
.\run-performance-tests.ps1 -DatasetSize 100000

# Compare SQL Server vs PostgreSQL
.\run-performance-tests.ps1 -DatasetSize 50000 -Database SqlServer
.\run-performance-tests.ps1 -DatasetSize 50000 -Database PostgreSql
```

### CI/CD Integration

```yaml
# GitHub Actions - Pull Request (fast)
- name: Integration Tests
  run: dotnet test --filter "Container=Testcontainers"
  # 5k dataset, ~30s

# GitHub Actions - Nightly (realistic)
- name: Performance Tests
  run: |
    $env:PAGIN8_TEST_DATASET_SIZE = "50000"
    dotnet test --filter "Container=Testcontainers"
  # 50k dataset, ~60s

# GitHub Actions - Weekly (stress)
- name: Stress Tests
  run: |
    $env:PAGIN8_TEST_DATASET_SIZE = "100000"
    dotnet test --filter "Container=Testcontainers"
  # 100k dataset, ~2-3m
```

---

## ?? Performance Expectations

### Query Performance by Dataset Size

| Query Type | 5k | 50k | 100k | 500k |
|------------|-----|-----|------|------|
| Simple indexed (eq) | 3ms | 8ms | 12ms | 35ms |
| String search (cs) | 5ms | 25ms | 50ms | 200ms |
| Complex filters | 10ms | 50ms | 100ms | 400ms |
| Multi-column sort | 15ms | 80ms | 200ms | 1000ms |

### Total Execution Time

| Dataset | Container Start | Seeding | Tests | Total |
|---------|----------------|---------|-------|-------|
| 5k | 10s | 5s | 15s | **30s** |
| 50k | 10s | 30s | 25s | **65s** |
| 100k | 10s | 60s | 40s | **110s** |
| 500k | 10s | 5m | 2m | **7m** |

---

## ??? Configuration Details

### Environment Variables

| Variable | Description | Default | Example |
|----------|-------------|---------|---------|
| `PAGIN8_TEST_DATASET_SIZE` | Number of products to generate | 5000 | 50000 |
| `PAGIN8_TEST_SEED` | Random seed for reproducibility | 42 | 999 |

### PowerShell Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `-DatasetSize` | int | Number of products | 5000 |
| `-Database` | string | SqlServer, PostgreSql, Both | Both |
| `-Seed` | int | Random seed | 42 |
| `-Verbose` | switch | Detailed logging | false |

---

## ?? Sample Output

```
========================================
Pagin8 Performance Testing
========================================

Configuration:
  ?? Dataset Size: 50.0k products (50000 records)
  ???  Database(s): Both
  ?? Seed: 42

? Docker is running
??  Estimated execution time: ~85 seconds

Starting SQL Server container...
Creating Products table...
Seeding 50000 products (seed: 42)...
? SQL Server ready with 50,000 products (seeded in 28.42s)

? TC-SQL-001: Equality (eq) - Found 12,470 active products (8ms)
? TC-SQL-006: Contains (cs) - Found 1,782 products containing 'Shoes' (25ms)
? TC-SQL-021: E-commerce: Budget Electronics - Found 892 products (45ms)

Starting PostgreSQL container...
? PostgreSQL ready with 50,000 products (seeded in 25.18s)

? TC-PG-001: Equality (eq) - Found 12,470 active products (7ms)
? TC-PG-022: E-commerce: Premium Featured - Found 421 products (32ms)

========================================
? All tests passed!
??  Total execution time: 01:05
??  Dataset size: 50.0k products

Performance Summary:
  Tests executed: 50
  Average per test: 127ms
  Dataset: 50k records
  Rating: ? Good
========================================
```

---

## ?? Tips & Best Practices

### For Development
- ? Use **5k dataset** for fast iteration (default)
- ? Use **50k dataset** before commits
- ? Use **100k dataset** before PRs

### For CI/CD
- ? **PRs**: 5k dataset (~30s)
- ? **Nightly**: 50k dataset (~60s)
- ? **Weekly**: 100k-500k dataset (~5-10m)

### For Performance Testing
1. Start with **5k** to establish baseline
2. Scale up: **5k ? 50k ? 100k ? 500k**
3. Monitor and compare metrics
4. Use **same seed (42)** for reproducibility
5. Change seed to test different distributions

### Troubleshooting
- Tests slow? Start with smaller dataset
- Out of memory? Increase Docker RAM or reduce dataset
- Timeout? Increase test timeout in xunit.runner.json

---

## ?? Documentation

- **Complete Guide**: [PERFORMANCE_TESTING.md](PERFORMANCE_TESTING.md)
- **Main README**: [README.md](README.md)
- **All Docs**: [INDEX.md](INDEX.md)

---

**Quick Start**:
```powershell
# 1. Navigate to test directory
cd 1Dev.Pagin8.Test\IntegrationTests

# 2. Run with 50k products
.\run-performance-tests.ps1 -DatasetSize 50000

# Done! ??
```

**Status**: ? Ready for performance testing  
**Flexibility**: Configure any dataset size (1k - 1M+)  
**Ease of Use**: One command, full control
