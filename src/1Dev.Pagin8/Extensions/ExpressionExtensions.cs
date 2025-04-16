using System.Linq.Expressions;

namespace _1Dev.Pagin8.Extensions;
internal static class ExpressionExtensions
{
    public static Expression<Func<T, bool>> ExtractLambda<T>(this Expression expression)
    {
        if (expression is not MethodCallExpression { Method.Name: "Where" } methodCall) throw new InvalidOperationException("Unable to extract a lambda expression from the given expression.");

        if (methodCall.Arguments[1] is UnaryExpression { Operand: Expression<Func<T, bool>> lambda })
        {
            return lambda;
        }

        throw new InvalidOperationException("Unable to extract a lambda expression from the given expression.");
    }
}