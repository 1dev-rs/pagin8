namespace _1Dev.Pagin8.Extensions;

internal static class TypeExtensions
{
    public static bool IsNullable(this Type type)
    {
        return Nullable.GetUnderlyingType(type) != null;
    }

    public static bool IsDateOnly(this Type type)
    {
        return (Nullable.GetUnderlyingType(type) ?? type) == typeof(DateOnly);
    }

    // DateOnly has no TypeCode of its own (Type.GetTypeCode returns Object), so it is reported as DateTime
    // to reuse all date handling (validation, parsing, DATE() comparison, coalesce, meta type).
    public static TypeCode GetPagin8TypeCode(this Type type)
    {
        return type == typeof(DateOnly) ? TypeCode.DateTime : Type.GetTypeCode(type);
    }

    public static bool IsNumericType(this Type type)
    {
        if (type.IsEnum) return false;

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return true;

            default:
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    return IsNumericType(Nullable.GetUnderlyingType(type)!);
                }
                return false;
        }
    }

    public static bool IsNumericType(this TypeCode typeCode)
    {
        switch (typeCode)
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return true;
            default:
                return false;
        }
    }
}