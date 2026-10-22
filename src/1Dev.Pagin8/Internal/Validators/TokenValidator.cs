using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
using Internal.Configuration;
using Pagin8.Internal.Configuration;

namespace _1Dev.Pagin8.Internal.Validators;
public class TokenValidator
{
   public static void ValidateComparison(string comparison)
    {
        if (EngineDefaults.Config != null && !EngineDefaults.Config.ComparisonOperators.Contains(comparison))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);
        }
    }

    public static void ValidateNesting(int nestingLevel)
    {
        if (nestingLevel > Pagin8Runtime.Config.MaxNestingLevel)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_ExceededNesting.Code);
        }
    }

    // Groups and nested filters become a WHERE condition, so sort/limit inside them would produce invalid SQL
    public static void ValidateOnlyFilterTokens(IEnumerable<Tokenizer.Tokens.Token> tokens)
    {
        if (tokens.Any(t => t is not Tokenizer.Tokens.FilterToken))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_NestedLevelOperation.Code);
        }
    }

    public static void ValidateMaxItemsPerPage(int maxPerPage)
    {
        if (maxPerPage > Pagin8Runtime.Config.PagingSettings.MaxItemsPerPage)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_ExceededMaxItems.Code);
        }
    }
}