using _1Dev.Pagin8.Internal.Tokenizer.Tokens;

namespace _1Dev.Pagin8.Internal;

public record Meta
{
    public static Meta CreateDefault()
    {
        return new Meta
        {
            ShowCount = false,
            AdditionalInfoMeta = AdditionalInfoMeta.CreateDefault(),
            SanitizedQuery = string.Empty,
            SelectedFields = [],
            RequestedFields = []
        };
    }

    public static Meta CreateWithSanitizedQuery(string sanitizedDefaultFilter)
    {
        return new Meta
        {
            SanitizedQuery = sanitizedDefaultFilter
        };

    }

    public void SetAdditionalInfo(MetaIncludeToken token)
    {
        AdditionalInfoMeta = AdditionalInfoMeta.Create(token.Columns);
    }

    public bool ShowCount { get; set; }

    public AdditionalInfoMeta AdditionalInfoMeta { get; set; }

    public string SanitizedQuery { get; set; } = string.Empty;

    public List<string> SelectedFields { get; set; } = [];

    public List<string> RequestedFields { get; set; } = [];
}