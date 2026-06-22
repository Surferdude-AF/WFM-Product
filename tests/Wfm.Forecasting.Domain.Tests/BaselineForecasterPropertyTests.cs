using CsCheck;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

// Property-based invariants (ADR-006 layer 1): truths that must hold for ANY
// history. CsCheck tries to break them.
public class BaselineForecasterPropertyTests
{
    private static readonly DateTimeOffset WeekStart = new(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);

    // Arbitrary sparse history on the 15-minute grid before the forecast week.
    private static readonly Gen<IReadOnlyList<HistoricalInterval>> GenHistory =
        Gen.Select(Gen.Int[0, 4000], Gen.Int[0, 1000], Gen.Int[0, 5000],
            (back, contacts, aht) => new HistoricalInterval(WeekStart.AddMinutes(-15 * back), contacts, aht))
        .List[0, 300]
        .Select(list => (IReadOnlyList<HistoricalInterval>)list);

    [Fact]
    public void A_full_week_of_intervals_is_always_produced_with_non_negative_volume_and_floored_aht()
    {
        GenHistory.Sample(history =>
        {
            var forecast = BaselineForecaster.Forecast(history, WeekStart);
            return forecast.Count == 7 * 96
                && forecast.All(p => p.Contacts >= 0 && p.AhtSeconds >= 60);
        });
    }

    [Fact]
    public void Same_input_yields_identical_output()
    {
        GenHistory.Sample(history =>
            BaselineForecaster.Forecast(history, WeekStart)
                .SequenceEqual(BaselineForecaster.Forecast(history, WeekStart)));
    }

    [Fact]
    public void Flat_history_forecasts_flat_and_scales_linearly()
    {
        Gen.Select(Gen.Int[0, 1000], Gen.Int[60, 3600], Gen.Int[1, 20], Gen.Int[1, 3])
            .Sample(t =>
            {
                var (contacts, aht, k, weeks) = t;

                var flat = BaselineForecaster.Forecast(Covering(weeks, contacts, aht), WeekStart);
                var flatHolds = flat.All(p => p.Contacts == contacts && p.AhtSeconds == aht);

                var scaled = BaselineForecaster.Forecast(Covering(weeks, contacts * k, aht), WeekStart);
                var scaleHolds = scaled.All(p => p.Contacts == contacts * k);

                return flatHolds && scaleHolds;
            });
    }

    [Fact]
    public void Empty_history_forecasts_the_neutral_defaults()
    {
        var forecast = BaselineForecaster.Forecast([], WeekStart);

        Assert.Equal(7 * 96, forecast.Count);
        Assert.All(forecast, p =>
        {
            Assert.Equal(0, p.Contacts);
            Assert.Equal(300, p.AhtSeconds);
        });
    }

    // History covering every weekday+interval for `weeks` prior weeks with constant
    // values, so every forecast key has samples and its weighted average is exact.
    private static List<HistoricalInterval> Covering(int weeks, int contacts, int aht)
    {
        var list = new List<HistoricalInterval>(weeks * 7 * 96);
        var start = WeekStart.AddDays(-7 * weeks);
        for (var day = 0; day < 7 * weeks; day++)
        {
            for (var i = 0; i < 96; i++)
            {
                list.Add(new HistoricalInterval(start.AddDays(day).AddMinutes(15 * i), contacts, aht));
            }
        }

        return list;
    }
}
