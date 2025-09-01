using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using AspNet.Transliterator;
using InterpolatedSql.SqlBuilders;
using _1Dev.Pagin8.Extensions;
using _1Dev.Pagin8.Internal.DateProcessor;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Metadata.Models;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;
using _1Dev.Pagin8.Internal.Validators;
using Pagin8.Internal.Configuration;
using QueryBuilder = InterpolatedSql.Dapper.SqlBuilders.QueryBuilder;

namespace _1Dev.Pagin8.Internal.Visitors;
public class NpgsqlTokenVisitor(IPagin8MetadataProvider metadata, IDateProcessor dateProcessor) : ISqlTokenVisitor
{

    #region Public methods

    public QueryBuilderResult Visit<T>(ComparisonToken token, QueryBuilderResult result) where T : class
    {
        var (procType, innerType) = DetermineProcessingType(typeof(T), token.JsonPath);
        var comparison = GenerateDatabaseComparison(innerType, token);
        var typeCode = GetTypeCodeForProperty(innerType, token.Field);
        var isText = IsText(typeCode);

        var leftHandSide = GetLeftHandSideExpression(procType, comparison.Column, token.JsonPath, typeCode);

        var query = BuildQuery(leftHandSide, typeCode, token, isText, comparison);
        TryHandleNullColumns(comparison.Column, token, ref query);
        result.Builder.AppendFormattableString(query);

        return result;
    }

    public QueryBuilderResult Visit<T>(GroupToken groupToken, QueryBuilderResult result) where T : class
    {
        var builder = result.Builder;

        if (groupToken.IsNegated)
        {
            builder.AppendFormattableString($"{EngineDefaults.Config.Negation:raw} ");
        }

        builder.AppendFormattableString($"(");

        if (!string.IsNullOrEmpty(groupToken.JsonPath))
        {
            groupToken.Tokens.Update(x => x.JsonPath = groupToken.JsonPath);
        }

        for (var index = 0; index < groupToken.Tokens.Count; index++)
        {
            var child = groupToken.Tokens[index];

            ProcessChildToken<T>(child, result);

            if (QueryBuilderHelper.HasNextToken(index, groupToken.Tokens))
            {
                builder.AppendFormattableString($"{groupToken.GetSqlOperator():raw}");
            }
        }

        builder.Append($")");

        return result;
    }

    public QueryBuilderResult Visit<T>(InToken token, QueryBuilderResult result) where T : class
    {
        var (procType, innerType) = DetermineProcessingType(typeof(T), token.JsonPath);
        var comparison = GenerateDatabaseComparison(innerType, token);
        var typeCode = GetTypeCodeForProperty(innerType, token.Field);
        var isText = IsText(typeCode);

        var column = GetLeftHandSideExpression(procType, comparison.Column, token.JsonPath, typeCode);

        var query = GenerateInQuery(column, isText, token, comparison);

        TryHandleNullColumns(column, token, ref query);
        result.Builder.AppendFormattableString(query);

        return result;
    }


    public QueryBuilderResult Visit<T>(SortToken token, QueryBuilderResult result) where T : class
    {
        MapPlaceholderToKey<T>(token.SortExpressions);
        AssertFieldsSortable<T>(token.SortExpressions);
        BuildSortConditions<T>(result.Builder, token.SortExpressions);
        BuildSortOrder<T>(result.Builder, token);
        return result;
    }

    public QueryBuilderResult Visit<T>(SelectToken token, QueryBuilderResult result) where T : class
    {
        if (result.Builder == null!) return result; // Skip select for count only

        var requestedFields = !token.Fields.Contains(QueryConstants.SelectAsterisk) ?
            token.Fields.ToList() :
            metadata.Get(typeof(T)).Select(x => x.Name).ToList();

        EnsureMandatoryFieldsInSelectionList<T>(token);
        var sel = string.Join(", ", token.Fields.Select(TryFormatColumnName));

        result.Builder.Select($"{sel:raw}");
        result.Meta.RequestedFields = requestedFields;
        result.Meta.SelectedFields = token.Fields;

        return result;
    }

