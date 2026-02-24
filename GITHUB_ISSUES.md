# GitHub Issues - Pagin8 Improvement Plan

## Phase 2: HIGH PRIORITY

### Issue #5: IsToken Negation Logic Bug - Incorrect Empty/Non-Empty Filtering

**Status:** ✅ RESOLVED — `AppendEmptyQueryConditions` refactored with explicit conditional branches in both `NpgsqlTokenVisitor` and `SqlServerTokenVisitor`.

**Labels:** `bug`, `priority:high`, `phase-2`

**Description:**

## Priority: HIGH
## Severity: 🔴 CRITICAL
## File: `src/1Dev.Pagin8/Internal/Visitors/NpgsqlTokenVisitor.cs:358`
## Effort: 🟡 1 hour

### Problem
The join logic in `AppendEmptyQueryConditions` is semantically confusing and works by accident:

```csharp
var join = token.IsNegated ? " AND " : " OR ";  // ❌ SEMANTICALLY CONFUSING
```

### Expected Behavior
- `field=is.$empty` → `(field IS NULL OR field = '')`
- `field=not.is.$empty` → `(field IS NOT NULL AND field <> '')`

### Solution
Refactor `AppendEmptyQueryConditions` to use explicit conditional logic for text fields:
- For NOT empty: `field IS NOT NULL AND field <> ''`
- For empty: `field IS NULL OR field = ''`
- For non-text fields: only check NULL

### Test Cases
- ✅ `name=is.$empty` → `(name IS NULL OR name = '')`
- ✅ `name=not.is.$empty` → `(name IS NOT NULL AND name <> '')`
- ✅ `count=is.$empty` → `count IS NULL`
- ✅ `count=not.is.$empty` → `count IS NOT NULL`

**Phase:** 2 - High Priority
**Impact:** Incorrect filtering results for empty/non-empty queries

---

### Issue #6: Date Range Validation - Missing Sanity Check (start <= end)

**Status:** ✅ RESOLVED — `DateProcessor.GetStartAndEndOfRelativeDate` now passes the result through `ValidateDateRange()` which throws `Pagin8Exception(Pagin8_InvalidDateRange)` when `start > end`.

**Labels:** `enhancement`, `priority:high`, `phase-2`

**Description:**

## Priority: HIGH
## Severity: 🟠 HIGH
## File: `src/1Dev.Pagin8/Internal/DateProcessor/DateProcessor.cs:57-118`
## Effort: 🟡 45 minutes

### Problem
No validation that calculated date ranges are valid (start <= end). This can lead to invalid SQL queries and incorrect results.

### Solution
Add validation method:
```csharp
private static (DateTime start, DateTime end) ValidateDateRange(DateTime start, DateTime end)
{
    if (start > end)
        throw new Pagin8Exception(
            Pagin8StatusCode.Pagin8_InvalidDateRange.Code,
            $"Invalid date range: start ({start}) is after end ({end})"
        );
    return (start, end);
}
```

Apply to all `Calculate*Range` methods: `CalculateWeekRange`, `CalculateMonthRange`, etc.

### Additional Work
Add new status code:
```csharp
public static Pagin8StatusCode Pagin8_InvalidDateRange = new()
{
    Code = "PAGIN8_INVALID_DATE_RANGE",
    Message = "The calculated date range is invalid"
};
```

**Phase:** 2 - High Priority
**Impact:** Invalid date ranges can cause query errors

---

### Issue #7: CultureInfo Inconsistency in Type Conversion (Double/Float)

**Status:** ✅ RESOLVED — `double.TryParse` updated to use `NumberStyles.Float, CultureInfo.InvariantCulture` in `NpgsqlTokenVisitor`, `SqlServerTokenVisitor`, and `LinqTokenVisitor`.

**Labels:** `bug`, `priority:high`, `phase-2`

**Description:**

## Priority: HIGH
## Severity: 🟠 HIGH
## File: `src/1Dev.Pagin8/Internal/Visitors/NpgsqlTokenVisitor.cs:598-612`
## Effort: 🟢 15 minutes

