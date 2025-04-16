using _1Dev.Pagin8.Internal.Configuration;
using _1Dev.Pagin8.Internal.Exceptions.Base;
using _1Dev.Pagin8.Internal.Exceptions.StatusCodes;

namespace _1Dev.Pagin8.Internal.Validators;
public class TokenValidator
{
   public static void ValidateComparison(string comparison)
    {
        if (ConfigurationProvider.Config != null && !ConfigurationProvider.Config.ComparisonOperators.Contains(comparison))
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_UnsupportedComparison.Code);
        }
    }

    public static void ValidateNesting(int nestingLevel)
    {
        if (nestingLevel > ConfigurationProvider.Config?.MaxNestingLevel)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_ExceededNesting.Code);
        }
    }

    public static void ValidateMaxItemsPerPage(int maxPerPage)
    {
        if (maxPerPage > ConfigurationProvider.Config?.PagingSettings.MaxItemsPerPage)
        {
            throw new Pagin8Exception(Pagin8StatusCode.Pagin8_ExceededMaxItems.Code);
        }
    }
}