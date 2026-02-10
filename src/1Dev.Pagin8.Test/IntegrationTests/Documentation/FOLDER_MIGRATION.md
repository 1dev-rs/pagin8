# ?? Documentation Moved to Subfolder

## ? Changes Made

All markdown documentation files have been **organized into the `Documentation/` subfolder** for better structure.

---

## ?? New Structure

```
IntegrationTests/
??? README.md                          ? Quick start (points to Documentation/)
??? test-config.json                   Configuration file
??? test-config.schema.json            JSON schema
??? run-performance-tests.ps1          Performance testing script
??? run-performance-tests.bat          Windows batch wrapper
??? Documentation/                     ?? All documentation here
?   ??? README.md                      Complete guide
?   ??? INDEX.md                       Navigation hub
?   ??? CONFIGURATION_GUIDE.md         Configuration reference
?   ??? CONFIGURATION_SUMMARY.md       Quick config reference
?   ??? PERFORMANCE_TESTING.md         Testing strategies
?   ??? PERFORMANCE_QUICK_REF.md       Quick performance reference
?   ??? PERFORMANCE_METRICS_GUIDE.md   Understanding metrics
?   ??? TESTCONTAINERS.md              Complete Testcontainers guide
?   ??? TESTCONTAINERS_SUMMARY.md      Quick Testcontainers reference
?   ??? DOCUMENTATION_REVIEW.md        Documentation analysis
?   ??? DOCUMENTATION_STATUS.md        Quality assessment
??? Configuration/
?   ??? TestConfiguration.cs           Configuration reader
??? Data/
?   ??? TestDataSeeder.cs              Bogus data generation
??? Fixtures/
?   ??? SqlServerContainerFixture.cs   SQL Server container
?   ??? PostgreSqlContainerFixture.cs  PostgreSQL container
??? Models/
?   ??? Product.cs                     Entity model
??? Performance/
?   ??? PerformanceMetricsCollector.cs Metrics collection
??? SqlServerContainerIntegrationTests.cs
??? PostgreSqlContainerIntegrationTests.cs
```

---

## ?? Benefits of New Structure

### Before (Messy)
```
IntegrationTests/
??? README.md
??? INDEX.md
??? CONFIGURATION_GUIDE.md
??? CONFIGURATION_SUMMARY.md
??? PERFORMANCE_TESTING.md
??? PERFORMANCE_QUICK_REF.md
??? PERFORMANCE_METRICS_GUIDE.md
??? TESTCONTAINERS.md
??? TESTCONTAINERS_SUMMARY.md
??? test-config.json
??? Models/
??? Data/
??? Fixtures/
??? ... (11 files in root!)
```

### After (Clean) ?
```
IntegrationTests/
??? README.md                 ? Entry point
??? test-config.json          Configuration
??? Documentation/            ?? All docs organized
??? Configuration/            Code
??? Data/                     Code
??? Fixtures/                 Code
??? Models/                   Code
??? Performance/              Code
```

---

## ?? How to Access Documentation

### From GitHub
Browse to:
```
1Dev.Pagin8.Test/IntegrationTests/Documentation/
```

### From File System
Navigate to:
```
src/1Dev.Pagin8.Test/IntegrationTests/Documentation/
```

### Direct Links (relative to IntegrationTests/)
- [Documentation/README.md](Documentation/README.md) - Start here
- [Documentation/INDEX.md](Documentation/INDEX.md) - Find what you need
- [Documentation/CONFIGURATION_GUIDE.md](Documentation/CONFIGURATION_GUIDE.md) - Configuration
- [Documentation/PERFORMANCE_QUICK_REF.md](Documentation/PERFORMANCE_QUICK_REF.md) - Quick reference

---

## ? What Still Works

Everything still works exactly the same:

### Running Tests
```powershell
# From repository root
dotnet test --filter "Container=Testcontainers"
```

### Configuration
```json
// test-config.json (still in IntegrationTests root)
{
  "testConfiguration": {
    "datasetSize": 50000
  }
}
```

### Scripts
```powershell
# Scripts still in IntegrationTests root
.\1Dev.Pagin8.Test\IntegrationTests\run-performance-tests.ps1 -DatasetSize 50000
```

---

## ?? Entry Points

### Quick Start
1. Read `IntegrationTests/README.md` (quick overview)
2. Click through to `Documentation/README.md` (complete guide)

### Navigation
1. Go to `Documentation/INDEX.md` (navigation hub)
2. Find the guide you need
3. Read and learn!

---

## ?? Documentation Files

All 11 markdown files are now in `Documentation/`:

1. **README.md** - Complete integration testing guide
2. **INDEX.md** - Navigation and learning paths
3. **CONFIGURATION_GUIDE.md** - Complete test-config.json reference
4. **CONFIGURATION_SUMMARY.md** - Quick configuration reference
5. **PERFORMANCE_TESTING.md** - Performance testing strategies
6. **PERFORMANCE_QUICK_REF.md** - Performance quick reference
7. **PERFORMANCE_METRICS_GUIDE.md** - Understanding metrics output
8. **TESTCONTAINERS.md** - Complete Testcontainers guide
9. **TESTCONTAINERS_SUMMARY.md** - Quick Testcontainers reference
10. **DOCUMENTATION_REVIEW.md** - Documentation analysis
11. **DOCUMENTATION_STATUS.md** - Quality assessment

---

## ?? Benefits

### For Developers
? **Cleaner root directory** - Less clutter, easier to find code  
? **Logical organization** - Docs separated from code  
? **Easy browsing** - All docs in one place  

### For Documentation
? **Better organization** - Clear documentation folder  
? **Easier maintenance** - All docs together  
? **Professional structure** - Standard practice  

### For GitHub
? **Clean repository view** - Documentation folder stands out  
? **Easy navigation** - Click into Documentation folder  
? **Professional appearance** - Well-organized project  

---

## ?? Migration Complete

**Status**: ? All documentation moved  
**Structure**: ?? Clean and organized  
**Links**: ? All working (relative paths)  
**Build**: ? Successful  
**Tests**: ? Still work exactly the same  

**No code changes required** - this is purely organizational! ??

---

## ?? Notes

### What Changed
- ? All `.md` files moved to `Documentation/` subfolder
- ? New `IntegrationTests/README.md` created (entry point)
- ? Links in `INDEX.md` still work (relative paths)

### What Didn't Change
- ? Test code (no changes)
- ? Configuration files (still in IntegrationTests root)
- ? Scripts (still in IntegrationTests root)
- ? Build process (no changes)
- ? Tests execution (works exactly the same)

---

**This is a purely organizational change for better project structure!** ???
