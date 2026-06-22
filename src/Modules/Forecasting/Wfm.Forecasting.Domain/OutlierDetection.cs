namespace Wfm.Forecasting.Domain;

// Variance-aware outlier detection (9c): a day is an outlier if its total volume
// is statistically extreme FOR ITS WEEKDAY, by the robust modified z-score
// (median + MAD, so a few outliers don't move the goalposts) AND materially off
// the norm. Falls back to a fixed ratio band when robust stats can't be computed
// (thin data / zero spread). Used to exclude spike/dip days from the forecast's
// training set so they don't distort the weekday baseline.
public static class OutlierDetection
{
    private const double MzThreshold = 3.5;     // modified z-score cutoff (Iglewicz-Hoaglin)
    private const double MaterialDeviation = 0.10; // ignore statistically-odd but trivial days
    private const int MinSamples = 4;           // below this per weekday, robust stats are unreliable
    private const double OutlierHigh = 1.25;    // fallback band: > median x 1.25 -> spike
    private const double OutlierLow = 0.80;     //               < median x 0.80 -> dip

    public static IReadOnlySet<DateOnly> DetectOutlierDates(IReadOnlyList<HistoricalInterval> history)
    {
        var outliers = new HashSet<DateOnly>();
        foreach (var deviation in DayDeviations(history))
        {
            if (IsOutlier(deviation))
            {
                outliers.Add(deviation.Date);
            }
        }

        return outliers;
    }

    public static IReadOnlyList<Anomaly> DetectAnomalies(IReadOnlyList<HistoricalInterval> history)
        => DayDeviations(history)
            .Where(IsOutlier)
            .Select(d => new Anomaly(
                d.Date,
                d.Ratio >= 1 ? AnomalyDirection.High : AnomalyDirection.Low,
                Math.Round(d.Ratio, 2, MidpointRounding.AwayFromZero),
                d.Total,
                d.Median))
            .OrderByDescending(a => a.Date) // most recent first
            .ToList();

    public static IReadOnlyList<HistoricalInterval> WithoutOutlierDays(IReadOnlyList<HistoricalInterval> history)
    {
        var outliers = DetectOutlierDates(history);
        if (outliers.Count == 0)
        {
            return history;
        }

        return history.Where(h => !outliers.Contains(DateOnly.FromDateTime(h.Start.DateTime))).ToList();
    }

    private readonly record struct DayDeviation(DateOnly Date, int Total, double Median, double Ratio, double? Mz);

    // Per-day deviation vs the same weekday's distribution: `Ratio` is the magnitude
    // (for display), `Mz` the robust z-score (for the test, null when MAD is zero or
    // samples are too few so the caller uses the ratio-band fallback).
    private static List<DayDeviation> DayDeviations(IReadOnlyList<HistoricalInterval> history)
    {
        var dayTotals = new Dictionary<DateOnly, int>();
        foreach (var interval in history)
        {
            var date = DateOnly.FromDateTime(interval.Start.DateTime);
            dayTotals[date] = dayTotals.GetValueOrDefault(date) + interval.Contacts;
        }

        var byWeekday = new Dictionary<DayOfWeek, List<(DateOnly Date, int Total)>>();
        foreach (var (date, total) in dayTotals)
        {
            if (!byWeekday.TryGetValue(date.DayOfWeek, out var bucket))
            {
                byWeekday[date.DayOfWeek] = bucket = [];
            }

            bucket.Add((date, total));
        }

        var result = new List<DayDeviation>();
        foreach (var days in byWeekday.Values)
        {
            var totals = days.Select(d => (double)d.Total).ToList();
            var median = Median(totals);
            var mad = Median(totals.Select(t => Math.Abs(t - median)).ToList());
            foreach (var (date, total) in days)
            {
                var ratio = median > 0 ? total / median : 1.0;
                double? mz = mad > 0 && days.Count >= MinSamples ? 0.6745 * (total - median) / mad : null;
                result.Add(new DayDeviation(date, total, median, ratio, mz));
            }
        }

        return result;
    }

    private static bool IsOutlier(DayDeviation d)
        => d.Mz is double mz
            ? Math.Abs(mz) > MzThreshold && Math.Abs(d.Ratio - 1) >= MaterialDeviation
            : d.Ratio > OutlierHigh || d.Ratio < OutlierLow;

    private static double Median(IReadOnlyList<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
