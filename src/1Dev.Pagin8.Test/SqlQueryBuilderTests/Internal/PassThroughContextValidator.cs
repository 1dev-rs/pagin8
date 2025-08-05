using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators.Contracts;

namespace _1Dev.Pagin8.Test.SqlQueryBuilderTests.Internal;

public class PassThroughContextValidator : IContextValidator
{
    public bool ValidateFilterableTokenFields<T>(List<Token> tokens) where T : class => true;
}