    public QueryBuilderResult Visit<T>(PagingToken token, QueryBuilderResult result) where T : class
    {
        token.Sort?.Accept<T>(this, result);
        token.Limit?.Accept<T>(this, result);
        token.Count?.Accept<T>(this, result);

        return result;
    }

    public QueryBuilderResult Visit<T>(ShowCountToken token, QueryBuilderResult result) where T : class
    {
        result.Meta.ShowCount = token.Value;

        return result;
    }

    public QueryBuilderResult Visit<T>(MetaIncludeToken token, QueryBuilderResult result) where T : class
    {
        result.Meta.SetAdditionalInfo(token);

        return result;
    }

    public QueryBuilderResult Visit<T>(NestedFilterToken token, QueryBuilderResult result) where T : class
    {
        var columnInfo = GetColumnInfo<T>(token.Field);
        var (processingType, _) = DetermineProcessingType(columnInfo.Type, token.Field);

        if (processingType == ProcessingType.JsonArray)
        {
            HandleJsonArrayFilter(token, result, columnInfo);
        }
        else
        {
            HandleRegularFilter(token, result, columnInfo);
        }

        result.Builder.Append($")");
        return result;
    }

    public QueryBuilderResult Visit<T>(ArrayOperationToken token, QueryBuilderResult result) where T : class
    {
        var elementType = GetArrayElementTypeOrThrow<T>(token.Field, out var propertyType);

        var isSimple =
            elementType.IsPrimitive ||
            elementType == typeof(string) ||
            !typeof(IEnumerable).IsAssignableFrom(typeof(T)); 

        if (isSimple)
        {
            ProcessSimpleTypeArray(token, result, propertyType);
        }
        else
        {
            ProcessComplexTypeArray(token, result, propertyType);
        }

        return result;
    }

    public QueryBuilderResult Visit<T>(DateRangeToken token, QueryBuilderResult result) where T : class
    {
        var (procType, innerType) = DetermineProcessingType(typeof(T), token.JsonPath);
        var typeCode = GetTypeCodeForProperty(innerType, token.Field);
        var columnName = GetColumnInfo<T>(token.Field).Name;
        var formattedName = TryFormatColumnName(columnName);
        var leftHandSide = GetLeftHandSideExpression(procType, formattedName, token.JsonPath, typeCode);

        var currentDate = DateTime.Now;

        var goBackwards = token.Operator is DateRangeOperator.Ago;

        var (startDate, endDate) = dateProcessor.GetStartAndEndOfRelativeDate(currentDate, token.Value, token.Range, goBackwards, token.Exact, token.Strict);

        var query =
            (FormattableString)
            $"{leftHandSide:raw} {token.GetSqlOperator():raw} {startDate:yyyy-MM-dd HH: mm: ss.fffffff} AND {endDate:yyyy-MM-dd HH: mm: ss.fffffff}";

        TryHandleNullColumns(leftHandSide, token, ref query);
        result.Builder.AppendFormattableString(query);

        return result;
    }

    public QueryBuilderResult Visit<T>(IsToken token, QueryBuilderResult result) where T : class
    {
        var (procType, innerType) = DetermineProcessingType(typeof(T), token.JsonPath);
        var columnInfo = GetColumnInfo(innerType, token.Field);
        var typeCode = GetTypeCodeForProperty(innerType, token.Field);
        var negation = token.IsNegated ? EngineDefaults.Config.Negation : "";

        result.Builder.Append($"(");

        var formattedName = TryFormatColumnName(columnInfo.Name);
        var leftHandSide = GetLeftHandSideExpression(procType, formattedName, token.JsonPath, typeCode);

        if (token.IsEmptyQuery)
        {
            AppendEmptyQueryConditions(result, innerType, token, leftHandSide, negation);
        }
        else
        {
            AppendValueQueryCondition(result, token, leftHandSide, negation);
        }

        result.Builder.Append($")");
        return result;
    }

