using System.Globalization;
using System.Text;

namespace _1Dev.Pagin8.Extensions;

internal static class StringExtension
{
    public static string RemoveDiacritics(this string input) =>
        string.Concat(input.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));

    public static string WithMaxLength(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Substring(0, Math.Min(value.Length, maxLength));
    }

    public static string PascalToCamelCase(this string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase) || char.IsLower(pascalCase[0]))
            return pascalCase;

        return char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
    }
}