using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.Helpers;
using _1Dev.Pagin8.Internal.Tokenizer.Tokens;
using _1Dev.Pagin8.Internal.Validators.Contracts;
using Microsoft.Extensions.Logging;

namespace _1Dev.Pagin8.Internal.Validators;

public class TokenContextValidator(IPagin8MetadataProvider metadataProvider, ServiceConfiguration config, ILogger<TokenContextValidator> logger) : IContextValidator
{
    public bool ValidateFilterableTokenFields<T>(List<Token> tokens) where T : class
    {
        return tokens.All(IsValidToken<T>);
    }

    private bool IsValidToken<T>(Token token) where T : class
    {
        return token switch
        {
            ComparisonToken comparisonToken => ValidateAndLog<T>(CanBeFiltered<T>(comparisonToken.Field), comparisonToken.Field),
            GroupToken groupToken => ValidateAndLog<T>(ValidateFilterableTokenFields<T>(groupToken.Tokens), null),
            DateRangeToken dateRangeToken => ValidateAndLog<T>(CanBeFiltered<T>(dateRangeToken.Field), dateRangeToken.Field),
            InToken inToken => ValidateAndLog<T>(CanBeFiltered<T>(inToken.Field), inToken.Field),
            IsToken isToken => ValidateAndLog<T>(CanBeFiltered<T>(isToken.Field), isToken.Field),
            ArrayOperationToken arrayOperationToken => ValidateAndLog<T>(CanBeFiltered<T>(arrayOperationToken.Field), arrayOperationToken.Field),
            NestedFilterToken nestedFilterToken => ValidateAndLog<T>(CanBeFiltered<T>(nestedFilterToken.Field), nestedFilterToken.Field),
            PagingToken { Sort: { } sort } => sort.SortExpressions.All(x => ValidateAndLog<T>(CanBeSorted<T>(x.Field), x.Field)),
            SelectToken selectToken => ValidateOrSanitizeSelectFields<T>(selectToken),
            _ => true
        };
    }

    private bool ValidateOrSanitizeSelectFields<T>(SelectToken selectToken) where T : class
    {
        if (!config.IgnoreUnknownSelectFields)
        {
            return selectToken.Fields.All(field => ValidateAndLog<T>(CanBeSelected<T>(field), field));
        }

        var invalidFields = selectToken.Fields.Where(field => !CanBeSelected<T>(field)).ToList();
        foreach (var field in invalidFields)
        {
            logger.LogWarning("Ignoring unknown select field '{Field}' for type '{Type}'", field, typeof(T).Name);
            selectToken.Fields.Remove(field);
        }

        return true;
    }

    private bool ValidateAndLog<T>(bool isValid, string? field)
    {
        if (!isValid && field != null)
        {
            logger.LogError($"Cannot perform operation on field '{field}' for type '{typeof(T).Name}'");
        }
        return isValid;
    }

    private bool CanBeFiltered<T>(string field) where T : class => metadataProvider.IsFieldFilterable<T>(field);

    private bool CanBeSorted<T>(string field) where T : class => metadataProvider.IsFieldSortable<T>(field) || field.Equals(QueryConstants.KeyPlaceholder, StringComparison.OrdinalIgnoreCase);

    private bool CanBeSelected<T>(string field) where T : class => metadataProvider.IsFieldInMeta<T>(field) || field.Equals(QueryConstants.SelectAsterisk);
}