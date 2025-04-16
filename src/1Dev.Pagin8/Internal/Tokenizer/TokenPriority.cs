namespace _1Dev.Pagin8.Internal.Tokenizer;

internal enum TokenPriority
{
    SelectToken = 0,
    ComparisonToken = 1,
    IsToken = 2,
    InToken = 3,
    ArrayOperationToken=4,
    NestedFilterToken = 5,
    GroupToken = 6,
    SortToken = 7,
    PagingToken = 8,
    MetaIncludeToken = 9,
}