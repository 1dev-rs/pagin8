using System.Globalization;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using AspNet.Transliterator;

namespace _1Dev.Pagin8.Internal.Helpers;

/// <summary>
/// Converts raw incl/excl values from the query string into typed values so they can be sent
/// as SQL parameters instead of being concatenated into the SQL text.
/// </summary>
internal static class ArrayValueConverter
{
    public static List<object> Convert(IEnumerable<object> values, Type elementType)
        => values.Select(v => Convert(v, elementType)).ToList();

    public static object Convert(object value, Type elementType)
    {
        var type = Nullable.GetUnderlyingType(elementType) ?? elementType;
        var raw = value.ToString()?.Trim() ?? string.Empty;

        object? result = type switch
        {
            _ when type == typeof(string) => Transliteration.CyrlToLatin(raw),
            _ when type == typeof(int) => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null,
            _ when type == typeof(long) => long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : null,
            _ when type == typeof(short) => short.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) ? s : null,
            _ when type == typeof(decimal) => decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var m) ? m : null,
            _ when type == typeof(double) => double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null,
            _ when type == typeof(float) => float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : null,
            _ when type == typeof(bool) => bool.TryParse(raw, out var b) ? b : null,
            _ when type == typeof(Guid) => Guid.TryParse(raw, out var g) ? g : null,
            _ when type == typeof(DateTime) => DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt : null,
            _ when type == typeof(DateOnly) => DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly) ? dateOnly.Date : null,
            _ => throw new Pagin8Exception(Pagin8StatusCode.Pagin8_PropertyTypeUnknown.Code, $"Array operations are not supported for type {type.Name}.")
        };

        return result ?? throw new Pagin8Exception(Pagin8StatusCode.Pagin8_InvalidArrayValue.Code, $"Value '{raw}' is not a valid {type.Name}.");
    }

    /// <summary>
    /// Joins values into a comma separated list where each value is a SQL parameter.
    /// </summary>
    public static FormattableString JoinAsParameters(IReadOnlyList<object> values, bool wrapInParentheses = false)
    {
        FormattableString Item(object v)
        {
            if (wrapInParentheses) return $"({v})";
            return $"{v}";
        }

        var joined = Item(values[0]);
        for (var i = 1; i < values.Count; i++)
        {
            joined = $"{joined}, {Item(values[i])}";
        }

        return joined;
    }
}
