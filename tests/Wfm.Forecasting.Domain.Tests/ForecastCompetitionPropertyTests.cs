using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

public class ForecastCompetitionPropertyTests
{
    private static readonly DateTimeOffset Start = new(2026, 4, 6, 0, 0, 0, TimeSpan.Zero); // a Monday

    // `weeks` whole weeks of 15-minute intervals; each week's per-interval volume
    // comes from contactsForWeek (so weekly totals follow whatever trend it encodes).
    private static List<HistoricalInterval> BuildWeeks(int weeks, Func<int, int> contactsForWeek)
    {
        var list = new List<HistoricalInterval>(weeks * 7 * 96);
        for (var week = 0; week < weeks; week++)
        {
            var contacts = contactsForWeek(week);
            for (var day = 0; day < 7; day++)
            {
                for (var i = 0; i < 96; i++)
                {
                    list.Add(new HistoricalInterval(Start.AddDays((week * 7) + day).AddMinutes(15 * i), contacts, 300));
                }
            }
        }

        return list;
    }

    [Fact]
    public void Too_little_data_defaults_to_the_simplest_method()
    {
        var result = ForecastCompetition.Run(BuildWeeks(5, _ => 10)); // need >= 6 weeks to compete

        Assert.False(result.Sufficient);
        Assert.Equal(ForecastCompetition.SeasonalNaive, result.Chosen);
        Assert.Empty(result.Methods);
    }

    [Fact]
    public void A_flat_series_keeps_the_simplest_method()
    {
        var result = ForecastCompetition.Run(BuildWeeks(8, _ => 10));

        Assert.True(result.Sufficient);
        Assert.Equal(ForecastCompetition.SeasonalNaive, result.Chosen); // trend earns nothing on flat data
    }

    [Fact]
    public void The_competition_is_deterministic()
    {
        var history = BuildWeeks(8, week => 10 + week);

        var first = ForecastCompetition.Run(history);
        var second = ForecastCompetition.Run(history);

        Assert.Equal(first.Chosen, second.Chosen);
        Assert.Equal(first.Scores, second.Scores);
        Assert.Equal(first.Mean, second.Mean);
    }

    [Fact]
    public void Weekly_slope_follows_the_trend_direction()
    {
        Assert.True(ForecastCompetition.WeeklySlope(BuildWeeks(6, week => 10 + week)) > 0);
        Assert.Equal(0, ForecastCompetition.WeeklySlope(BuildWeeks(6, _ => 10)));
        Assert.True(ForecastCompetition.WeeklySlope(BuildWeeks(6, week => 100 - week)) < 0);
    }
}
