using System.Runtime.CompilerServices;

namespace _1Dev.Pagin8.Internal.Utils;
public class Guard
{
    public static void AgainstNull<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == null)
        {
            throw new ArgumentNullException(paramName);
        }
    }
}