    public QueryBuilderResult Visit<T>(LimitToken token, QueryBuilderResult result) where T : class
    {
        result.Builder.Append($"LIMIT {token.Value}");
        return result;
    }

    #endregion

    #region Private methods

    private static FormattableString BuildQuery(string column, TypeCode typeCode, ComparisonToken token, bool isText, DbComparison comparison)
    {
        return typeCode == TypeCode.DateTime
            ? $"DATE({column:raw}) {token.GetSqlOperator(isText):raw} DATE({comparison.Value})"
            : (FormattableString)$"{column:raw} {token.GetSqlOperator(isText):raw} {comparison.Value} {TryEscapeSpecialChars(token.Operator):raw}";
    }

    private void BuildSortConditions<T>(QueryBuilder builder, IReadOnlyList<SortExpression> sortExpressions) where T : class
    {
        InterpolatedSqlBuilderOptions.DefaultOptions.ReuseIdenticalParameters = true;
        if (sortExpressions.All(x => string.IsNullOrEmpty(x.LastValue))) return;

        builder.Append($"(");
        for (var i = 0; i < sortExpressions.Count; i++)
        {
            if (i > 0)
            {
                builder.Append($" OR ");
            }

            builder.Append($"(");

            for (var j = 0; j <= i; j++)
            {
                var currentSortExpression = sortExpressions[j];
                var currentField = currentSortExpression.Field;
                var currentSortOrder = currentSortExpression.SortOrder;

                if (j > 0)
                {
                    builder.Append($" AND");
                }

                var formattedValue = FormatColumnValue<T>(currentField, currentSortExpression.LastValue);
                var formattedName = TryFormatColumnName(currentField);
                var @operator = GetComparisonOperator(j, i, currentSortOrder);

                if (formattedValue is null && @operator is "=")
                {
                    builder.Append($" {formattedName:raw} IS NULL");
                }
                else
                {
                    var columnInfo = GetColumnInfo<T>(currentField, useTranslit: false);
                    var typeCode = GetTypeCodeForProperty<T>(currentField);

                    if (columnInfo.IsNullAllowed)
                    {
                        var coalesce = GetCoalesceMinValue(typeCode);
                        builder.AppendFormattableString($" COALESCE({formattedName:raw}, {coalesce}) {@operator:raw} COALESCE({formattedValue}, {coalesce})");
                    }
                    else
                    {
                        builder.AppendFormattableString($" {formattedName:raw} {@operator:raw} {formattedValue}");
                    }
                }
            }

            builder.Append($")");
        }
        builder.Append($")");
    }

    private void BuildSortOrder<T>(QueryBuilder builder, SortToken token) where T : class
    {
        builder.Append($"ORDER BY ");

        var added = false;
        for (var index = 0; index < token.SortExpressions.Count; index++)
        {
            var expression = token.SortExpressions[index];

            if (index > 0 && added) builder.Append($",");

            var columnInfo = GetColumnInfo<T>(expression.Field, useTranslit: false);

            var typeCode = GetTypeCodeForProperty<T>(columnInfo.Name);

            var formattedName = TryFormatColumnName(columnInfo.Name);

            if (columnInfo.IsNullAllowed)
            {
                var coalesce = GetCoalesceMinValue(typeCode);
                var nullPosition = expression.SortOrder == SortOrder.Ascending ? "NULLS FIRST" : "NULLS LAST";
                builder.Append($"COALESCE({formattedName:raw}, {coalesce}) {expression.SortOrder.GetQueryFromSortOrder().ToUpper():raw} {nullPosition:raw}");
            }
            else
            {
                builder.Append($"{formattedName:raw} {expression.SortOrder.GetQueryFromSortOrder().ToUpper():raw}");
            }

            added = true;
        }
    }

