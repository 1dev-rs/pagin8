namespace _1Dev.Pagin8;
public interface IQueryableTokenProcessor<T>
{
    /// <summary>
    /// Processes the given input query string to apply token-based transformations on a source sequence.
    /// </summary>
    /// <param name="input">The input query string containing tokens that dictate how the query should be modified.</param>
    /// <param name="source">The enumerable source of type <typeparamref name="T"/> over which the tokens will be applied.</param>
    /// <returns>An <see cref="IQueryable{T}"/> that represents the transformed source sequence based on the applied tokens from the input string.</returns>
    /// <remarks>
    /// This method parses the input string to extract tokens and applies these as queryable operations (like filters, sorts, selections, etc.) to the provided source sequence. The transformations are based on the semantics and the structure of the tokens in the input string.
    /// </remarks>
    IQueryable<T> Process(string input, IEnumerable<T> source);
}
