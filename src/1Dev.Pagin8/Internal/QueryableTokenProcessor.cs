using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;

namespace _1Dev.Pagin8.Internal;
public class QueryableTokenProcessor<T>(ITokenizationService tokenizationService, ILinqTokenVisitor<T> linqTokenVisitor)
    : IQueryableTokenProcessor<T>
    where T : class
{
    #region Public methods
   
    public IQueryable<T> Process(string input, IEnumerable<T> source)
    {
        var queryable = source.AsQueryable();
        var inputParams = QueryInputParameters.CreateWithQueryString(input);
        var tokenizationResponse = tokenizationService.Tokenize<T>(inputParams);

        return !tokenizationResponse.Tokens.Any() 
            ? queryable :
            tokenizationResponse.Tokens.Aggregate(queryable.Where(_ => true), (current, token) => token.Accept(linqTokenVisitor, current));
    }
    #endregion
}