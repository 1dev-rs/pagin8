using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using _1Dev.Pagin8.Internal.Tokenizer.Operators;

namespace _1Dev.Pagin8.Internal.Validators;
public static class ComparisonValidator
{
    private static readonly HashSet<TypeCode> NumericTypes =
    [
        TypeCode.Byte, TypeCode.SByte, TypeCode.UInt16, TypeCode.UInt32, TypeCode.UInt64, TypeCode.Int16,
        TypeCode.Int32, TypeCode.Int64, TypeCode.Decimal, TypeCode.Double, TypeCode.Single
    ];

    private static readonly HashSet<TypeCode> NumericAndDateTimeTypes = [..NumericTypes, TypeCode.DateTime];

    private static readonly HashSet<TypeCode> TextTypes = [TypeCode.String, TypeCode.Char];

    private static readonly HashSet<TypeCode> BooleanTypes = [TypeCode.Boolean];

    private static readonly HashSet<TypeCode> AllTypes = NumericAndDateTimeTypes.Concat(BooleanTypes).Concat(TextTypes).ToHashSet();

    private static readonly HashSet<TypeCode> AllExceptBool = NumericAndDateTimeTypes.Concat(TextTypes).ToHashSet();

    private static readonly Dictionary<ComparisonOperator, HashSet<TypeCode>> SupportedTypesByOperator = new()
    {
        { ComparisonOperator.Equals, AllTypes },
        { ComparisonOperator.Like, TextTypes },
        { ComparisonOperator.LessThan, AllExceptBool },
        { ComparisonOperator.LessThanOrEqual, AllExceptBool },
        { ComparisonOperator.GreaterThan, AllExceptBool },
        { ComparisonOperator.GreaterThanOrEqual, AllExceptBool },
        { ComparisonOperator.Contains, TextTypes },
        { ComparisonOperator.StartsWith, TextTypes },
        { ComparisonOperator.EndsWith, TextTypes },
        { ComparisonOperator.In, AllTypes },
        { ComparisonOperator.Is, AllTypes }
    };

    public static void EnsureTypeCodeOperatorValid(TypeCode typeCode, ComparisonOperator op)
    {
        if (!SupportedTypesByOperator.TryGetValue(op, out var supportedTypes) || !supportedTypes.Contains(typeCode))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);
        }
    }
}