### Problem
Inconsistent CultureInfo usage in numeric parsing:
```csharp
TypeCode.Double => double.TryParse(value, out var doubleValue) ? ...
// ❌ No CultureInfo specified

TypeCode.Decimal => decimal.TryParse(value, CultureInfo.InvariantCulture, out var decimalValue) ? ...
// ✅ CultureInfo specified
```

This causes locale-dependent parsing bugs.

### Solution
```csharp
TypeCode.Double => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue)
    ? doubleValue
    : throw new ArgumentException($"Cannot format value {value} as Double"),

TypeCode.Single => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue)
    ? floatValue
    : throw new ArgumentException($"Cannot format value {value} as Single"),
```

### Files to Update
- `NpgsqlTokenVisitor.cs:598-612`
- `LinqTokenVisitor.cs:414-428`

**Phase:** 2 - High Priority
**Impact:** Locale-dependent parsing bugs with decimal separators

---

### Issue #8: SQL Injection Risk - FormattableString Field Name Replacement

**Status:** ✅ RESOLVED — `ValidateJsonFieldName()` added as a private static method in both `NpgsqlTokenVisitor` and `SqlServerTokenVisitor`, called at the start of `HandleJsonArrayFilter` before `token.Field` is embedded in any SQL string. Validates that the field name starts with a letter or underscore and contains only alphanumeric characters, underscores, or dots. Throws `Pagin8Exception(Pagin8_TokenFieldInvalid)` on violation.

**Labels:** `security`, `priority:high`, `phase-2`

**Description:**

## Priority: HIGH
## Severity: 🟠 HIGH (Security)
## File: `src/1Dev.Pagin8/Internal/Visitors/NpgsqlTokenVisitor.cs:375`
## Effort: 🟡 2 hours

### Problem
Field name inserted via string replacement BEFORE FormattableString creation:
```csharp
var formattedArrayQuery = FormattableStringFactory.Create(
    BaseJsonArrayQuery.ToString().Replace("/**field**/", token.Field)
);
// ❌ token.Field inserted via string replacement before FormattableString creation
```

This bypasses parameterization if field validation is somehow bypassed.

### Solution Option 1 (Preferred)
Add strict field name validation:
```csharp
private static void ValidateJsonFieldName(string fieldName)
{
    if (string.IsNullOrWhiteSpace(fieldName))
        throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidField.Code);

    if (!Regex.IsMatch(fieldName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        throw new Pagin8Exception(
            Pagin8StatusCode.Pagin8_InvalidField.Code,
            $"Invalid JSON field name: {fieldName}"
        );
}
```

### Solution Option 2 (Better)
Use proper parameterization with `:raw` for column names:
```csharp
private FormattableString BuildJsonArrayQuery(string field, FormattableString innerConditions)
{
    var quotedField = TryFormatColumnName(field);
    return $@"EXISTS (
        SELECT 1 FROM jsonb_array_elements({quotedField:raw}) AS elem
        WHERE {innerConditions}
    )";
}
```

**Phase:** 2 - High Priority
**Impact:** Potential SQL injection if field validation bypassed

---

### Issue #9: Performance - Cache Reflection Method Lookups (50% improvement)

**Status:** ✅ RESOLVED — Added `private static readonly ConcurrentDictionary<(Type TokenType, Type EntityType), MethodInfo> _methodCache` to both `NpgsqlTokenVisitor` and `SqlServerTokenVisitor`. `DynamicVisit` now uses `_methodCache.GetOrAdd(...)` so `GetMethod` + `MakeGenericMethod` are called only once per unique `(tokenType, entityType)` pair. Added `using System.Collections.Concurrent` to both visitors.

**Labels:** `performance`, `priority:high`, `phase-2`, `optional`

**Description:**

## Priority: HIGH (Optional)
## Severity: 🟠 HIGH
## File: `src/1Dev.Pagin8/Internal/Visitors/NpgsqlTokenVisitor.cs:859-869`
## Effort: 🟡 2 hours

### Problem
Reflection overhead on every dynamic visit call:
```csharp
private void DynamicVisit(Token token, QueryBuilderResult result, Type type)
{
    var method = GetType().GetMethod("Visit", ...);  // ❌ REFLECTION EVERY CALL
    var genericMethod = method.MakeGenericMethod(type);
    var invokeResult = genericMethod.Invoke(this, [token, result]);
}
```

