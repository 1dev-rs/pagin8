# Testcontainers Integration Tests

## ?? Overview

This directory contains **modern integration tests** using [Testcontainers for .NET](https://dotnet.testcontainers.org/), which automatically spin up **real databases in Docker containers** for testing.

### ? Key Benefits

- ? **Zero manual setup** - Databases start automatically
- ? **Isolated tests** - Each test run uses fresh containers
- ? **True integration** - Tests against real SQL Server & PostgreSQL
- ? **CI/CD ready** - Works in GitHub Actions, Azure DevOps, etc.
- ? **Consistent data** - Reproducible test data every time
- ? **Multiple databases** - Test both SQL Server and PostgreSQL

---

## ?? Prerequisites

### 1. Docker Desktop (Required)

Install Docker Desktop for your platform:

**Windows:**
```bash
# Download from: https://www.docker.com/products/docker-desktop/
# Or use winget:
winget install Docker.DockerDesktop
```

**macOS:**
```bash
brew install --cask docker
```

**Linux:**
```bash
# Follow instructions at: https://docs.docker.com/desktop/install/linux-install/
```

**Verify installation:**
```bash
docker --version
docker ps
```

### 2. .NET 9 SDK
```bash
dotnet --version  # Should be 9.0 or higher
```

---

## ?? Quick Start

### Run All Tests

```bash
dotnet test
```

That's it! The tests will:
1. ? Pull SQL Server and PostgreSQL images (first time only)
2. ?? Start containers
3. ?? Create tables and seed data
4. ? Run tests
5. ?? Clean up containers

### Run Specific Database Tests

**SQL Server only:**
```bash
dotnet test --filter "Database=SqlServer"
```

**PostgreSQL only:**
```bash
dotnet test --filter "Database=PostgreSql"
```

**Testcontainer tests only:**
```bash
dotnet test --filter "Container=Testcontainers"
```

---

## ?? Test Data

### Product Schema

Tests use a realistic **e-commerce Products** table:

```csharp
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }              // "Wireless Mouse #42"
    public string Category { get; set; }          // Electronics, Clothing, Books, etc.
    public string Status { get; set; }            // Active, Inactive, Discontinued, etc.
    public decimal Price { get; set; }            // $10.00 - $1000.00
    public int Stock { get; set; }                // 0 - 1000
    public string? Brand { get; set; }            // TechCorp, StyleMax, etc.
    public string? Description { get; set; }      
    public DateTime CreatedAt { get; set; }       // Last 2 years
    public DateTime? UpdatedAt { get; set; }      
    public string? Tags { get; set; }             // bestseller, new-arrival, etc.
    public bool IsFeatured { get; set; }          
    public double Rating { get; set; }            // 1.0 - 5.0
}
```

### Sample Data

Each test run generates **5,000 products** with:
- **8 categories**: Electronics, Clothing, Books, Home & Garden, Sports, Toys, Food & Beverage, Health & Beauty
- **5 statuses**: Active, Inactive, Discontinued, ComingSoon, OutOfStock
- **7 brands**: TechCorp, StyleMax, HomePro, SportsFit, KidZone, NatureGoods, HealthPlus
- **Realistic prices**: $10 - $1000
- **Stock levels**: 0 - 1000 units
- **Ratings**: 1.0 - 5.0 stars
- **Dates**: Products created over last 2 years

---

## ?? Test Coverage

### SQL Server Tests (`SqlServerContainerIntegrationTests`)

**25 tests** covering:
- ? Comparison operators (eq, gt, lt, gte, lte)
- ? String operators (cs, stw, enw, like)
- ? Date operations (ranges, relative dates)
- ? Logical operators (AND, OR)
- ? IN / NOT IN operators
- ? Boolean filters
- ? Sorting & pagination
- ? Complex real-world queries

**Example queries tested:**
```
TC-SQL-001: status=eq.Active
TC-SQL-002: price=gt.500
TC-SQL-006: name=cs.Mouse
TC-SQL-010: createdAt=gte.2024-01-01&createdAt=lte.2024-12-31
TC-SQL-021: category=eq.Electronics&price=lt.200&status=eq.Active&stock=gt.0
```

### PostgreSQL Tests (`PostgreSqlContainerIntegrationTests`)

**25 tests** covering:
- Same as SQL Server tests
- ? PostgreSQL-specific features (ILIKE for case-insensitive search)

---

## ??? Project Structure

```
IntegrationTests/
??? Models/
?   ??? Product.cs                              # Test entity model
??? Data/
?   ??? TestDataSeeder.cs                       # Data generation & SQL scripts
??? Fixtures/
?   ??? SqlServerContainerFixture.cs           # SQL Server Testcontainer setup
?   ??? PostgreSqlContainerFixture.cs          # PostgreSQL Testcontainer setup
??? SqlServerContainerIntegrationTests.cs       # SQL Server tests
??? PostgreSqlContainerIntegrationTests.cs      # PostgreSQL tests
??? TESTCONTAINERS.md                           # This file
```

---

## ?? How It Works

### 1. Testcontainer Fixture

Each database has a fixture that:
```csharp
public class SqlServerContainerFixture : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        // 1. Create container
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
        
        // 2. Start container
        await _container.StartAsync();
        
        // 3. Create schema
        await Connection.ExecuteAsync(createTableScript);
        
        // 4. Seed data
        var products = TestDataSeeder.GenerateProducts(5000);
        await Connection.ExecuteAsync(insertScript, products);
    }
}
```

### 2. Test Collection

Tests share the fixture (container runs once per test class):
```csharp
[Collection("SqlServer Testcontainer")]
public class SqlServerContainerIntegrationTests
{
    public SqlServerContainerIntegrationTests(SqlServerContainerFixture fixture)
    {
        // Container is already running and seeded
    }
}
```

### 3. Automatic Cleanup

Containers are automatically stopped and removed after tests complete.

---

## ?? Configuration

### Change Number of Test Records

Edit `TestDataSeeder.cs`:
```csharp
// In fixture InitializeAsync()
var products = TestDataSeeder.GenerateProducts(count: 10000, seed: 42);
```

### Use Different Docker Images

Edit fixtures:
```csharp
// SQL Server
new MsSqlBuilder()
    .WithImage("mcr.microsoft.com/mssql/server:2019-latest")
    
// PostgreSQL
new PostgreSqlBuilder()
    .WithImage("postgres:15-alpine")
```

### Keep Containers Running (Debug)

Comment out cleanup in fixture:
```csharp
public async Task DisposeAsync()
{
    // await _container.DisposeAsync(); // Keep container running
}
```

Then connect manually:
```bash
# Get connection string from test output
# Example: Server=localhost,32768;Database=master;...
```

---

## ?? Example Test Run

```bash
$ dotnet test --filter "Database=SqlServer"

Starting SQL Server container...
SQL Server container started: Server=localhost,32769;Database=master;...
Creating Products table...
Seeding 5000 products...
SQL Server ready with 5000 products

? TC-SQL-001: Equality (eq) - Filter by exact status
  Found 1247 active products
  
? TC-SQL-002: Greater Than (gt) - Filter by price > 500
  Found 2384 products with price > $500
  
? TC-SQL-006: Contains (cs) - Search in product name
  Found 178 products containing 'Mouse'
  
...

? TC-SQL-021: E-commerce: Find Budget Electronics
  Found 89 budget electronics in stock with ratings
  
Stopping SQL Server container...

Test Run Successful.
Total tests: 25
     Passed: 25
```

---

## ?? CI/CD Integration

### GitHub Actions

```yaml
name: Integration Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'
      
      - name: Run Integration Tests
        run: dotnet test --filter "Container=Testcontainers" --logger "console;verbosity=normal"
```

**No Docker setup needed!** GitHub Actions runners have Docker pre-installed.

### Azure DevOps

```yaml
trigger:
  - main

pool:
  vmImage: 'ubuntu-latest'

steps:
  - task: UseDotNet@2
    inputs:
      version: '9.0.x'
  
  - script: dotnet test --filter "Container=Testcontainers"
    displayName: 'Run Integration Tests'
```

---

## ? Performance

### First Run (Cold Start)
- **SQL Server**: ~30-45 seconds (image pull + container start)
- **PostgreSQL**: ~15-20 seconds (image pull + container start)

### Subsequent Runs (Warm Start)
- **SQL Server**: ~10-15 seconds (container start + data seed)
- **PostgreSQL**: ~5-8 seconds (container start + data seed)

### Test Execution
- **Per test**: ~50-200ms
- **Full suite (25 tests)**: ~5-10 seconds

---

## ?? Troubleshooting

### "Docker is not running"

**Start Docker Desktop:**
```bash
# Check Docker status
docker ps

# If not running, start Docker Desktop application
```

### "Container failed to start"

**Check Docker logs:**
```bash
docker ps -a  # Find container ID
docker logs <container-id>
```

**Common causes:**
- Port already in use (Testcontainers uses random ports)
- Docker out of disk space
- Image pull failed (check internet connection)

### "Tests timeout"

**Increase timeout in test:**
```csharp
[Fact(Timeout = 60000)] // 60 seconds
public async Task MyTest() { }
```

### "Can't connect to container from WSL2"

**Windows users:** Ensure Docker Desktop is configured for WSL2:
```bash
# In Docker Desktop settings:
Settings ? General ? Use WSL 2 based engine ?
```

---

## ?? Additional Resources

- [Testcontainers for .NET Documentation](https://dotnet.testcontainers.org/)
- [Docker Desktop Installation](https://docs.docker.com/desktop/)
- [SQL Server Docker Image](https://hub.docker.com/_/microsoft-mssql-server)
- [PostgreSQL Docker Image](https://hub.docker.com/_/postgres)

---

## ?? Next Steps

1. **Run the tests**: `dotnet test --filter "Container=Testcontainers"`
2. **Check test output**: See generated SQL queries and results
3. **Add custom tests**: Copy existing test patterns
4. **Customize data**: Modify `TestDataSeeder.cs`
5. **CI/CD**: Add to your pipeline (see examples above)

---

**Happy Testing! ??**
