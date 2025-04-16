using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens.Sort;

namespace _1Dev.Pagin8.Internal.Tokenizer.Contracts;

public interface ILinqTokenVisitor<T> where T : class
{
    IQueryable<T> Visit(ComparisonToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(GroupToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(InToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(SortToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(LimitToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(SelectToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(PagingToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(ShowCountToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(DateRangeToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(IsToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(MetaIncludeToken token, IQueryable<T> queryable);

    IQueryable<T> Visit(NestedFilterToken token, IQueryable<T> queryable);
    
    IQueryable<T> Visit(ArrayOperationToken token, IQueryable<T> queryable);
}