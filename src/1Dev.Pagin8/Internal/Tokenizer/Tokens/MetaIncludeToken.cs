using _1Dev.Pagin8.Internal.Tokenizer.Contracts;

namespace _1Dev.Pagin8.Internal.Tokenizer.Tokens;

public class MetaIncludeToken(bool filters, bool subscriptions, bool columns) : Token
{
    public bool Filters { get; set; } = filters;
    public bool Subscriptions { get; set; } = subscriptions;
    public bool Columns { get; set; } = columns;


    public override QueryBuilderResult Accept<T>(ISqlTokenVisitor visitor, QueryBuilderResult result) => visitor.Visit<T>(this, result);

    public override IQueryable<T> Accept<T>(ILinqTokenVisitor<T> visitor, IQueryable<T> source) => visitor.Visit(this, source);

    public override string RevertToQueryString()
    {
        var properties = new List<string>();

        if (Filters) properties.Add(nameof(Filters).ToLowerInvariant());
        if (Subscriptions) properties.Add(nameof(Subscriptions).ToLowerInvariant());
        if (Columns) properties.Add(nameof(Columns).ToLowerInvariant());

        var joined = string.Join(",", properties);
        return $"metaInclude={joined}";
    }
}