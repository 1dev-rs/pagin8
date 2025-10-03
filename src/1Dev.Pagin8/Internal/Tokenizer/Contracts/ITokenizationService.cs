using _1Dev.Pagin8.Input;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Tokenizer.Contracts;

public interface ITokenizationService 
{
    TokenizationResponse Tokenize<T>(QueryInputParameters input, bool validateContext = true) where T : class;

    string Standardize<T>(string input, bool isDefault = false) where T : class;

    string MergeAndStandardize<T>(string schema1, string schema2, bool isDefault = false) where T : class;

    string Standardize(IEnumerable<Token> tokens);
}