### Solution
Add static method cache using ConcurrentDictionary:
```csharp
private static readonly ConcurrentDictionary<(Type tokenType, Type entityType), MethodInfo> _methodCache = new();

private void DynamicVisit(Token token, QueryBuilderResult result, Type type)
{
    var key = (token.GetType(), type);

    var genericMethod = _methodCache.GetOrAdd(key, k =>
    {
        var method = GetType().GetMethod(
            "Visit",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            [k.tokenType, typeof(QueryBuilderResult)],
            null
        ) ?? throw new InvalidOperationException($"Visit method not found for {k.tokenType.Name}");

        return method.MakeGenericMethod(k.entityType);
    });

    var invokeResult = genericMethod.Invoke(this, [token, result]);
    // ...
}
```

### Benchmark
Expected ~50% performance improvement for nested filters.

**Phase:** 2 - High Priority (OPTIONAL)
**Impact:** O(n) reflection overhead for nested filters

---

## Phase 3: MEDIUM PRIORITY

### Issue #12: Null Semantics - Incorrect Sort Coalescing for Nullable Columns

**Status:** ✅ RESOLVED — Keyset pagination `IsNullAllowed` branch in `NpgsqlTokenVisitor` and `SqlServerTokenVisitor` now handles null cursor value explicitly (`IS NULL`) and for non-null values uses an explicit null-aware compound expression instead of `COALESCE(null, fallback)`.

**Labels:** `bug`, `priority:medium`, `phase-3`

**Description:**

## Priority: MEDIUM
## Severity: 🟡 MEDIUM
## File: `src/1Dev.Pagin8/Internal/Visitors/NpgsqlTokenVisitor.cs:280-297`
## Effort: 🟡 1 hour

### Problem
When `formattedValue` is `null`, `COALESCE(null, fallback)` might not match intended behavior for keyset pagination:
```csharp
if (columnInfo.IsNullAllowed)
{
    var coalesce = GetCoalesceMinValue(typeCode);
    builder.AppendFormattableString(
        $" COALESCE({formattedName:raw}, {coalesce}) {@operator:raw} COALESCE({formattedValue}, {coalesce})"
    );
}
```

### Solution
Handle null values explicitly:
```csharp
if (columnInfo.IsNullAllowed)
{
    if (formattedValue == null)
    {
        // For null values in keyset pagination, use IS NULL comparison
        builder.AppendFormattableString($" {formattedName:raw} IS NULL");
    }
    else
    {
        var coalesce = GetCoalesceMinValue(typeCode);
        builder.AppendFormattableString(
            $" (({formattedName:raw} IS NULL AND {coalesce} {@operator:raw} {formattedValue}) OR " +
            $"({formattedName:raw} IS NOT NULL AND {formattedName:raw} {@operator:raw} {formattedValue}))"
        );
    }
}
```

**Phase:** 3 - Medium Priority
**Impact:** Incorrect keyset pagination for nullable columns

---

### Issue #13: Security - Regex Pattern Injection (ReDoS Risk)

**Status:** ✅ RESOLVED — Added `Esc()`/`EscJoin()` helpers in `TokenHelper` that wrap `Regex.Escape()`. All 14 pattern properties now escape config values before interpolating them. Added `ValidateConfiguration()` startup check for empty/null config values.

**Labels:** `security`, `priority:medium`, `phase-3`

**Description:**

## Priority: MEDIUM
## Severity: 🟡 MEDIUM (Security)
## File: `src/1Dev.Pagin8/Internal/Helpers/TokenHelper.cs:18-50`
## Effort: 🟡 1 hour

### Problem
Config values used in regex patterns without escaping:
```csharp
public static string ComparisonPattern =>
    $@"^(?<field>[^.]+)=(?<negation>{EngineDefaults.Config.Negation}\.)?(?<operator>({string.Join("|", EngineDefaults.Config.ComparisonOperators)}))\.(?<val>.*?)(?:\^(?<comment>.+))?$";
```

