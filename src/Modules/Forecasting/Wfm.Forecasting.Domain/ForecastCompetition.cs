namespace Wfm.Forecasting.Domain;

// One method's measured performance across the walk-forward backtest folds.
public sealed record MethodScore(
    string Id,
    string Label,
    double MeanAccuracy,
    double Std,
    double Bias,
    IReadOnlyList<double> Scores);

// The outcome of the per-Skill method competition: which method was chosen and the
// accuracy thresholds (green/amber) for the chosen one. `Sufficient` is false when
// there is too little data to compete (defaults to the simplest method).
public sealed record CompetitionResult(
    bool Sufficient,
    string Chosen,
    IReadOnlyList<MethodScore> Methods,
    IReadOnlyList<double> Scores,
    double Mean,
    double Std,
    double? GreenThreshold,
    double? AmberThreshold);

// Multi-method forecast competition (ST-006): backtest each registered method on
// the Skill's own data (walk-forward), then pick the best by measured accuracy with
// a parsimony + margin rule -- the simpler seasonal-naive wins unless seasonal-trend
// beats it by a real margin. Makes adding methods safe: a method is only used where
// it provably wins. Pure and deterministic.
public static class ForecastCompetition
{
    public const string SeasonalNaive = "seasonal-naive";
    public const string SeasonalTrend = "seasonal-trend";

    private const int MinWeeksToCompete = 6;
    private const int WarmupWeeks = 4;       // first fold trains on weeks 0..3, tests week 4
    private const double FloorStd = 5.0;     // threshold spread floor
    private const double MarginFloor = 1.0;  // trend must beat naive by at least this

    // Linear-regression slope of weekly contact totals (0 when fewer than 2 weeks).
    public static double WeeklySlope(IReadOnlyList<HistoricalInterval> history)
    {
        var totals = WeeklyTotals(history);
        var n = totals.Count;
        if (n < 2)
        {
            return 0;
        }

        var xMean = (n - 1) / 2.0;
        var yMean = totals.Average();
        double numerator = 0, denominator = 0;
        for (var i = 0; i < n; i++)
        {
            numerator += (i - xMean) * (totals[i] - yMean);
            denominator += (i - xMean) * (i - xMean);
        }

        return numerator / denominator;
    }

    public static CompetitionResult Run(IReadOnlyList<HistoricalInterval> history)
    {
        var weeks = SplitIntoWeeks(history);
        if (weeks.Count < MinWeeksToCompete)
        {
            return new CompetitionResult(false, SeasonalNaive, [], [], 0, 0, null, null);
        }

        var naiveScores = new List<double>();
        var naiveBiases = new List<double>();
        var trendScores = new List<double>();
        var trendBiases = new List<double>();

        // Walk-forward: train on weeks 0..w-1, forecast week w.
        for (var w = WarmupWeeks; w < weeks.Count - 1; w++)
        {
            var train = weeks.Take(w).SelectMany(week => week).ToList();
            var clean = OutlierDetection.WithoutOutlierDays(train);
            var slope = WeeklySlope(clean);
            var testStart = MondayForward(weeks[w][0].Start);

            var actuals = new Dictionary<DateTimeOffset, int>();
            foreach (var record in weeks[w])
            {
                actuals[record.Start] = record.Contacts;
            }

            var baseForecast = BaselineForecaster.Forecast(clean, testStart);
            var n = baseForecast.Count;
            var actual = new double[n];
            var naive = new double[n];
            double baseSum = 0;
            for (var i = 0; i < n; i++)
            {
                actual[i] = actuals.GetValueOrDefault(baseForecast[i].Start, 0);
                naive[i] = baseForecast[i].Contacts;
                baseSum += baseForecast[i].Contacts;
            }

            var factor = TrendFactor(baseSum, slope, w);
            var trend = new double[n];
            for (var i = 0; i < n; i++)
            {
                trend[i] = factor == 1 ? naive[i] : Math.Max(0, naive[i] * factor);
            }

            naiveScores.Add(Clamp100(100 - Wmape(actual, naive)));
            naiveBiases.Add(BiasPercent(actual, naive));
            trendScores.Add(Clamp100(100 - Wmape(actual, trend)));
            trendBiases.Add(BiasPercent(actual, trend));
        }

        var folds = naiveScores.Count;

        // The selection uses 1-dp values (the prototype rounds before comparing).
        var naiveMethod = Summarise(SeasonalNaive, "Seasonal (no trend)", naiveScores, naiveBiases);
        var trendMethod = Summarise(SeasonalTrend, "Seasonal + trend", trendScores, trendBiases);

        var seDiff = Math.Sqrt(((naiveMethod.Std * naiveMethod.Std) + (trendMethod.Std * trendMethod.Std)) / folds);
        var margin = Math.Max(MarginFloor, seDiff);
        var chosen = trendMethod.MeanAccuracy - naiveMethod.MeanAccuracy >= margin ? SeasonalTrend : SeasonalNaive;

        var chosenScores = chosen == SeasonalTrend ? trendScores : naiveScores;
        var mean = chosenScores.Average();
        var rawStd = StandardDeviation(chosenScores, mean);
        var spread = Math.Max(rawStd, FloorStd);

        return new CompetitionResult(
            true,
            chosen,
            [naiveMethod, trendMethod],
            chosenScores,
            Round1(mean),
            Round1(rawStd),
            Round1(Clamp100(mean - spread)),
            Round1(Clamp100(mean - (2 * spread))));
    }

