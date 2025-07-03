using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using AspNet.Transliterator;
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
using System.Globalization;

namespace _1Dev.Pagin8.Internal.Visitors;
public class LinqTokenVisitor<T>(IPagin8MetadataProvider metadata, IDateProcessor dateProcessor)
    : ILinqTokenVisitor<T>
    where T : class
{
    #region Private members

    private readonly ParameterExpression _parameter = Expression.Parameter(typeof(T), "x");

    #endregion

    #region Public methods

    public IQueryable<T> Visit(ComparisonToken token, IQueryable<T> queryable)
    {
        var body = GenerateComparisonExpression(token);
        var lambda = Expression.Lambda<Func<T, bool>>(body, _parameter);
        return queryable.Where(lambda);
    }

    private Expression GenerateComparisonExpression(ComparisonToken token)
    {
        var comparison = GenerateDatabaseComparison(token);
        var typeCode = GetTypeCodeForProperty(token.Field);
        var isText = IsText(typeCode);
        var property = GetPropertyInfo(typeof(T), comparison.Column);
        var propertyType = property.PropertyType;

        var left = Expression.Property(_parameter, property);

        Expression leftProcessed = propertyType == typeof(string)
            ? Expression.Call(typeof(Transliteration).GetMethod(nameof(Transliteration.ToLowerBoldLatin), [typeof(string)])!, left)
            : left;

        Expression right;
        if (propertyType.IsEnum)
        {
            var enumValue = Enum.Parse(propertyType, token.Value, ignoreCase: true);
            right = Expression.Constant(enumValue);
        }
        else if (isText)
        {
            right = Expression.Constant(comparison.Value, typeof(string));
        }
        else
        {
            right = ConvertToExpression(comparison.Value, propertyType);
        }

        if (propertyType == typeof(DateTime))
        {
            leftProcessed = Expression.Property(leftProcessed, GetPropertyInfo(typeof(DateTime), "Date"));
            right = Expression.Property(right, GetPropertyInfo(typeof(DateTime), "Date"));
        }

        var comparisonExpression = LinqOperatorProcessor.GetLinqExpression(token.Operator, isText, leftProcessed, right);

        return token.IsNegated ? Expression.Not(comparisonExpression) : comparisonExpression;
    }

    public IQueryable<T> Visit(GroupToken groupToken, IQueryable<T> queryable)
    {
        Expression? combinedExpression = null;

        foreach (var token in groupToken.Tokens)
        {
            var tokenResult = token.Accept(this, queryable);
            var expressionToAdd = tokenResult.Expression.ExtractLambda<T>();


            var adjustedExpressionToAdd = new ReplaceExpressionVisitor(expressionToAdd.Parameters[0], _parameter).Visit(expressionToAdd.Body);

            if (combinedExpression == null)
            {
                combinedExpression = adjustedExpressionToAdd;
            }
            else
            {
                combinedExpression = groupToken.NestingOperator switch
                {
                    NestingOperator.And => Expression.AndAlso(combinedExpression, adjustedExpressionToAdd),
                    NestingOperator.Or => Expression.OrElse(combinedExpression, adjustedExpressionToAdd),
                    _ => combinedExpression
                };
            }
        }

        if (combinedExpression == null)
        {
            return queryable;
        }

        if (groupToken.IsNegated)
        {
            combinedExpression = Expression.Not(combinedExpression);
        }

        var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression, _parameter);
        return queryable.Where(lambda);
    }


    public IQueryable<T> Visit(InToken token, IQueryable<T> queryable)
    {
        var property = GetPropertyExpression(token.Field);
        var type = property.Type;
        var isText = type == typeof(string);

        var values = token.Values
            .Trim('(', ')')
            .Split(',')
            .Select(v => v.Trim())
            .ToList();

        Expression body;

        if (isText && token.Comparison is ComparisonOperator.StartsWith or
                                    ComparisonOperator.EndsWith or
                                    ComparisonOperator.Contains or
                                    ComparisonOperator.Like)
        {
            var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));

            var orExpressions = new List<Expression>();

            foreach (var value in values)
            {
                var translatedValue = Transliteration.ToLowerBoldLatin(value);

                var left = Expression.Call(
                    typeof(Transliteration).GetMethod(nameof(Transliteration.ToLowerBoldLatin), [typeof(string)])!,
                    property
                );
                var right = Expression.Constant(translatedValue, typeof(string));

                var methodName = token.Comparison switch
                {
                    ComparisonOperator.StartsWith => nameof(string.StartsWith),
                    ComparisonOperator.EndsWith => nameof(string.EndsWith),
                    ComparisonOperator.Contains => nameof(string.Contains),
                    ComparisonOperator.Like => nameof(string.Contains),
                    _ => throw new NotSupportedException($"Unsupported operator {token.Comparison}")
                };

                var method = typeof(string).GetMethod(methodName, [typeof(string), typeof(StringComparison)])!;
                var call = Expression.Call(left, method, right, Expression.Constant(StringComparison.OrdinalIgnoreCase));

                orExpressions.Add(call);
            }

            var comparisonExpression = orExpressions.Count switch
            {
                0 => Expression.Constant(false),
                1 => orExpressions[0],
                _ => orExpressions.Skip(1).Aggregate(orExpressions[0], Expression.OrElse)
            };

            body = Expression.AndAlso(notNull, comparisonExpression);
        }
        else
        {
            var valuesArray = values.Select(v => SafeConvert(v, type)).ToArray();
            var array = Array.CreateInstance(type, valuesArray.Length);
            Array.Copy(valuesArray, array, valuesArray.Length);

            Expression valueToCheck;

            if (isText)
            {
                var translitMethod = typeof(Transliteration).GetMethod(nameof(Transliteration.ToLowerBoldLatin), [typeof(string)])!;
                var translitCall = Expression.Call(translitMethod, property);

                valueToCheck = Expression.Convert(translitCall, typeof(string));
            }
            else
            {
                valueToCheck = property;
            }


            var containsMethod = typeof(Enumerable)
                .GetMethods(BindingFlags.Static | BindingFlags.Public)
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(type);

            var valuesExpression = Expression.Constant(array, array.GetType());
            var containsCall = Expression.Call(null, containsMethod, valuesExpression, valueToCheck);

            body = isText
                ? Expression.AndAlso(Expression.NotEqual(property, Expression.Constant(null, typeof(string))), containsCall)
                : containsCall;
        }

        if (token.IsNegated)
            body = Expression.Not(body);

        var lambda = Expression.Lambda<Func<T, bool>>(body, _parameter);
        return queryable.Where(lambda);
    }

    public IQueryable<T> Visit(SortToken token, IQueryable<T> queryable)
    {
        MapPlaceholderToKey(token.SortExpressions);
        AssertFieldsSortable(token.SortExpressions);
        queryable = ApplyKeySetPagination(queryable, token.SortExpressions);
        queryable = ApplySorting(queryable, token);
        return queryable;
    }

    public IQueryable<T> Visit(LimitToken token, IQueryable<T> queryable)
    {
        if (token is not { Value: > 0 })
        {
            throw new ArgumentException("LimitToken must have a positive value.");
        }

        return queryable.Take(token.Value);
    }

    public IQueryable<T> Visit(SelectToken token, IQueryable<T> queryable)
    {
        // If all properties are selected, return the original query.
        if (token.Fields.Contains(QueryConstants.SelectAsterisk)) return queryable;

        var elementInits = new List<ElementInit>();

        var addMethod = typeof(Dictionary<string, object>).GetMethod("Add");

        foreach (var fieldName in token.Fields)
        {
            var property = typeof(T).GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property == null) throw new ArgumentException($"Property '{fieldName}' not found on type '{typeof(T).Name}'.");

            var keyValue = Expression.Constant(fieldName);
            var propertyAccess = Expression.Property(_parameter, property);
            var convertedPropertyAccess = Expression.Convert(propertyAccess, typeof(object));

            var elementInit = Expression.ElementInit(addMethod!, keyValue, convertedPropertyAccess);
            elementInits.Add(elementInit);
        }

        var dictionaryAddExpressions = Expression.ListInit(Expression.New(typeof(Dictionary<string, object>)), elementInits);
        _ = Expression.Lambda<Func<T, Dictionary<string, object>>>(dictionaryAddExpressions, _parameter);

        // Do nothing for now, just return original, need to figure out return type mismatch - N.Z
        return queryable;
    }


    public IQueryable<T> Visit(PagingToken token, IQueryable<T> queryable)
    {
        token.Sort?.Accept(this, queryable);
        token.Limit?.Accept(this, queryable);
        token.Count?.Accept(this, queryable);

        return queryable;
    }

    public IQueryable<T> Visit(ShowCountToken token, IQueryable<T> queryable)
    {
        return queryable;
    }

    public IQueryable<T> Visit(DateRangeToken token, IQueryable<T> queryable)
    {
        var columnName = GetColumnInfo(token.Field).Name;
        var propertyOrField = GetPropertyExpression(columnName);
        var currentDate = DateTime.Now;
        var goBackwards = token.Operator is DateRangeOperator.Ago;

        var (startDate, endDate) = dateProcessor.GetStartAndEndOfRelativeDate(currentDate, token.Value, token.Range, goBackwards, token.Exact, token.Strict);

        Expression combinedExpression;
        if (!token.IsNegated)
        {
            // BETWEEN
            var greaterThanOrEqual = CreateBinary(propertyOrField, startDate, Expression.GreaterThanOrEqual);
            var lessThanOrEqual = CreateBinary(propertyOrField, endDate, Expression.LessThanOrEqual);
            combinedExpression = Expression.AndAlso(greaterThanOrEqual, lessThanOrEqual);
        }
        else
        {
            // NOT BETWEEN
            var lessThan = CreateBinary(propertyOrField, startDate, Expression.LessThan);
            var greaterThan = CreateBinary(propertyOrField, endDate, Expression.GreaterThan);
            Expression mainExpression = Expression.OrElse(lessThan, greaterThan);

            // Handle nulls
            Expression checkForNull = Expression.Equal(propertyOrField, Expression.Constant(null, propertyOrField.Type));
            combinedExpression = Expression.OrElse(mainExpression, checkForNull);
        }

        var lambda = Expression.Lambda<Func<T, bool>>(combinedExpression, _parameter);
        return queryable.Where(lambda);

        Expression CreateBinary(Expression left, object value, Func<Expression, Expression, BinaryExpression> expressionFunc)
        {
            var right = GetNullableSafeConstant(left, value);
            return expressionFunc(left, right);
        }

        Expression GetNullableSafeConstant(Expression property, object value)
        {
            var targetType = property.Type.IsGenericType &&
                             property.Type.GetGenericTypeDefinition() == typeof(Nullable<>)
                ? property.Type
                : value.GetType();

            return Expression.Constant(value, targetType);
        }
    }

    public IQueryable<T> Visit(IsToken token, IQueryable<T> queryable)
    {
        Expression body;

        var property = GetPropertyExpression(token.Field);

        if (token.IsEmptyQuery)
        {
            var nullCheck = Expression.Equal(property, Expression.Constant(null, property.Type));

            if (property.Type == typeof(string))
            {
                var emptyStringCheck = Expression.Equal(property, Expression.Constant(string.Empty));

                body = token.IsNegated ? Expression.AndAlso(nullCheck, emptyStringCheck)
                    : Expression.OrElse(nullCheck, emptyStringCheck);
            }
            else
            {
                body = nullCheck;
            }
        }
        else
        {
            var value = bool.Parse(token.Value);
            var valueCheck = Expression.Equal(property, Expression.Constant(value));

            body = valueCheck;
        }

        if (token.IsNegated)
        {
            body = Expression.Not(body);
        }

        var lambda = Expression.Lambda<Func<T, bool>>(body, _parameter);
        return queryable.Where(lambda);
    }

    public IQueryable<T> Visit(MetaIncludeToken token, IQueryable<T> queryable)
    {
        return queryable;
    }

    public IQueryable<T> Visit(NestedFilterToken token, IQueryable<T> queryable)
    {
        throw new NotImplementedException();
    }

    public IQueryable<T> Visit(ArrayOperationToken token, IQueryable<T> queryable)
    {
        var propertyInfo = GetPropertyInfo(typeof(T), token.Field);
        if (!typeof(IEnumerable).IsAssignableFrom(propertyInfo.PropertyType)) throw new InvalidOperationException($"Property {token.Field} is not an IEnumerable.");

        var elementType = propertyInfo.PropertyType.GetGenericArguments()[0];
        return ProcessArrayOperation(token, queryable, propertyInfo, elementType);

    }

    #endregion

    #region Private methods

    private PropertyInfo GetPropertyInfo(Type type, string propertyName)
    {
        return type.GetProperty(propertyName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance) ?? throw new InvalidOperationException($"Unable to find property {propertyName} on type {typeof(T).Name}.");
    }

    private ColumnInfo GetColumnInfo(string field, bool useTranslit = true) => metadata.GetColumnInfo<T>(field, useTranslit);

    private TypeCode GetTypeCodeForProperty(string field) => metadata.GetTypeCodeForProperty<T>(field);

    private string GetEntityKey() => metadata.GetEntityKey<T>();

    private DbComparison GenerateDatabaseComparison(ComparisonToken token)
    {
        var typeCode = GetTypeCodeForProperty(token.Field);

        ComparisonValidator.EnsureTypeCodeOperatorValid(typeCode, token.Operator);
        var formattedValue = FormatComparisonValue(token.Value, typeCode);

        return new DbComparison(token.Field, formattedValue);
    }

    private dynamic FormatComparisonValue(string value, TypeCode typeCode)
    {
        return typeCode switch
        {
            TypeCode.String => Transliteration.ToLowerBoldLatin(value),
            TypeCode.DateTime => DateTime.TryParse(value, out var date) ? date : throw new ArgumentException($"Cannot format value {value} as DateTime"),
            TypeCode.Int16 => short.TryParse(value, out var shortValue) ? shortValue : throw new ArgumentException($"Cannot format value {value} as Int16"),
            TypeCode.Int32 => int.TryParse(value, out var intValue) ? intValue : throw new ArgumentException($"Cannot format value {value} as Int32"),
            TypeCode.Int64 => long.TryParse(value, out var longValue) ? longValue : throw new ArgumentException($"Cannot format value {value} as Int64"),
            TypeCode.Double => double.TryParse(value, out var doubleValue) ? doubleValue : throw new ArgumentException($"Cannot format value {value} as Double"),
            TypeCode.Boolean => bool.TryParse(value, out var boolValue) ? boolValue : throw new ArgumentException($"Cannot format value {value} as Boolean"),
            TypeCode.Char => char.TryParse(value, out var charValue) ? Transliteration.ToLowerBoldLatin(charValue.ToString()) : throw new ArgumentException($"Cannot format value {value} as Char"),
            TypeCode.Decimal => decimal.TryParse(value, CultureInfo.InvariantCulture, out var decimalValue) ? decimalValue : throw new ArgumentException($"Cannot format value {value} as Decimal"),
            _ => throw new ArgumentException($"Cannot format values for TypeCode {typeCode}")
        };
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

    private IQueryable<T> ApplySorting(IQueryable<T> queryable, SortToken token)
    {
        if (token.SortExpressions.Count == 0) return queryable;

        var firstExpression = token.SortExpressions[0];
        var parameter = Expression.Parameter(typeof(T), "x");
        var firstLambda = Expression.Lambda<Func<T, object>>(
            Expression.Convert(GetPropertyExpression(firstExpression.Field), typeof(object)),
            parameter);

        queryable = firstExpression.SortOrder == SortOrder.Ascending
            ? queryable.OrderBy(firstLambda)
            : queryable.OrderByDescending(firstLambda);

        for (var index = 1; index < token.SortExpressions.Count; index++)
        {
            var expression = token.SortExpressions[index];
            var lambda = Expression.Lambda<Func<T, object>>(
                Expression.Convert(GetPropertyExpression(expression.Field), typeof(object)),
                parameter);

            queryable = expression.SortOrder == SortOrder.Ascending
                ? ((IOrderedQueryable<T>)queryable).ThenBy(lambda) :
                ((IOrderedQueryable<T>)queryable).ThenByDescending(lambda);
        }

        return queryable;
    }

    private MemberExpression GetPropertyExpression(string propertyName)
    {
        return Expression.PropertyOrField(_parameter, propertyName);
    }

    private IQueryable<T> ApplyKeySetPagination(IQueryable<T> queryable, IReadOnlyList<SortExpression> sortExpressions)
    {
        if (sortExpressions.All(x => string.IsNullOrEmpty(x.LastValue))) return queryable;

        var combinedOrExpressions = sortExpressions.Select((_, i) =>
        {
            var andConditions = Enumerable.Range(0, i + 1).Select(j =>
            {
                var currentSortExpression = sortExpressions[j];

                ComparisonOperator op;
                if (j < i)
                {
                    op = ComparisonOperator.Equals;
                }
                else
                {
                    op = currentSortExpression.SortOrder == SortOrder.Ascending
                        ? ComparisonOperator.GreaterThan
                        : ComparisonOperator.LessThan;
                }
                return GenerateComparisonExpression(new ComparisonToken(currentSortExpression.Field, op, currentSortExpression.LastValue, false, 2, null));
            });

            return andConditions.Aggregate(Expression.AndAlso);
        });

        var combinedExpression = combinedOrExpressions.Aggregate(Expression.OrElse);

        var finalLambda = Expression.Lambda<Func<T, bool>>(combinedExpression, _parameter);
        return queryable.Where(finalLambda);
    }

    private void MapPlaceholderToKey(IReadOnlyCollection<SortExpression> sortExpressions)
    {
        if (sortExpressions == null) throw new Pagin8Exception(Pagin8StatusCode.Pagin8_MissingSortExpressions.Code);

        var keyExpressions = sortExpressions.Where(x => x.Field == "$key");
        foreach (var keyExpression in keyExpressions)
        {
            keyExpression.Field = GetEntityKey();
        }
    }

    private void AssertFieldsSortable(IEnumerable<SortExpression> sortExpressions)
    {
        if (sortExpressions.Any(sortExpression => !metadata.IsFieldSortable<T>(sortExpression.Field)))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_ColumnNotSortable.Code);
        }
    }

    private static Expression ConvertToExpression(object value, Type targetType)
    {
        var convertedValue = SafeConvert(value, targetType);
        return Expression.Constant(convertedValue, targetType);
    }


    private static object? SafeConvert(object? value, Type targetType)
    {
        if (targetType == null)
        {
            throw new ArgumentNullException(nameof(targetType));
        }

        if (value == null)
        {
            return null;
        }

        var actualType = targetType.IsNullable() ? Nullable.GetUnderlyingType(targetType)! : targetType;

        if (actualType == typeof(decimal))
        {
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        return Convert.ChangeType(value, actualType, CultureInfo.InvariantCulture);
    }

    private static IQueryable<T> ProcessArrayOperation(ArrayOperationToken token, IQueryable<T> queryable, PropertyInfo propertyInfo, Type elementType)
    {
        var values = token.Values.Select(v => Convert.ChangeType(v, elementType)).ToList();
        if (values.Count == 0)
            return queryable; // or throw?

        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyExpression = Expression.Property(parameter, propertyInfo);

        var body = token.Operator switch
        {
            ArrayOperator.Include => values.Aggregate((Expression)null, (current, value) =>
                current == null
                    ? Expression.Call(typeof(Enumerable), "Contains", [elementType], propertyExpression, Expression.Constant(value))
                    : Expression.AndAlso(current, Expression.Call(typeof(Enumerable), "Contains", [elementType], propertyExpression, Expression.Constant(value)))),

            ArrayOperator.Exclude => values.Aggregate((Expression)null, (current, value) =>
                current == null ?
                    Expression.Not(Expression.Call(typeof(Enumerable), "Contains", [elementType], propertyExpression, Expression.Constant(value))) :
                    Expression.AndAlso(current, Expression.Not(Expression.Call(typeof(Enumerable), "Contains", [elementType], propertyExpression, Expression.Constant(value))))
                ),


            _ => throw new NotSupportedException($"Unsupported operation type: {token.Operator}")
        };

        if (body == null)
            return queryable;

        var finalBody = token.IsNegated ? Expression.Not(body) : body;

        var lambda = Expression.Lambda<Func<T, bool>>(finalBody, parameter);
        return queryable.Where(lambda);
    }

    #endregion

    private class ReplaceExpressionVisitor(Expression oldValue, Expression newValue) : ExpressionVisitor
    {
        public override Expression Visit(Expression node)
        {
            if (node == oldValue)
                return newValue;
            return base.Visit(node);
        }
    }
}