Potential ReDoS if config contains regex special chars.

### Solution
Escape regex special characters:
```csharp
private static string EscapeRegexSpecialChars(string input)
{
    return Regex.Escape(input);
}

public static string ComparisonPattern =>
    $@"^(?<field>[^.]+)=(?<negation>{EscapeRegexSpecialChars(EngineDefaults.Config.Negation)}\.)?(?<operator>({string.Join("|", EngineDefaults.Config.ComparisonOperators.Select(EscapeRegexSpecialChars))}))\.(?<val>.*?)(?:\^(?<comment>.+))?$";
```

### Additional Validation
Add config validation on startup:
```csharp
public static void ValidateConfiguration()
{
    if (string.IsNullOrWhiteSpace(Config.Negation))
        throw new InvalidOperationException("Negation config cannot be empty");

    if (Config.ComparisonOperators.Any(string.IsNullOrWhiteSpace))
        throw new InvalidOperationException("Comparison operators cannot contain empty values");
}
```

**Phase:** 3 - Medium Priority
**Impact:** Potential ReDoS if config contains regex metacharacters

---

### Issue #14: Validation - Empty IN Operator Values Not Rejected

**Status:** ✅ RESOLVED — `InTokenizationStrategy` now validates non-empty value list and rejects blank entries, throwing `Pagin8Exception(Pagin8_InvalidIn)`.

**Labels:** `enhancement`, `priority:medium`, `phase-3`

**Description:**

## Priority: MEDIUM
## Severity: 🟡 MEDIUM
## File: `src/1Dev.Pagin8/Internal/Tokenizer/Strategy/InTokenizationStrategy.cs:36-39`
## Effort: 🟢 30 minutes

### Problem
Current validation doesn't catch empty value lists. Queries like `field=in.()` should be rejected.

### Solution
Add validation for empty and invalid value lists:
```csharp
if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(@operator) || string.IsNullOrEmpty(value))
    throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidIn.Code);

var values = SplitInValues(value);

// ✅ Add validation for empty value list
if (values.Count == 0)
    throw new Pagin8Exception(
        Pagin8StatusCode.Pagin8_InvalidIn.Code,
        "IN operator requires at least one value"
    );

// ✅ Validate no empty values
if (values.Any(string.IsNullOrWhiteSpace))
    throw new Pagin8Exception(
        Pagin8StatusCode.Pagin8_InvalidIn.Code,
        "IN operator values cannot be empty"
    );
```

### Test Cases
- ❌ Invalid: `field=in.()`
- ❌ Invalid: `field=in.(,)`
- ❌ Invalid: `field=in.(1,,3)`
- ✅ Valid: `field=in.(1,2,3)`

**Phase:** 3 - Medium Priority
**Impact:** Invalid queries like `field=in.()` not properly rejected

---

### Issue #15: Performance - Unnecessary List Materialization in Standardize()

**Status:** ✅ RESOLVED — Removed `.ToList()` from `TokenizationService.Standardize()`; smart materialization (`tokens as List<Token> ?? tokens.ToList()`) moved into `Tokenizer.RevertToQueryString()`.

**Labels:** `performance`, `priority:medium`, `phase-3`

**Description:**

## Priority: MEDIUM
## Severity: 🟡 MEDIUM
## File: `src/1Dev.Pagin8/Internal/Tokenizer/TokenizationService.cs:56`
## Effort: 🟢 15 minutes

### Problem
Unnecessary `.ToList()` call materializes the entire token collection:
```csharp
public string Standardize(IEnumerable<Token> tokens)
{
    var orderedTokens = EnsureOrderByPriority(tokens).ToList();  // Unnecessary ToList
    return tokenizer.RevertToQueryString(orderedTokens);
}
```

### Solution Option 1
Remove materialization if not needed:
```csharp
public string Standardize(IEnumerable<Token> tokens)
{
    var orderedTokens = EnsureOrderByPriority(tokens);
    // If RevertToQueryString needs List, materialize inside that method
    return tokenizer.RevertToQueryString(orderedTokens);
}
```

