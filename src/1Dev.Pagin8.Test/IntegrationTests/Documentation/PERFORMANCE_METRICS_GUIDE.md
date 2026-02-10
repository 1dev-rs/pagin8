# ?? Performance Metrics - Visual Guide

## ?? What You'll See

When you run performance tests with `enablePerformanceMetrics: true` in `test-config.json`, you'll see **detailed performance reports** at the end of each test run.

---

## ?? Sample Output

### Running with Realistic Preset (50k products)

```powershell
$env:PAGIN8_TEST_PRESET = "realistic"
dotnet test --filter "Container=Testcontainers"
```

### Expected Output:

```
Starting SQL Server container...
Creating Products table...
Seeding 50,000 products (seed: 42)...
? SQL Server ready with 50,000 products (seeded in 28.42s)
?? Configuration: 50,000 records, seed=42, image=mcr.microsoft.com/mssql/server:2022-latest

Running tests...

? TC-SQL-001: Equality (eq) - Found 12,470 active products
  Query: status=eq.Active
  SQL: SELECT * FROM Products WHERE 1=1 AND status = @p0 ORDER BY Id OFFSET 0 ROWS FETCH NEXT 50 ROWS ONLY
  Records: 50
  ??  Execution Time: 12ms

? TC-SQL-002: Greater Than (gt) - Found 23,840 products
  Query: price=gt.500
  ??  Execution Time: 18ms

? TC-SQL-006: Contains (cs) - Found 1,782 products containing 'Shoes'
  Query: name=cs.Shoes
  ??  Execution Time: 35ms

... (more tests) ...

Stopping SQL Server container...

??????????????????????????????????????????????????????????????????????
?  ?? Performance Report - SQL Server                                ?
??????????????????????????????????????????????????????????????????????
?  Dataset Size:         50,000 records                              ?
?  Total Tests:              25 tests                                ?
?  Total Time:            3,250 ms                                   ?
??????????????????????????????????????????????????????????????????????
?  Average:              130.00 ms  ? Good                          ?
?  Median:                98.00 ms                                   ?
?  Min:                   12 ms                                      ?
?  Max:                  487 ms                                      ?
??????????????????????????????????????????????????????????????????????

Performance Distribution:
  ? Excellent (< 100ms):     17 tests (68.0%) ?????????????
  ? Good (100-500ms):         7 tests (28.0%) ?????
  ??  Acceptable (500-1000ms): 1 tests ( 4.0%) ?
  ?? Slow (> 1000ms):          0 tests ( 0.0%) 

Top 5 Slowest Queries:
  1. ?? ComplexQuery_MultiCategoryPopular                487ms ( 20 results)
  2. ??  Sorting_MultiColumn                              356ms ( 25 results)
  3. ? ComplexQuery_BudgetElectronics                    245ms ( 20 results)
  4. ? InOperator_MultipleCategories                     198ms ( 50 results)
  5. ? StringContains_ShouldFindPartialMatches          135ms ( 50 results)

Top 5 Fastest Queries:
  1. ? Equality_ShouldFilterByExactMatch                  12ms ( 50 results)
  2. ? GreaterThan_ShouldFilterCorrectly                  18ms ( 50 results)
  3. ? LessThan_ShouldFilterCorrectly                     22ms ( 50 results)
  4. ? BooleanFilter_ShouldFilterCorrectly                28ms ( 30 results)
  5. ? DateRanges_ShouldFilterCorrectly                   35ms ( 50 results)

```

Then the same for PostgreSQL:

```
Starting PostgreSQL container...
? PostgreSQL ready with 50,000 products (seeded in 25.18s)

... (running tests) ...

??????????????????????????????????????????????????????????????????????
?  ?? Performance Report - PostgreSQL                                ?
??????????????????????????????????????????????????????????????????????
?  Dataset Size:         50,000 records                              ?
?  Total Tests:              25 tests                                ?
?  Total Time:            2,980 ms                                   ?
??????????????????????????????????????????????????????????????????????
?  Average:              119.20 ms  ? Good                          ?
?  Median:                85.00 ms                                   ?
?  Min:                    8 ms                                      ?
?  Max:                  425 ms                                      ?
??????????????????????????????????????????????????????????????????????

Performance Distribution:
  ? Excellent (< 100ms):     18 tests (72.0%) ??????????????
  ? Good (100-500ms):         7 tests (28.0%) ?????
  ??  Acceptable (500-1000ms): 0 tests ( 0.0%) 
  ?? Slow (> 1000ms):          0 tests ( 0.0%) 

Top 5 Slowest Queries:
  1. ? ComplexQuery_MultiCategoryPopular                425ms ( 20 results)
  2. ? Sorting_MultiColumn                              298ms ( 25 results)
  3. ? ComplexQuery_BudgetElectronics                    215ms ( 20 results)
  4. ? InOperator_MultipleCategories                     178ms ( 50 results)
  5. ? StringContains_ShouldFindPartialMatches          125ms ( 50 results)

Top 5 Fastest Queries:
  1. ? Equality_ShouldFilterByExactMatch                   8ms ( 50 results)
  2. ? GreaterThan_ShouldFilterCorrectly                  15ms ( 50 results)
  3. ? LessThan_ShouldFilterCorrectly                     19ms ( 50 results)
  4. ? BooleanFilter_ShouldFilterCorrectly                25ms ( 30 results)
  5. ? DateRanges_ShouldFilterCorrectly                   32ms ( 50 results)

Test Run Successful.
Total tests: 50
     Passed: 50 ?
     Failed: 0
     Skipped: 0
  Total time: 1m 25s
```

