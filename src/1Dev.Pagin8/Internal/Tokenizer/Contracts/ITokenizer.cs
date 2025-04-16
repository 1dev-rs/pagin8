using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Tokenizer.Contracts;
public interface ITokenizer
{
    List<Token> Tokenize(string query, int nestingLevel = 1);

    List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1);

    string RevertToQueryString(List<Token> tokens);
}