### Solution Option 2
Smart materialization:
```csharp
public string RevertToQueryString(IEnumerable<Token> tokens)
{
    var tokenList = tokens as List<Token> ?? tokens.ToList();  // Smart materialization
    // ... use tokenList
}
```

**Phase:** 3 - Medium Priority
**Impact:** Minor memory overhead for large token sets

---

## Phase 4: LOW PRIORITY (Backlog)

### Issue #10: Feature Parity - LINQ Visitor Nested Filtering Not Implemented

**Labels:** `enhancement`, `priority:low`, `phase-4`, `hard`

**Description:**

## Priority: LOW (Backlog)
## Severity: 🟠 HIGH
## File: `src/1Dev.Pagin8/Internal/Visitors/LinqTokenVisitor.cs:374-376`
## Effort: 🔴 8 hours (HARD)

### Problem
LINQ visitor doesn't support nested filtering:
```csharp
public IQueryable<T> Visit(NestedFilterToken token, IQueryable<T> queryable)
{
    throw new NotImplementedException();  // ❌ NOT IMPLEMENTED
}
```

This creates feature inconsistency between SQL and LINQ backends.

### Solution
Implement LINQ equivalent of nested filtering using Expression trees:
```csharp
public IQueryable<T> Visit(NestedFilterToken token, IQueryable<T> queryable)
{
    var parameter = Expression.Parameter(typeof(T), "x");
    var property = Expression.Property(parameter, token.Field);

    // Build nested predicate based on token.InnerFilter
    var nestedPredicate = BuildNestedPredicate(property, token.InnerFilter);

    var lambda = Expression.Lambda<Func<T, bool>>(nestedPredicate, parameter);
    return queryable.Where(lambda);
}

private Expression BuildNestedPredicate(Expression property, string filter)
{
    // Parse and build expression tree from filter
    // This is complex - need to tokenize and build expression recursively
}
```

### Note
This is a significant undertaking. Prioritize based on actual LINQ visitor usage data in production.

**Phase:** 4 - Backlog (HARD)
**Impact:** Inconsistent feature support between SQL and LINQ

---

### Issue #11: Feature Gap - JSON Path IN Support Not Implemented

**Labels:** `enhancement`, `priority:low`, `phase-4`, `hard`

**Description:**

## Priority: LOW (Backlog)
## Severity: 🟠 HIGH
## File: `src/1Dev.Pagin8/Internal/Tokenizer/Strategy/InTokenizationStrategy.cs:51-53`
## Effort: 🔴 4 hours (HARD)

### Problem
Cannot use IN operator on nested JSON properties:
```csharp
public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
{
    throw new NotImplementedException();
}
```

### Solution
Implement JSON path support:
```csharp
public List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1)
{
    var match = Regex.Match(query, InPattern);
    if (!match.Success)
        throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidIn.Code);

    var field = match.Groups["field"].Value;
    var negation = match.Groups["negation"].Value;
    var @operator = match.Groups["operator"].Value;
    var value = match.Groups["val"].Value;

    // Validate inputs
    if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(@operator) || string.IsNullOrEmpty(value))
        throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidIn.Code);

    var values = SplitInValues(value);
    var isNegated = !string.IsNullOrEmpty(negation);

    return [new InToken
    {
        Field = field,
        Operator = @operator,
        Values = values,
        IsNegated = isNegated,
        JsonPath = jsonPath,  // Pass through JSON path
        NestingLevel = nestingLevel
    }];
}
```

Then update `NpgsqlTokenVisitor.Visit<T>(InToken token, ...)` to handle JSON path case.

**Phase:** 4 - Backlog (HARD)
**Impact:** Cannot use IN operator on nested JSON properties

---

### Issue #16: UX - Guard.AgainstNull Missing Parameter Name for Better Debugging

**Status:** ✅ RESOLVED — `Guard.AgainstNull` updated with `[CallerArgumentExpression(nameof(value))]` for automatic parameter name capture.

**Labels:** `enhancement`, `priority:low`, `phase-4`

**Description:**

## Priority: LOW (Backlog)
## Severity: 🔵 LOW
## File: `src/1Dev.Pagin8/Internal/Utils/Guard.cs:4-10`
## Effort: 🟢 15 minutes