---

## ?? Metrics Explained

### Performance Report Box
- **Dataset Size**: Number of products in database
- **Total Tests**: Number of queries executed
- **Total Time**: Total execution time for all tests
- **Average**: Mean execution time
- **Median**: Middle value (better indicator than average)
- **Min**: Fastest query
- **Max**: Slowest query

### Performance Distribution
Shows how many tests fall into each category:
- **? Excellent**: < 100ms (configurable in test-config.json)
- **? Good**: 100-500ms
- **??  Acceptable**: 500-1000ms
- **?? Slow**: > 1000ms (needs optimization)

**Bar Chart**: Visual representation of distribution

### Top 5 Slowest Queries
Helps identify:
- Queries that need optimization
- Complex queries that may need indexing
- Potential bottlenecks

### Top 5 Fastest Queries
Shows:
- Well-optimized queries
- Simple indexed lookups
- Expected performance baseline

---

## ?? Customizing Thresholds

Edit `test-config.json`:

```json
{
  "testConfiguration": {
    "performance": {
      "thresholds": {
        "excellentMs": 50,    // Very fast
        "goodMs": 200,
        "acceptableMs": 500
      }
    }
  }
}
```

This will change the rating categories in the report.

---

## ?? Interpreting Results

### Excellent Results (?)
```
Average: 45.00 ms  ? Excellent
Performance Distribution:
  ? Excellent (< 100ms):  25 tests (100%) ????????????????????
```
**Meaning**: All queries are highly optimized. Production-ready! ??

### Good Results (?)
```
Average: 150.00 ms  ? Good
Performance Distribution:
  ? Excellent (< 100ms):  18 tests (72%) ??????????????
  ? Good (100-500ms):      7 tests (28%) ?????
```
**Meaning**: Most queries are fast, a few complex ones are slower. Still good for production.

### Acceptable Results (?? )
```
Average: 650.00 ms  ??  Acceptable
Performance Distribution:
  ? Good (100-500ms):     15 tests (60%) ????????????
  ??  Acceptable (500-1000ms): 8 tests (32%) ??????
  ?? Slow (> 1000ms):       2 tests ( 8%) ??
```
**Meaning**: Some queries need optimization. Review slowest queries and add indexes.

### Poor Results (??)
```
Average: 1250.00 ms  ?? Slow
Performance Distribution:
  ??  Acceptable (500-1000ms): 10 tests (40%) ????????
  ?? Slow (> 1000ms):          15 tests (60%) ????????????
```
**Meaning**: Performance issues! Check:
1. Database indexes
2. Dataset size too large
3. Docker resource allocation
4. Query optimization needed

---

## ?? What to Look For

### Red Flags ??
- Average > 500ms with 50k dataset
- Any query > 1000ms on simple operations
- Median >> Average (means a few very slow queries)
- Most tests in "Slow" category

### Good Signs ?
- Average < 200ms with 50k dataset
- Most tests "Excellent" or "Good"
- Median ? Average (consistent performance)
- Max < 500ms (no extreme outliers)

---

## ?? Troubleshooting Slow Queries

### If you see slow queries (> 500ms):

1. **Check the Top 5 Slowest list**
   - Identify which queries are slow

2. **Review the SQL output**
   - Look at the generated SQL in test output
   - Check for missing indexes

3. **Run EXPLAIN PLAN** (SQL Server)
   ```sql
   SET SHOWPLAN_ALL ON
   GO
   -- Your slow query here
   GO
   SET SHOWPLAN_ALL OFF
   ```

4. **Run EXPLAIN** (PostgreSQL)
   ```sql
   EXPLAIN ANALYZE 
   -- Your slow query here
   ```

5. **Add indexes if needed**
   - Common index targets: WHERE clauses, JOIN columns, ORDER BY fields

---

## ?? Tips

### Enable Metrics (Default)
`test-config.json`:
```json
{
  "testConfiguration": {
    "enablePerformanceMetrics": true  // ? This is on by default
  }
}
```

### Disable Metrics (Faster runs)
```json
{
  "testConfiguration": {
    "enablePerformanceMetrics": false  // ? Turn off for quick runs
  }
}
```

### Compare Databases
Run both databases to compare performance:
```powershell
dotnet test --filter "Container=Testcontainers"
```

Look at both performance reports to see which database performs better for your queries.

---

## ?? Performance Trends

### Track Over Time
Save metrics from each run and compare:

```
# Release 1.0 (5k dataset)
SQL Server Average: 45ms ?
PostgreSQL Average: 38ms ?

# Release 1.1 (50k dataset - 10x more data)
SQL Server Average: 130ms ?  (2.9x slower - good scaling!)
PostgreSQL Average: 119ms ?  (3.1x slower - good scaling!)
```

**Good scaling**: 10x data = 2-4x slower  
**Bad scaling**: 10x data = 10x+ slower (needs optimization)

---

## ?? Advanced: Custom Analysis

### Export Metrics (Future Enhancement)
You could enhance `PerformanceMetricsCollector` to export JSON:

```csharp
public void ExportToJson(string filePath)
{
    var report = GenerateReport();
    var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(filePath, json);
}
```

Then analyze in Excel, Power BI, or custom dashboards.

---

**Status**: ? Performance Metrics Enabled  
**Visual**: Beautiful console reports with colors and charts  
**Actionable**: Top 5 slowest/fastest help prioritize optimization
