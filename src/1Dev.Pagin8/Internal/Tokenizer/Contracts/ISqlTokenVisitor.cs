using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;

namespace _1Dev.Pagin8.Internal.Tokenizer.Contracts;

public interface ISqlTokenVisitor 
{
    QueryBuilderResult Visit<T>(ComparisonToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(GroupToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(InToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(SortToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(LimitToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(SelectToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(PagingToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(ShowCountToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(DateRangeToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(IsToken token, QueryBuilderResult builder) where T : class;
    
    QueryBuilderResult Visit<T>(MetaIncludeToken token, QueryBuilderResult builder) where T : class;
    
    QueryBuilderResult Visit<T>(NestedFilterToken token, QueryBuilderResult builder) where T : class;

    QueryBuilderResult Visit<T>(ArrayOperationToken token, QueryBuilderResult builder) where T : class;
}