### Problem
Guard throws ArgumentNullException without parameter name:
```csharp
public static void AgainstNull<T>(T value)
{
    if (value == null)
        throw new ArgumentNullException();  // ❌ NO PARAMETER NAME
}
```

This makes debugging harder.

### Solution
Use CallerArgumentExpression:
```csharp
public static void AgainstNull<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
{
    if (value == null)
        throw new ArgumentNullException(paramName ?? "value");
}
```

### Usage Example
```csharp
Guard.AgainstNull(myParameter);
// Throws: ArgumentNullException: Value cannot be null. (Parameter 'myParameter')
```

**Phase:** 4 - Backlog
**Impact:** Poor debugging experience

---

### Issue #17: Validation - No Limit Value Range Check (negative/zero limits)

**Status:** ✅ RESOLVED — Both `NpgsqlTokenVisitor` and `SqlServerTokenVisitor` now throw `Pagin8Exception(Pagin8_InvalidLimit)` when `token.Value <= 0`.

**Labels:** `enhancement`, `priority:low`, `phase-4`

**Description:**

## Priority: LOW (Backlog)
## Severity: 🔵 LOW
## File: `src/1Dev.Pagin8/Internal/Visitors/NpgsqlTokenVisitor.cs:233-236`
## Effort: 🟢 10 minutes

### Problem
No validation that limit value is positive:
```csharp
public QueryBuilderResult Visit<T>(LimitToken token, QueryBuilderResult result) where T : class
{
    result.Builder.Append($"LIMIT {token.Value}");  // ❌ NO VALIDATION
    return result;
}
```

This can generate invalid SQL for negative or zero limits.

### Solution
Add validation:
```csharp
public QueryBuilderResult Visit<T>(LimitToken token, QueryBuilderResult result) where T : class
{
    if (token.Value <= 0)
        throw new Pagin8Exception(
            Pagin8StatusCode.Pagin8_InvalidLimit.Code,
            $"Limit must be positive, got: {token.Value}"
        );

    result.Builder.Append($"LIMIT {token.Value}");
    return result;
}
```

### Additional Work
Add new status code:
```csharp
public static Pagin8StatusCode Pagin8_InvalidLimit = new()
{
    Code = "PAGIN8_INVALID_LIMIT",
    Message = "The limit value must be a positive integer"
};
```

### Test Cases
- ❌ Invalid: `limit.0`
- ❌ Invalid: `limit.-1`
- ✅ Valid: `limit.10`

**Phase:** 4 - Backlog
**Impact:** Invalid SQL for negative/zero limits

---

## Summary

**Total Issues:** 12 (11 resolved, 2 open)

### Resolved
- ✅ #5 — IsToken negation logic bug
- ✅ #6 — Date range start > end not validated
- ✅ #7 — CultureInfo inconsistency in Double/Float parsing
- ✅ #8 — SQL injection risk via FormattableString field name replacement
- ✅ #9 — Cache reflection method lookups (performance)
- ✅ #12 — Null semantics in nullable column sort coalescing
- ✅ #13 — ReDoS risk in regex patterns (Regex.Escape + startup validation)
- ✅ #14 — Empty IN operator values not rejected
- ✅ #15 — Unnecessary `ToList()` in `Standardize()`
- ✅ #16 — `Guard.AgainstNull` missing parameter name
- ✅ #17 — No limit value range check

### Open
- 🔵 #10 — LINQ visitor nested filtering not implemented (hard, backlog)
- 🔵 #11 — JSON path IN support not implemented (hard, backlog)

### By Phase
- **Phase 2 (High Priority):** 5 issues (~10 hours)
- **Phase 3 (Medium Priority):** 4 issues (~3 hours)
- **Phase 4 (Low Priority):** 3 issues (~12.5 hours)

### By Type
- **Bug:** 4 issues
- **Enhancement:** 5 issues
- **Security:** 2 issues
- **Performance:** 2 issues

### Quick Action Items
1. Create these issues in GitHub
2. Apply appropriate labels
3. Assign to milestones based on phase
4. Delete IMPROVEMENT_PLAN.md after tickets are created
