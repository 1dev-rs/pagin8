using _1Dev.Pagin8.Internal.Tokenizer.Operators;

namespace _1Dev.Pagin8.Internal.DateProcessor;
public interface IDateProcessor
{
    public (DateTime startDate, DateTime endDate) GetStartAndEndOfRelativeDate(DateTime currentDate, int amount, DateRange unit, bool goBackwards, bool exact = false, bool strict = false);
}
