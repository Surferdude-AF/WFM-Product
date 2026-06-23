namespace Wfm.Forecasting.Domain;

// Wraps the timezone-agnostic BaselineForecaster (9a) with per-Skill localisation
// (9b): converts UTC history into the Skill's local wall-clock before the seasonal
// grouping, forecasts a local week starting on the given local date, then converts
// the forecast back to UTC instants. With SkillTimeZone.Utc this is identical to
// calling BaselineForecaster directly.
public static class LocalizedForecaster
{
    public static IReadOnlyList<ForecastPoint> Forecast(
        IReadOnlyList<HistoricalInterval> history,
        SkillTimeZone zone,
        DateOnly weekStartLocal,
        OperatingSchedule? schedule = null)
    {
        var localHistory = new List<HistoricalInterval>(history.Count);
        foreach (var interval in history)
        {
            localHistory.Add(interval with { Start = zone.ToLocal(interval.Start) });
        }

        var weekStart = new DateTimeOffset(weekStartLocal.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var localForecast = BaselineForecaster.Forecast(localHistory, weekStart);

        // The operating mask runs in the Skill's local wall-clock, before instants
        // are converted back to UTC (ST-002 2a).
        localForecast = (schedule ?? OperatingSchedule.AlwaysOpen).Apply(localForecast);

        var result = new List<ForecastPoint>(localForecast.Count);
        foreach (var point in localForecast)
        {
            result.Add(point with { Start = zone.ToUtc(point.Start.DateTime) });
        }

        return result;
    }
}
