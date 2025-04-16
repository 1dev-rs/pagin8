using _1Dev.Pagin8.Internal.Tokenizer.Contracts;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;
public class NestedFilterTokenizationStrategyFactory : TokenizationStrategyFactory
{
    public override ITokenizationStrategy CreateTokenizationStrategy(ITokenizer tokenizer, string query) => new NestedFilterTokenizationStrategy(tokenizer);
}
