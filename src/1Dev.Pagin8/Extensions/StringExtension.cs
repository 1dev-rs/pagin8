namespace _1Dev.Pagin8.Extensions;

internal static class StringExtension
{
    public static string PascalToCamelCase(this string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase) || char.IsLower(pascalCase[0]))
            return pascalCase;

        return char.ToLowerInvariant(pascalCase[0]) + pascalCase[1..];
    }
}