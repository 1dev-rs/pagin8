namespace _1Dev.Pagin8.Internal.Utils;
public class Guard
{
    public static void AgainstNull<T>(T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException();
        }
    }
}
