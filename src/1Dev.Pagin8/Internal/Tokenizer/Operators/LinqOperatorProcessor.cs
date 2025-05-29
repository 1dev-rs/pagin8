using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace _1Dev.Pagin8.Internal.Tokenizer.Operators;
public static class LinqOperatorProcessor
{
    #region PublicMethods
    
    public static Expression GetLinqExpression(ComparisonOperator op, bool isText, Expression left, Expression right)
    {
        Expression body;
        switch (op)
        {
            case ComparisonOperator.Equals:
                body = Expression.Equal(left, right);
                break;
            case ComparisonOperator.GreaterThan:
            case ComparisonOperator.GreaterThanOrEqual:
            case ComparisonOperator.LessThan:
            case ComparisonOperator.LessThanOrEqual:
                body = GetComparisonExpression(op, left, right);
                break;

            case ComparisonOperator.Contains:
            case ComparisonOperator.StartsWith:
            case ComparisonOperator.EndsWith:
                if (!isText)
                    throw new InvalidOperationException("String methods can only be applied to string types.");

                if (left.Type != typeof(string))
                    throw new InvalidOperationException("Left operand should be of type string.");

                var method = GetMethodInfo(op);
                var comparisonType = Expression.Constant(StringComparison.OrdinalIgnoreCase);

                var methodCall = Expression.Call(left, method, right, comparisonType);

                // wrap in null check: left != null && left.Method() - N.Z
                var notNull = Expression.NotEqual(left, Expression.Constant(null, typeof(string)));
                body = Expression.AndAlso(notNull, methodCall);
                break;
            case ComparisonOperator.Like:
                if (!isText)
                    throw new InvalidOperationException("LIKE can only be applied to string types.");

                if (left.Type != typeof(string))
                    throw new InvalidOperationException("Left operand must be of type string.");

                if (right is not ConstantExpression { Value: string likePattern })
                    throw new InvalidOperationException("Right operand must be a constant string expression.");

                // Convert SQL LIKE pattern to regex pattern - N.Z
                var regexPattern = SqlLikeToRegex(likePattern);

                var regexMatchCall = Expression.Call(
                    typeof(Regex).GetMethod(nameof(Regex.IsMatch), [typeof(string), typeof(string), typeof(RegexOptions)])!,
                    left,
                    Expression.Constant(regexPattern),
                    Expression.Constant(RegexOptions.IgnoreCase)
                );

                // wrap in null check: left != null && left.Method() - N.Z
                var notNullCheck = Expression.NotEqual(left, Expression.Constant(null, typeof(string)));
                body = Expression.AndAlso(notNullCheck, regexMatchCall);
                break;

            default:
                throw new NotImplementedException($"Operator {op} is not implemented.");
        }

        return body;
    }


    #endregion

    #region Private methods

    private static string SqlLikeToRegex(string likePattern)
    {
        var sb = new StringBuilder("^");

        foreach (char c in likePattern)
        {
            sb.Append(c switch
            {
                '_' => ".",       
                '%' => ".*",      
                '\\' => "\\\\", 
                '.' or '$' or '^' or '{' or '[' or '(' or '|' or ')' or '*' or '+' or '?' => $"\\{c}", // Escape regex metacharacters
                _ => c.ToString()
            });
        }

        sb.Append("$");
        return sb.ToString();
    }

    private static Expression GetComparisonExpression(ComparisonOperator op, Expression left, Expression right)
    {
        if (left.Type == typeof(string) && right.Type == typeof(string))
        {
            var compareMethod = typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;
            Expression compareCall = Expression.Call(compareMethod, left, right);
            Expression zero = Expression.Constant(0);

            return op switch
            {
                ComparisonOperator.GreaterThan => Expression.GreaterThan(compareCall, zero),
                ComparisonOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(compareCall, zero),
                ComparisonOperator.LessThan => Expression.LessThan(compareCall, zero),
                ComparisonOperator.LessThanOrEqual => Expression.LessThanOrEqual(compareCall, zero),
                _ => throw new ArgumentException("Invalid comparison operator for string comparison.")
            };
        }

        return op switch
        {
            ComparisonOperator.GreaterThan => Expression.GreaterThan(left, right),
            ComparisonOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(left, right),
            ComparisonOperator.LessThan => Expression.LessThan(left, right),
            ComparisonOperator.LessThanOrEqual => Expression.LessThanOrEqual(left, right),
            _ => throw new ArgumentException("Invalid comparison operator for the given types.")
        };
    }

    private static MethodInfo GetMethodInfo(ComparisonOperator comparisonOperator)
    {
        if (comparisonOperator == ComparisonOperator.Like)
            throw new InvalidOperationException("LIKE is handled via Regex and should not call GetMethodInfo.");

        var method = comparisonOperator switch
        {
            ComparisonOperator.Contains =>
                typeof(string).GetMethod(nameof(string.Contains), [typeof(string), typeof(StringComparison)]),

            ComparisonOperator.StartsWith =>
                typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string), typeof(StringComparison)]),

            ComparisonOperator.EndsWith =>
                typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string), typeof(StringComparison)]),

            _ => throw new NotImplementedException($"Method for operator {comparisonOperator} is not implemented.")
        };

        if (method is null)
            throw new InvalidOperationException($"Method for {comparisonOperator} not found — check .NET version or string signature.");

        return method;
    }

    #endregion
}
