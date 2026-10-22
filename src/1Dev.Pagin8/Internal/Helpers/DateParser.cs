using System.Globalization;

namespace _1Dev.Pagin8.Internal.Helpers;

/// <summary>
/// Single date format contract for filter values, shared by the SQL and LINQ visitors so both
/// modes accept exactly the same input.
/// </summary>
internal static class DateParser
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff"
    ];

    public static bool TryParseExactLocalDate(string value, out DateTime result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result = default;
            return false;
        }

        return DateTime.TryParseExact(value, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }
}
