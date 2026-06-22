namespace Wfm.Forecasting.Domain;

// Seasonal baseline forecast: each 15-minute interval of the forecast week is the
// recency-weighted average of the same weekday+interval across history (recent
// weeks count more). Pure and deterministic (ADR-005): the week start is injected,
// not read from a clock; timezone-agnostic (it keys off the wall-clock of the
// timestamps it is given, localised upstream in 9b). No outlier exclusion yet --
// that arrives in 9c.
public static class BaselineForecaster
{
    private const int IntervalsPerDay = 96;
    private const int DefaultAhtSeconds = 300;
    private const int MinAhtSeconds = 60;

    public static IReadOnlyList<ForecastPoint> Forecast(
        IReadOnlyList<HistoricalInterval> history,
        DateTimeOffset weekStart)
    {
        var groups = GroupByWeekdayInterval(history);

        var result = new List<ForecastPoint>(7 * IntervalsPerDay);
        for (var day = 0; day < 7; day++)
        {
            var date = weekStart.AddDays(day);
            for (var i = 0; i < IntervalsPerDay; i++)
            {
                var start = date.AddMinutes(15 * i);
                if (groups.TryGetValue((date.DayOfWeek, i), out var hist) && hist.Count > 0)
                {
                    var contacts = Math.Max(0, RoundHalfUp(WeightedAverage(hist, h => h.Contacts)));
                    var aht = Math.Max(MinAhtSeconds, RoundHalfUp(WeightedAverage(hist, h => h.AhtSeconds)));
                    result.Add(new ForecastPoint(start, contacts, aht));
                }
                else
                {
                    result.Add(new ForecastPoint(start, 0, DefaultAhtSeconds));
                }
            }
        }

        return result;
    }

    private static Dictionary<(DayOfWeek, int), List<HistoricalInterval>> GroupByWeekdayInterval(
        IReadOnlyList<HistoricalInterval> history)
    {
        var groups = new Dictionary<(DayOfWeek, int), List<HistoricalInterval>>();
        foreach (var interval in history)
        {
            var key = (interval.Start.DayOfWeek, interval.Start.Hour * 4 + interval.Start.Minute / 15);
            if (!groups.TryGetValue(key, out var bucket))
            {
                groups[key] = bucket = [];
            }

            bucket.Add(interval);
        }

        // Oldest first, so the linear-ramp weights give recent weeks the most pull.
        foreach (var bucket in groups.Values)
        {
            bucket.Sort((a, b) => a.Start.CompareTo(b.Start));
        }

        return groups;
    }

    // Linear ramp: the i-th oldest sample is weighted (i+1).
    private static double WeightedAverage(List<HistoricalInterval> items, Func<HistoricalInterval, int> select)
    {
        double sum = 0, weight = 0;
        for (var i = 0; i < items.Count; i++)
        {
            sum += select(items[i]) * (i + 1);
            weight += i + 1;
        }

        return sum / weight;
    }

    // Values are non-negative here, so away-from-zero matches the prototype's
    // JavaScript Math.round (round half up) -- not .NET's default banker's rounding.
    private static int RoundHalfUp(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
