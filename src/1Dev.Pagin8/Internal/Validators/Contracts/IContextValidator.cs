using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Validators.Contracts;
public interface IContextValidator 
{
    bool ValidateFilterableTokenFields<T>(List<Token> tokens) where T : class;
}