namespace Wfm.Forecasting.Domain;

// A named calendar date (ST-002). Date-only by nature -- a holiday is a day, not a
// time. Reference data: it drives "needs review" flags that a human materialises
// into special days or events; it does not change the forecast on its own.
public sealed record Holiday(DateOnly Date, string Name);

public static class HolidayCalendar
{
    // US federal holidays for a year (observed-date shifting not modelled in v1).
    public static IReadOnlyList<Holiday> UnitedStates(int year) =>
    [
        new(new(year, 1, 1), "New Year's Day"),
        new(NthWeekday(year, 1, DayOfWeek.Monday, 3), "Martin Luther King Jr. Day"),
        new(NthWeekday(year, 2, DayOfWeek.Monday, 3), "Presidents' Day"),
        new(LastWeekday(year, 5, DayOfWeek.Monday), "Memorial Day"),
        new(new(year, 6, 19), "Juneteenth"),
        new(new(year, 7, 4), "Independence Day"),
        new(NthWeekday(year, 9, DayOfWeek.Monday, 1), "Labor Day"),
        new(NthWeekday(year, 10, DayOfWeek.Monday, 2), "Columbus Day"),
        new(new(year, 11, 11), "Veterans Day"),
        new(NthWeekday(year, 11, DayOfWeek.Thursday, 4), "Thanksgiving Day"),
        new(new(year, 12, 25), "Christmas Day"),
    ];

    // Holidays for a market between two dates (inclusive), spanning years, sorted.
    public static IReadOnlyList<Holiday> InRange(string market, DateOnly start, DateOnly end)
    {
        var calendar = Resolve(market);
        var result = new List<Holiday>();
        for (var year = start.Year; year <= end.Year; year++)
        {
            foreach (var holiday in calendar(year))
            {
                if (holiday.Date >= start && holiday.Date <= end)
                {
                    result.Add(holiday);
                }
            }
        }

        return result.OrderBy(h => h.Date).ToList();
    }

    private static Func<int, IReadOnlyList<Holiday>> Resolve(string market) => market switch
    {
        "US" => UnitedStates,
        _ => throw new ArgumentException($"Unknown market '{market}'.", nameof(market)),
    };

    private static DateOnly NthWeekday(int year, int month, DayOfWeek weekday, int n)
    {
        var first = new DateOnly(year, month, 1);
        var shift = ((int)weekday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(shift + ((n - 1) * 7));
    }

    private static DateOnly LastWeekday(int year, int month, DayOfWeek weekday)
    {
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var shift = ((int)last.DayOfWeek - (int)weekday + 7) % 7;
        return last.AddDays(-shift);
    }
}