    private static string GetLeftHandSideExpression(ProcessingType procType, string column, string? jsonPath, TypeCode typeCode)
    {
        var isText = typeCode is TypeCode.String or TypeCode.Char;

        return procType switch
        {
            ProcessingType.JsonArray => Wrap($"(x.val ->> '{column}'){GetJsonFieldType(typeCode)}"),
            ProcessingType.Json => Wrap($"({jsonPath} ->> '{column}'){GetJsonFieldType(typeCode)}"),
            _ => Wrap(column)
        };

        string Wrap(string expr) => isText ? $"generated.transliterate_to_bold_latin({expr})" : expr;
    }

    private void AppendEmptyQueryConditions(QueryBuilderResult result, Type innerType, IsToken token, string leftHandSide, string negation)
    {
        var typeCode = GetTypeCodeForProperty(innerType, token.Field);
        var isText = IsText(typeCode);

        FormattableString query = $"{leftHandSide:raw} IS {negation:raw} NULL";
        result.Builder.AppendFormattableString(query);

        if (!isText) return;

        var join = token.IsNegated ? " AND " : " OR ";
        result.Builder.Append($"{join:raw}");

        query = $"{leftHandSide:raw} {(token.IsNegated ? "<>" : "="):raw} ''";
        result.Builder.AppendFormattableString(query);
    }

    private static void AppendValueQueryCondition(QueryBuilderResult result, IsToken token, string leftHandSide, string negation)
    {
        var value = bool.Parse(token.Value);
        FormattableString query = $"{leftHandSide:raw} IS {negation:raw} {value:raw}";
        result.Builder.AppendFormattableString(query);
    }

    private void HandleJsonArrayFilter(NestedFilterToken token, QueryBuilderResult result, ColumnInfo columnInfo)
    {
        result.Builder.Append($"EXISTS (");
        var formattedArrayQuery = FormattableStringFactory.Create(BaseJsonArrayQuery.ToString().Replace("/**field**/", token.Field));
        var innerBuilder = new QueryBuilder(result.Builder.DbConnection, formattedArrayQuery);
        var innerResult = new QueryBuilderResult { Builder = innerBuilder };

        AppendChildTokens(token, innerResult, columnInfo.Type);

        result.Builder.Append(innerBuilder.Build());
    }

    private void HandleRegularFilter(NestedFilterToken token, QueryBuilderResult result, ColumnInfo columnInfo)
    {
        result.Builder.Append($"(");
        AppendChildTokens(token, result, columnInfo.Type);
    }

    private void AppendChildTokens(NestedFilterToken token, QueryBuilderResult result, Type innerType)
    {
        foreach (var child in token.Tokens)
        {
            result.Builder.Append($" AND ");
            DynamicVisit(child, result, innerType);
        }
    }
    private static FormattableString BaseJsonArrayQuery => @$"
            SELECT 1
            FROM (
                SELECT jsonb_array_elements(/**field**/) AS val
                UNION ALL
                SELECT NULL as val
                WHERE jsonb_array_length(/**field**/) = 0
            ) AS x
            WHERE 1=1 
            /**filters**/ ";

    private static string TryEscapeSpecialChars(ComparisonOperator op)
    {
        return op is ComparisonOperator.StartsWith or
            ComparisonOperator.EndsWith or
            ComparisonOperator.Contains or
            ComparisonOperator.Like ?
            $"ESCAPE '{QueryBuilderHelper.EscapeCharacter}' "
            : "";
    }

    private static object GetCoalesceMinValue(TypeCode typeCode)
    {
        switch (typeCode)
        {
            case TypeCode.String:
            case TypeCode.Char:
                return "''";
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Double:
            case TypeCode.Decimal:
                return -1;
            case TypeCode.DateTime:
                return DateTime.MinValue;
            case TypeCode.Boolean:
                return false;
            default:
                throw new NotSupportedException($"Coalesce fallback value does not exist for type code: {typeCode}");
        }
    }

