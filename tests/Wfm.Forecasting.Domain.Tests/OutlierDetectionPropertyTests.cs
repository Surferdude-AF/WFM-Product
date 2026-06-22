using CsCheck;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

public class OutlierDetectionPropertyTests
{
    private static readonly DateTimeOffset WeekStartUtc = new(2026, 6, 8, 0, 0, 0, TimeSpan.Zero);

    private static readonly Gen<IReadOnlyList<HistoricalInterval>> GenHistory =
        Gen.Select(Gen.Int[0, 4000], Gen.Int[0, 1000],
            (back, contacts) => new HistoricalInterval(WeekStartUtc.AddMinutes(-15 * back), contacts, 200))
        .List[0, 300]
        .Select(list => (IReadOnlyList<HistoricalInterval>)list);

    [Fact]
    public void A_constant_series_flags_nothing()
    {
        Gen.Select(Gen.Int[1, 1000], Gen.Int[4, 8]).Sample(t =>
        {
            var (contacts, weeks) = t;
            var history = Covering(weeks, contacts);
            return OutlierDetection.DetectAnomalies(history).Count == 0
                && OutlierDetection.DetectOutlierDates(history).Count == 0;
        });
    }

    [Fact]
    public void Outlier_dates_are_a_subset_of_the_input_dates()
    {
        GenHistory.Sample(history =>
        {
            var dates = history.Select(h => DateOnly.FromDateTime(h.Start.DateTime)).ToHashSet();
            return OutlierDetection.DetectOutlierDates(history).All(dates.Contains);
        });
    }

    [Fact]
    public void An_extreme_spike_day_is_excluded_so_it_does_not_distort_the_forecast()
    {
        const int baseVolume = 20;
        var spikeDate = new DateOnly(2026, 6, 1); // the most recent Monday before the week start

        var flat = Covering(5, baseVolume);
        var spiked = flat
            .Select(h => DateOnly.FromDateTime(h.Start.DateTime) == spikeDate ? h with { Contacts = baseVolume * 10 } : h)
            .ToList();

        Assert.Contains(spikeDate, OutlierDetection.DetectOutlierDates(spiked));

        var clean = BaselineForecaster.Forecast(flat, WeekStartUtc);
        var robust = BaselineForecaster.Forecast(OutlierDetection.WithoutOutlierDays(spiked), WeekStartUtc);
        var unfiltered = BaselineForecaster.Forecast(spiked, WeekStartUtc);

        // Index 0 = Monday 00:00 (the week starts on a Monday).
        Assert.Equal(baseVolume, clean[0].Contacts);
        Assert.Equal(baseVolume, robust[0].Contacts);       // spike removed -> identical to the clean baseline
        Assert.True(unfiltered[0].Contacts > baseVolume);   // the spike distorts the unfiltered forecast
    }

    private static List<HistoricalInterval> Covering(int weeks, int contacts)
    {
        var list = new List<HistoricalInterval>(weeks * 7 * 96);
        var start = WeekStartUtc.AddDays(-7 * weeks);
        for (var day = 0; day < 7 * weeks; day++)
        {
            for (var i = 0; i < 96; i++)
            {
                list.Add(new HistoricalInterval(start.AddDays(day).AddMinutes(15 * i), contacts, 200));
            }
        }

        return list;
    }
}
