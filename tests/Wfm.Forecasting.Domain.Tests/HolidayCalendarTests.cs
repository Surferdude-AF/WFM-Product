using System.Globalization;
using CsCheck;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

public class HolidayCalendarTests
{
    private static DateOnly Date(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public void Reproduces_the_prototype_us_holidays_for_2026()
    {
        var golden = Fixtures.LoadHolidays().Year2026;

        var holidays = HolidayCalendar.UnitedStates(2026);

        Assert.Equal(golden.Count, holidays.Count);
        for (var i = 0; i < holidays.Count; i++)
        {
            Assert.Equal(Date(golden[i].Date), holidays[i].Date);
            Assert.Equal(golden[i].Name, holidays[i].Name);
        }
    }

    [Fact]
    public void Reproduces_the_prototype_range_spanning_a_year_boundary()
    {
        var golden = Fixtures.LoadHolidays().Range;

        var holidays = HolidayCalendar.InRange("US", Date("2026-11-01"), Date("2027-01-15"));

        Assert.Equal(golden.Select(h => (Date(h.Date), h.Name)), holidays.Select(h => (h.Date, h.Name)));
    }

    [Fact]
    public void An_unknown_market_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => HolidayCalendar.InRange("XX", Date("2026-01-01"), Date("2026-12-31")));
    }

    [Fact]
    public void Every_holiday_of_a_year_falls_in_that_year_and_dates_are_distinct()
    {
        Gen.Int[1990, 2100].Sample(year =>
        {
            var holidays = HolidayCalendar.UnitedStates(year);
            return holidays.All(h => h.Date.Year == year)
                && holidays.Select(h => h.Date).Distinct().Count() == holidays.Count;
        });
    }

    [Fact]
    public void A_range_returns_holidays_in_order_and_within_bounds()
    {
        var start = Date("2026-03-01");
        var end = Date("2028-08-31");

        var holidays = HolidayCalendar.InRange("US", start, end);

        Assert.All(holidays, h => Assert.InRange(h.Date, start, end));
        Assert.Equal(holidays.OrderBy(h => h.Date), holidays);
    }
}
