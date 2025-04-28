using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Contracts;

namespace _1Dev.Pagin8.Internal.Tokenizer.Strategy;

public abstract class TokenizationStrategyFactory
{
    private static readonly Dictionary<Func<string, bool>, TokenizationStrategyFactory> FactoryRegistry
        = new()
        {
            { TokenHelper.IsSortOperation, new SortTokenizationStrategyFactory() },
            { TokenHelper.IsInOperation, new InTokenizationStrategyFactory() },
            { TokenHelper.IsGroupingOperation, new GroupTokenizationStrategyFactory() },
            { TokenHelper.IsLimitOperation, new LimitTokenizationStrategyFactory() },
            { TokenHelper.IsSelectOperation, new SelectTokenizationStrategyFactory() },
            { TokenHelper.IsPagingOperation, new PagingTokenizationStrategyFactory() },
            { TokenHelper.IsCountOperation, new ShowCountTokenizationStrategyFactory() },
            { TokenHelper.IsMetadataOperation, new MetaIncludeTokenizationStrategyFactory() },
            { TokenHelper.IsDateRangeOperation, new DateRangeTokenizationStrategyFactory() },
            { TokenHelper.IsComparisonOperation, new ComparisonTokenizationStrategyFactory() },
            { TokenHelper.IsIsOperation, new IsTokenizationStrategyFactory() },
            { TokenHelper.IsNestedFilterOperation, new NestedFilterTokenizationStrategyFactory() },
            { TokenHelper.IsArrayOperation, new ArrayTokenizationStrategyFactory() }
        };

    public abstract ITokenizationStrategy CreateTokenizationStrategy(ITokenizer tokenizer, string query);

    public static TokenizationStrategyFactory GetFactory(string query)
    {
        return FactoryRegistry.FirstOrDefault(kv => kv.Key(query)).Value ?? throw new NotSupportedException($"Provided format doesn't match with any tokenization factory: {query}");
    }
}