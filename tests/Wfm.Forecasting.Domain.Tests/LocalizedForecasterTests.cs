using System.Globalization;
using CsCheck;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

public class LocalizedForecasterTests
{
    private static readonly DateOnly WeekStart = new(2026, 6, 8);

    // Five prior Mondays at 09:00Z; in Berlin summer that is 11:00 local.
    private static readonly string[] PriorMondays =
        ["2026-05-04", "2026-05-11", "2026-05-18", "2026-05-25", "2026-06-01"];

    private static readonly Gen<IReadOnlyList<HistoricalInterval>> GenHistory =
        Gen.Select(Gen.Int[0, 4000], Gen.Int[0, 1000], Gen.Int[0, 5000],
            (back, contacts, aht) => new HistoricalInterval(
                new DateTimeOffset(2026, 6, 8, 0, 0, 0, TimeSpan.Zero).AddMinutes(-15 * back), contacts, aht))
        .List[0, 300]
        .Select(list => (IReadOnlyList<HistoricalInterval>)list);

    [Fact]
    public void With_utc_it_is_identical_to_the_baseline_forecaster()
    {
        var weekStartUtc = new DateTimeOffset(WeekStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        GenHistory.Sample(history =>
            LocalizedForecaster.Forecast(history, SkillTimeZone.Utc, WeekStart)
                .SequenceEqual(BaselineForecaster.Forecast(history, weekStartUtc)));
    }

    [Fact]
    public void A_local_week_maps_to_the_right_utc_instants()
    {
        var berlin = SkillTimeZone.Of("Europe/Berlin");

        var forecast = LocalizedForecaster.Forecast([], berlin, WeekStart);

        Assert.Equal(7 * 96, forecast.Count);
        // Local Monday 2026-06-08 00:00 CEST(+2) is 2026-06-07 22:00Z.
        Assert.Equal(new DateTimeOffset(2026, 6, 7, 22, 0, 0, TimeSpan.Zero), forecast[0].Start);
        // Local Tuesday 00:00 (interval 96) is 2026-06-08 22:00Z.
        Assert.Equal(new DateTimeOffset(2026, 6, 8, 22, 0, 0, TimeSpan.Zero), forecast[96].Start);
    }

    [Fact]
    public void The_local_zone_shifts_the_seasonal_profile()
    {
        var berlin = SkillTimeZone.Of("Europe/Berlin");
        // 09:00Z maps to 11:00 local in Berlin summer (interval 44, not 36).
        var history = PriorMondays
            .Select(d => new HistoricalInterval(
                new DateTimeOffset(
                    DateOnly.ParseExact(d, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(new TimeOnly(9, 0)),
                    TimeSpan.Zero),
                50, 200))
            .ToList();

        var forecast = LocalizedForecaster.Forecast(history, berlin, WeekStart);

        Assert.Equal(50, forecast[44].Contacts);                 // 11:00 local has the volume
        Assert.Equal(0, forecast[36].Contacts);                  // 09:00 local does not
        Assert.Equal(new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero), forecast[44].Start);
    }

    [Fact]
    public void A_week_spanning_a_dst_transition_still_produces_a_full_week()
    {
        var berlin = SkillTimeZone.Of("Europe/Berlin");
        // Week of 2026-10-19 contains the autumn fall-back (Sun 2026-10-25).
        var forecast = LocalizedForecaster.Forecast([], berlin, new DateOnly(2026, 10, 19));

        Assert.Equal(7 * 96, forecast.Count);
    }
}