    private static FormattableString GenerateInQuery(string column, bool isText, InToken token, DbComparison comparison)
    {
        var @operator = token.GetSqlOperator(isText); 
        var comparisonOperator = SqlOperatorProcessor.GetSqlOperator(token.Comparison, isText, token.IsNegated);

        if (!isText)
            return $"{column:raw} {@operator:raw} ({comparison.Value:raw})";

        if (token.Comparison is ComparisonOperator.Equals or ComparisonOperator.In && @operator.Contains("{0}"))
        {
            var formatted = string.Format(@operator, comparison.Value);
            return FormattableStringFactory.Create($"{column} {formatted}");
        }

        var raw = (string)comparison.Value;

        var values = raw
            .Trim('(', ')')
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim('\'', ' '))
            .ToList();

        var conditions = values
            .Select(v => $"{column} {comparisonOperator} '{v}'");

        var combined = string.Join(" OR ", conditions);

        return FormattableStringFactory.Create($"({combined})");
    }


    private void MapPlaceholderToKey<T>(IReadOnlyCollection<SortExpression> sortExpressions) where T : class
    {
        if (sortExpressions == null) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_MissingSortExpressions.Code);

        var keyExpressions = sortExpressions.Where(x => x.Field == QueryConstants.KeyPlaceholder);
        foreach (var keyExpression in keyExpressions)
        {
            keyExpression.Field = GetEntityKey<T>();
        }
    }

    private static string GetComparisonOperator(int j, int i, SortOrder sortOrder)
    {
        if (j == i)
        {
            return sortOrder == SortOrder.Ascending ? ">" : "<";
        }

        return "=";
    }

    private static void TryHandleNullColumns(string column, INegationAware token, ref FormattableString query)
    {
        if (!token.IsNegated) return;

        query = $"(({query}) OR {column:raw} IS NULL)";
    }

    private void ProcessChildToken<T>(Token token, QueryBuilderResult result) where T : class => token.Accept<T>(this, result);

    private ColumnInfo GetColumnInfo<T>(string field, bool useTranslit = true) where T : class => metadata.GetColumnInfo<T>(field, useTranslit);

    private ColumnInfo GetColumnInfo(Type type, string field, bool useTranslit = true) => metadata.GetColumnInfo(type, field, useTranslit);

    private TypeCode GetTypeCodeForProperty<T>(string field) where T : class => metadata.GetTypeCodeForProperty<T>(field);

    private TypeCode GetTypeCodeForProperty(Type type, string field) => metadata.GetTypeCodeForProperty(type, field);

    private string GetEntityKey<T>() where T : class => metadata.GetEntityKey<T>();

    private DbComparison GenerateDatabaseComparison(Type type, ComparisonToken token)
    {
        var columnInfo = GetColumnInfo(type, token.Field);
        var typeCode = GetTypeCodeForProperty(type, token.Field);

        ComparisonValidator.EnsureTypeCodeOperatorValid(typeCode, token.Operator);
        var formattedValue = FormatComparisonValue(token.Value, typeCode, token.Operator, columnInfo.IsTranslit);
        var formattedName = TryFormatColumnName(columnInfo.Name);

        return new DbComparison(formattedName, formattedValue);
    }

    private DbComparison GenerateDatabaseComparison(Type type, InToken token)
    {
        var columnInfo = GetColumnInfo(type, token.Field);
        var typeCode = GetTypeCodeForProperty(type, token.Field);

        var values = token.Values.Trim('(', ')').Split(',', StringSplitOptions.RemoveEmptyEntries);

        var formattedValues = values
            .Select(v => FormatComparisonValue(v, typeCode, token.Comparison, columnInfo.IsTranslit))
            .ToArray();

        var formattedValue = JoinInArray(",", typeCode, formattedValues);
        var formattedName = TryFormatColumnName(columnInfo.Name);

        return new DbComparison(formattedName, formattedValue);
    }



    private static string JoinInArray(string separator, TypeCode typeCode, params object[] values)
    {
        var stringBuilder = new StringBuilder();

        for (var i = 0; i < values.Length; i++)
        {
            switch (typeCode)
            {
                case TypeCode.String:
                    stringBuilder.Append($"'{values[i]}'");
                    break;
                case TypeCode.DateTime:
                    stringBuilder.Append($"'{values[i]:yyyy-MM-dd HH:mm:ss.fffffff}'");
                    break;
                default:
                    stringBuilder.Append(values[i]);
                    break;
            }

            if (i != values.Length - 1)
            {
                stringBuilder.Append(separator);
            }
        }

        return stringBuilder.ToString();
    }

    private dynamic FormatComparisonValue(string value, TypeCode typeCode, ComparisonOperator comparison, bool isTranslit, bool isSort = false)
    {
        return typeCode switch
        {
            TypeCode.String => QueryBuilderHelper.MapComparisonToNpgsqlString(comparison, value, isTranslit, isSort),
            TypeCode.DateTime => DateTime.TryParse(value, out var date) ? date : throw new ArgumentException($"Cannot format value {value} as DateTime"),
            TypeCode.Int16 => short.TryParse(value, out var shortValue) ? shortValue : throw new ArgumentException($"Cannot format value {value} as Int16"),
            TypeCode.Int32 => int.TryParse(value, out var intValue) ? intValue : throw new ArgumentException($"Cannot format value {value} as Int32"),
            TypeCode.Int64 => long.TryParse(value, out var longValue) ? longValue : throw new ArgumentException($"Cannot format value {value} as Int64"),
            TypeCode.Double => double.TryParse(value, out var doubleValue) ? doubleValue : throw new ArgumentException($"Cannot format value {value} as Double"),
            TypeCode.Boolean => bool.TryParse(value, out var boolValue) ? boolValue : throw new ArgumentException($"Cannot format value {value} as Boolean"),
            TypeCode.Char => char.TryParse(value, out var charValue) ? Transliteration.ToLowerBoldLatin(charValue.ToString()) : throw new ArgumentException($"Cannot format value {value} as Char"),
            TypeCode.Decimal => decimal.TryParse(value, out var decimalValue) ? decimalValue : throw new ArgumentException($"Cannot format value {value} as Decimal"),
            _ => throw new ArgumentException($"Cannot format values for TypeCode {typeCode}")
        };
    }

    private dynamic? FormatColumnValue<T>(string field, string? value) where T : class
    {
        var typeCode = GetTypeCodeForProperty<T>(field);

        if (string.IsNullOrEmpty(value))
        {
            if (typeCode == TypeCode.String)
                return value == null ? null : string.Empty;
            return null;
        }

        return typeCode switch
        {
            TypeCode.String => Transliteration.CyrlToLatin(value),
            TypeCode.DateTime => DateTime.TryParse(value, out var date) ? (DateTime?)date : throw new ArgumentException($"Cannot format value {value} as DateTime"),
            TypeCode.Int16 => short.TryParse(value, out var shortValue) ? (short?)shortValue : throw new ArgumentException($"Cannot format value {value} as Int16"),
            TypeCode.Int32 => int.TryParse(value, out var intValue) ? (int?)intValue : throw new ArgumentException($"Cannot format value {value} as Int32"),
            TypeCode.Int64 => long.TryParse(value, out var longValue) ? (long?)longValue : throw new ArgumentException($"Cannot format value {value} as Int64"),
            TypeCode.Double => double.TryParse(value, out var doubleValue) ? (double?)doubleValue : throw new ArgumentException($"Cannot format value {value} as Double"),
            TypeCode.Boolean => bool.TryParse(value, out var boolValue) ? (bool?)boolValue : throw new ArgumentException($"Cannot format value {value} as Boolean"),
            TypeCode.Char => char.TryParse(value, out var charValue) ? Transliteration.ToBoldLatin(charValue.ToString()) : throw new ArgumentException($"Cannot format value {value} as Char"),
            TypeCode.Decimal => decimal.TryParse(value, out var decimalValue) ? (decimal?)decimalValue : throw new ArgumentException($"Cannot format value {value} as Decimal"),
            _ => throw new ArgumentException($"Cannot format values for TypeCode {typeCode}")
        };
    }

    private void EnsureMandatoryFieldsInSelectionList<T>(SelectToken token) where T : class
    {
        if (token.Fields.Any(x => x.Equals(QueryConstants.SelectAsterisk, StringComparison.CurrentCultureIgnoreCase))) return;

        var primaryKey = GetEntityKey<T>();

        if (!token.Fields.Any(x => x.Equals(primaryKey, StringComparison.CurrentCultureIgnoreCase)))
        {
            token.Fields.Add(primaryKey);
        }
    }

    private static bool IsText(TypeCode typeCode)
    {
        return typeCode switch
        {
            TypeCode.Char => true,
            TypeCode.String => true,
            _ => false
        };
    }

    private void AssertFieldsSortable<T>(IEnumerable<SortExpression> sortExpressions) where T : class
    {
        if (sortExpressions.Any(sortExpression => !metadata.IsFieldSortable<T>(sortExpression.Field)))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_ColumnNotSortable.Code);
        }
    }

    private static string TryFormatColumnName(string columnName)
    {
        var reservedWords = new HashSet<string>
        {
            "ALL", "ANALYSE", "ANALYZE", "AND", "ANY", "ARRAY", "AS", "ASC", "ASYMMETRIC", "BOTH",
            "CASE", "CAST", "CHECK", "COLLATE", "COLUMN", "CONSTRAINT", "CREATE", "CURRENT_CATALOG",
            "CURRENT_DATE", "CURRENT_ROLE", "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURRENT_USER",
            "DEFAULT", "DEFERRABLE", "DESC", "DISTINCT", "DO", "ELSE", "END", "EXCEPT", "FALSE",
            "FETCH", "FOR", "FOREIGN", "FROM", "GRANT", "GROUP", "HAVING", "IN", "INITIALLY",
            "INTERSECT", "INTO", "LEADING", "LIMIT", "LOCALTIME", "LOCALTIMESTAMP", "NEW", "NOT",
            "NULL", "OFFSET", "OLD", "ON", "ONLY", "OR", "ORDER", "PLACING", "PRIMARY", "REFERENCES",
            "RETURNING", "SELECT", "SESSION_USER", "SOME", "SYMMETRIC", "TABLE", "THEN", "TO",
            "TRAILING", "TRUE", "UNION", "UNIQUE", "USER", "USING", "VARIADIC", "WHEN", "WHERE",
            "WINDOW", "WITH"
        };

        return reservedWords.Contains(columnName.ToUpper()) ? $"\"{columnName.PascalToCamelCase()}\"" : columnName.PascalToCamelCase();
    }

    private static Type GetArrayElementTypeOrThrow<T>(string fieldName, out Type propertyType)
    {
        var type = typeof(T);
        Type? elementType;

        if (type == typeof(string)) // Special case
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_PropertyTypeUnknown.Code);
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType();
        }
        else if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
        {
            elementType = type.GetGenericArguments().FirstOrDefault();
        }
        else if (typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string))
        {
            elementType = type
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments().FirstOrDefault();
        }
        else
        {
            elementType = type;
        }

        if (elementType == null)
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_PropertyTypeUnknown.Code);

        var property = elementType.GetProperty(
            fieldName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase
        );

        if (property == null)
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_PropertyTypeUnknown.Code);

        propertyType = property.PropertyType;
        return elementType;
    }

    private static void ProcessSimpleTypeArray(ArrayOperationToken token, QueryBuilderResult result, Type valueType)
    {
        var elementType = GetElementTypeOrSelf(valueType);
        var arrayTypeSpecifier = GetPostgresArrayType(elementType);
        var valuesFormatted = FormatArrayValues(token.Values, elementType);

        if (!valuesFormatted.Any())
            return;

        var isDatabaseFieldArray = valueType != typeof(string) && typeof(IEnumerable).IsAssignableFrom(valueType);

        var filterSql = token.Operator switch
        {
            ArrayOperator.Include when isDatabaseFieldArray =>
                $"{token.Field}{arrayTypeSpecifier} @> ARRAY[{valuesFormatted}]{arrayTypeSpecifier}",
            ArrayOperator.Exclude when isDatabaseFieldArray =>
                $"NOT ({token.Field}{arrayTypeSpecifier} && ARRAY[{valuesFormatted}]{arrayTypeSpecifier})",

            ArrayOperator.Include =>
                $"{token.Field} = ANY(ARRAY[{valuesFormatted}]{arrayTypeSpecifier})",
            ArrayOperator.Exclude =>
                $"{token.Field} != ALL(ARRAY[{valuesFormatted}]{arrayTypeSpecifier})",

            _ => throw new NotSupportedException($"Unsupported array operator: {token.Operator}")
        };

        if (token.IsNegated)
            result.Builder.Append($" NOT ({filterSql:raw})");
        else
            result.Builder.Append($" {filterSql:raw}");
    }

    private static Type GetElementTypeOrSelf(Type type)
    {
        if (type == typeof(string)) return type;

        if (type.IsArray)
            return type.GetElementType()!;

        if (type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type))
            return type.GetGenericArguments().First();

        return type;
    }

    private static string FormatArrayValues(IEnumerable<object> values, Type type)
    {
        var array = type == typeof(string) ? values.Select(v => $"'{Transliteration.CyrlToLatin(v.ToString())}'").ToList() : values.Select(v => v.ToString());
        return string.Join(", ", array);
    }

    private static void ProcessComplexTypeArray(ArrayOperationToken token, QueryBuilderResult result, Type arrayType)
    {
        var arrayTypeSpecifier = GetPostgresArrayType(arrayType);
        var valuesFormatted = FormatArrayValues(token.Values, arrayType);

        if (!valuesFormatted.Any())
            return;

        var leftHandSideArray = $"ARRAY(SELECT x ->> '{token.Field}' FROM jsonb_array_elements({token.JsonPath}) as x){arrayTypeSpecifier}";

        var filterSql = token.Operator switch
        {
            ArrayOperator.Include => $"{leftHandSideArray} @> ARRAY[{valuesFormatted}]{arrayTypeSpecifier}",
            ArrayOperator.Exclude => $"NOT ({leftHandSideArray} && ARRAY[{valuesFormatted}]{arrayTypeSpecifier}) OR jsonb_array_length({token.JsonPath}) = 0",
            _ => throw new NotSupportedException($"Unsupported array operator: {token.Operator}")
        };

        if (token.IsNegated)
        {
            result.Builder.Append($"NOT ({filterSql:raw})");
        }
        else
        {
            result.Builder.Append($"{filterSql:raw}");
        }
    }

    private static string GetJsonFieldType(TypeCode typeCode)
    {
        return typeCode.IsNumericType() ? "::int" : "::text";
    }

    private static string GetPostgresArrayType(Type type)
    {
        if (type == typeof(int))
            return "::int[]";
        if (type == typeof(string))
            return "::text[]";
        if (type == typeof(decimal))
            return "::numeric[]";
        throw new ArgumentException("Unsupported array type.");
    }

    private void DynamicVisit(Token token, QueryBuilderResult result, Type type)
    {
        var method = GetType().GetMethod("Visit", BindingFlags.Public | BindingFlags.Instance, null, [token.GetType(), typeof(QueryBuilderResult)], null) ?? throw new InvalidOperationException("Visit method not found.");
        var genericMethod = method.MakeGenericMethod(type);

        var invokeResult = genericMethod.Invoke(this, [token, result]);
        if (invokeResult is not QueryBuilderResult)
        {
            throw new InvalidOperationException("The invoked Visit method returned null or an unexpected type.");
        }
    }

    private static (ProcessingType, Type) DetermineProcessingType(Type type, string? jsonPath)
    {
        var isJson = !string.IsNullOrEmpty(jsonPath);
        var isJsonArray = isJson && typeof(IEnumerable).IsAssignableFrom(type) && type != typeof(string);
        var innerType = isJsonArray ? type.GetGenericArguments()[0] : type;

        return (isJsonArray ? ProcessingType.JsonArray : isJson ? ProcessingType.Json : ProcessingType.Regular, innerType);
    }
    #endregion
}

public enum ProcessingType
{
    Regular,
    Json,
    JsonArray
}