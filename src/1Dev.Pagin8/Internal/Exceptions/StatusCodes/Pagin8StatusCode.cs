// ReSharper disable InconsistentNaming
namespace _1Dev.Pagin8.Internal.Exceptions.StatusCodes;
public sealed class Pagin8StatusCode
{

    public static readonly Pagin8StatusCode Pagin8_UnsupportedComparison = new(nameof(Pagin8_UnsupportedComparison));
    public static readonly Pagin8StatusCode Pagin8_InvalidSortDirection = new(nameof(Pagin8_InvalidSortDirection));
    public static readonly Pagin8StatusCode Pagin8_UnsupportedDateRangeOperation = new(nameof(Pagin8_UnsupportedDateRangeOperation));
    public static readonly Pagin8StatusCode Pagin8_UnsupportedDateRange = new(nameof(Pagin8_UnsupportedDateRange));
    public static readonly Pagin8StatusCode Pagin8_TokenFieldInvalid = new(nameof(Pagin8_TokenFieldInvalid));
    public static readonly Pagin8StatusCode Pagin8_SortSectionMissing = new(nameof(Pagin8_SortSectionMissing));
    public static readonly Pagin8StatusCode Pagin8_InvalidSortExpressionFormat = new(nameof(Pagin8_InvalidSortExpressionFormat));
    public static readonly Pagin8StatusCode Pagin8_InvalidSortKeyPosition = new(nameof(Pagin8_InvalidSortKeyPosition));
    public static readonly Pagin8StatusCode Pagin8_SortKeyCursorMissing = new(nameof(Pagin8_SortKeyCursorMissing));
    public static readonly Pagin8StatusCode Pagin8_InvalidSortKayPlaceholderFormat = new(nameof(Pagin8_InvalidSortKayPlaceholderFormat));
    public static readonly Pagin8StatusCode Pagin8_RootLevelOperation = new(nameof(Pagin8_RootLevelOperation));
    public static readonly Pagin8StatusCode Pagin8_NestedLevelOperation = new(nameof(Pagin8_NestedLevelOperation));
    public static readonly Pagin8StatusCode Pagin8_InvalidMetaInclude = new(nameof(Pagin8_InvalidMetaInclude));
    public static readonly Pagin8StatusCode Pagin8_InvalidShowCount = new(nameof(Pagin8_InvalidShowCount));
    public static readonly Pagin8StatusCode Pagin8_InvalidSelect = new(nameof(Pagin8_InvalidSelect));
    public static readonly Pagin8StatusCode Pagin8_InvalidPaging = new(nameof(Pagin8_InvalidPaging));
    public static readonly Pagin8StatusCode Pagin8_InvalidLimit = new(nameof(Pagin8_InvalidLimit));
    public static readonly Pagin8StatusCode Pagin8_InvalidIn = new(nameof(Pagin8_InvalidIn));
    public static readonly Pagin8StatusCode Pagin8_InvalidGroup = new(nameof(Pagin8_InvalidGroup));
    public static readonly Pagin8StatusCode Pagin8_InvalidDateRange = new(nameof(Pagin8_InvalidDateRange));
    public static readonly Pagin8StatusCode Pagin8_InvalidComparison = new(nameof(Pagin8_InvalidComparison));
    public static readonly Pagin8StatusCode Pagin8_InvalidIsToken = new(nameof(Pagin8_InvalidIsToken));
    public static readonly Pagin8StatusCode Pagin8_NestingLevelMustBePositive = new(nameof(Pagin8_NestingLevelMustBePositive));
    public static readonly Pagin8StatusCode Pagin8_PropertyValueMustBePositive = new(nameof(Pagin8_PropertyValueMustBePositive));
    public static readonly Pagin8StatusCode Pagin8_ItemsPerPageMustBePositive = new(nameof(Pagin8_ItemsPerPageMustBePositive));
    public static readonly Pagin8StatusCode Pagin8_DefaultItemsPerPageMustBePositive = new(nameof(Pagin8_DefaultItemsPerPageMustBePositive));
    public static readonly Pagin8StatusCode Pagin8_MaxSafeItemCountMustBePositive = new(nameof(Pagin8_MaxSafeItemCountMustBePositive));
    public static readonly Pagin8StatusCode Pagin8_MaxTaggingCountMustBePositive = new(nameof(Pagin8_MaxTaggingCountMustBePositive));
    public static readonly Pagin8StatusCode Pagin8_ExceededNesting = new(nameof(Pagin8_ExceededNesting));
    public static readonly Pagin8StatusCode Pagin8_ExceededMaxItems = new(nameof(Pagin8_ExceededMaxItems));
    public static readonly Pagin8StatusCode Pagin8_GeneralError = new(nameof(Pagin8_GeneralError));
    public static readonly Pagin8StatusCode Pagin8_MissingQuery = new(nameof(Pagin8_MissingQuery));
    public static readonly Pagin8StatusCode Pagin8_MalformedQuery = new(nameof(Pagin8_MalformedQuery));
    public static readonly Pagin8StatusCode Pagin8_MissingSortExpressions = new(nameof(Pagin8_MissingSortExpressions));
    public static readonly Pagin8StatusCode Pagin8_ColumnNotExist = new(nameof(Pagin8_ColumnNotExist));
    public static readonly Pagin8StatusCode Pagin8_ColumnNotSortable = new(nameof(Pagin8_ColumnNotSortable));

    public static readonly Pagin8StatusCode Pagin8_DuplicateArrayType = new(nameof(Pagin8_DuplicateArrayType));
    public static readonly Pagin8StatusCode Pagin8_PropertyTypeUnknown = new(nameof(Pagin8_PropertyTypeUnknown));

    public string Code { get; }

    private Pagin8StatusCode(string code)
    {
        Code = code;
    }
}