    private static MethodScore Summarise(string id, string label, List<double> scores, List<double> biases)
    {
        var mean = scores.Average();
        return new MethodScore(id, label, Round1(mean), Round1(StandardDeviation(scores, mean)), Round1(biases.Average()), scores);
    }

    private static double TrendFactor(double baseSum, double slope, int weekIndex)
        => baseSum > 0 ? (baseSum + (slope * weekIndex)) / baseSum : 1;

    private static double Wmape(double[] actual, double[] forecast)
    {
        double absoluteError = 0, totalActual = 0;
        for (var i = 0; i < actual.Length; i++)
        {
            absoluteError += Math.Abs(actual[i] - forecast[i]);
            totalActual += actual[i];
        }

        return totalActual == 0 ? 0 : absoluteError / totalActual * 100;
    }

    private static double BiasPercent(double[] actual, double[] forecast)
    {
        double error = 0, totalActual = 0;
        for (var i = 0; i < actual.Length; i++)
        {
            error += forecast[i] - actual[i];
            totalActual += actual[i];
        }

        return totalActual == 0 ? 0 : error / totalActual * 100;
    }

    private static double StandardDeviation(List<double> values, double mean)
    {
        double sum = 0;
        foreach (var v in values)
        {
            sum += (v - mean) * (v - mean);
        }

        return Math.Sqrt(sum / values.Count);
    }

    private static double Clamp100(double value) => Math.Min(100, Math.Max(0, value));

    private static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    private static List<double> WeeklyTotals(IReadOnlyList<HistoricalInterval> history)
    {
        var totals = new Dictionary<DateOnly, double>();
        var order = new List<DateOnly>();
        foreach (var record in history)
        {
            var monday = MondayOf(DateOnly.FromDateTime(record.Start.DateTime));
            if (!totals.ContainsKey(monday))
            {
                order.Add(monday);
            }

            totals[monday] = totals.GetValueOrDefault(monday) + record.Contacts;
        }

        return order.Select(k => totals[k]).ToList();
    }

    private static List<List<HistoricalInterval>> SplitIntoWeeks(IReadOnlyList<HistoricalInterval> history)
    {
        var byWeek = new Dictionary<DateOnly, List<HistoricalInterval>>();
        foreach (var record in history)
        {
            var monday = MondayOf(DateOnly.FromDateTime(record.Start.DateTime));
            if (!byWeek.TryGetValue(monday, out var bucket))
            {
                byWeek[monday] = bucket = [];
            }

            bucket.Add(record);
        }

        return byWeek.Keys.OrderBy(k => k).Select(k => byWeek[k]).ToList();
    }

    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static DateTimeOffset MondayForward(DateTimeOffset start)
    {
        var date = DateOnly.FromDateTime(start.DateTime);
        while (date.DayOfWeek != DayOfWeek.Monday)
        {
            date = date.AddDays(1);
        }

        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }
}
