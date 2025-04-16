using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;
public interface ITokenizationStrategy
{
    List<Token> Tokenize(string query, int nestingLevel = 1);

    List<Token> Tokenize(string query, string jsonPath, int nestingLevel = 1);
}
