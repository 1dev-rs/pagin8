namespace _1Dev.Pagin8.Extensions;

internal static class CollectionExtension
{
    public static void Update<T>(this IEnumerable<T> source, Action<T> update) where T : class
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (update == null) throw new ArgumentNullException(nameof(update));

        foreach (var element in source)
        {
            update(element);
        }
    }
}