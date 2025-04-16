using _1Dev.Pagin8.Internal.Tokenizer.Operators;

namespace _1Dev.Pagin8.Internal.DateProcessor;
public class DateProcessor : IDateProcessor
{
    public (DateTime startDate, DateTime endDate) GetStartAndEndOfRelativeDate(DateTime currentDate, int amount, DateRange unit, bool goBackwards, bool exact = false, bool strict = false) =>
        unit switch
        {
            DateRange.Day => CalculateDayRange(currentDate, amount, goBackwards, exact, strict),
            DateRange.Week => CalculateWeekRange(currentDate, amount, goBackwards, exact, strict),
            DateRange.Month => CalculateMonthRange(currentDate, amount, goBackwards, exact, strict),
            DateRange.Year => CalculateYearRange(currentDate, amount, goBackwards, exact, strict),
            _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Invalid DateRange unit.")
        };

    private static (DateTime startDate, DateTime endDate) CalculateDayRange(DateTime currentDate, int amount, bool goBackwards, bool exact, bool strict)
    {
        if (exact)
        {
            var targetDate = goBackwards ? currentDate.AddDays(-amount) : currentDate.AddDays(amount);
            return (targetDate.Date, targetDate.Date.AddDays(1).AddTicks(-1));
        }

        var offsetDate = goBackwards ? currentDate.AddDays(-amount) : currentDate.AddDays(amount);

        if (strict) return goBackwards
                            ? (offsetDate, currentDate)
                            : (currentDate, offsetDate);

        return goBackwards
            ? (offsetDate.Date, currentDate)
            : (currentDate, offsetDate.Date.AddDays(1).AddTicks(-1));
    }


    private static (DateTime startDate, DateTime endDate) CalculateWeekRange(DateTime currentDate, int amount, bool goBackwards, bool exact, bool strict)
    {
        var targetDate = goBackwards
            ? currentDate.AddDays(-amount * 7)
            : currentDate.AddDays(amount * 7);

        if (exact)
        {
            return (targetDate.Date, targetDate.Date.AddDays(1).AddTicks(-1)); // Full day range
        }

        if (strict)
        {
            return goBackwards
                ? (targetDate, currentDate)  
                : (currentDate, targetDate);
        }

        var startOfTargetWeek = targetDate.AddDays(-WeekDaysToSubtract(targetDate)).Date;
        var endOfTargetWeek = targetDate.AddDays(WeekDaysToAdd(targetDate)).Date.AddTicks(-1);

        return goBackwards
            ? (startOfTargetWeek, currentDate)
            : (currentDate, endOfTargetWeek);
    }

    private static (DateTime startDate, DateTime endDate) CalculateMonthRange(DateTime currentDate, int amount, bool goBackwards, bool exact, bool strict)
    {
        var targetDate = goBackwards
            ? currentDate.AddMonths(-amount)
            : currentDate.AddMonths(amount);

        if (exact)
        {
            return (targetDate.Date, targetDate.Date.AddDays(1).AddTicks(-1));
        }

        if (strict)
        {
            return goBackwards
                ? (targetDate, currentDate)
                : (currentDate, targetDate);
        }

        var startOfTargetMonth = new DateTime(targetDate.Year, targetDate.Month, 1);
        var endOfTargetMonth = startOfTargetMonth.AddMonths(1).AddTicks(-1);

        return goBackwards
            ? (startOfTargetMonth, currentDate)
            : (currentDate, endOfTargetMonth);
    }

    private static (DateTime startDate, DateTime endDate) CalculateYearRange(DateTime currentDate, int amount, bool goBackwards, bool exact, bool strict)
    {
        var targetDate = goBackwards
            ? currentDate.AddYears(-amount)
            : currentDate.AddYears(amount);

        if (exact)
        {
            return (targetDate.Date, targetDate.Date.AddDays(1).AddTicks(-1));
        }

        if (strict)
        {
            return goBackwards
                ? (targetDate, currentDate)   
                : (currentDate, targetDate);
        }

        var startOfTargetYear = new DateTime(targetDate.Year, 1, 1);
        var endOfTargetYear = startOfTargetYear.AddYears(1).AddTicks(-1);

        return goBackwards
            ? (startOfTargetYear, currentDate)
            : (currentDate, endOfTargetYear);
    }

    private static int WeekDaysToSubtract(DateTime currentDate)
    {
        return ((int)currentDate.DayOfWeek - (int)GetWeekStartDay() + 7) % 7;
    }

    private static int WeekDaysToAdd(DateTime currentDate)
    {
        return ((int)GetWeekStartDay() - (int)currentDate.DayOfWeek + 7) % 7;
    }

    private static DayOfWeek GetWeekStartDay()
    {
        return System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
    }
}
