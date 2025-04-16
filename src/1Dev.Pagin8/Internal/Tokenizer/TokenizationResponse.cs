using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal.Tokenizer;

public record TokenizationResponse(List<Token> Tokens, string SanitizedQuery, bool IsMetaOnly = false, bool IsCountOnly = false)
{
    public static TokenizationResponse CreateEmpty()
    {
        return new TokenizationResponse([], string.Empty);
    }
    public List<Token> Tokens { get; set; } = Tokens;

    public string SanitizedQuery { get; set; } = SanitizedQuery;

    public bool IsMetaOnly { get; set; } = IsMetaOnly;

    public bool IsCountOnly { get; set; } = IsCountOnly;
}