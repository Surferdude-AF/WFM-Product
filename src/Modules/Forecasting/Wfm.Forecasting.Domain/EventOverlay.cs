namespace Wfm.Forecasting.Domain;

// A demand overlay (the adopted AI feature's deterministic engine, 9e): over a
// local date range it multiplies volume and AHT by fixed factors, scoped to named
// Skills (empty scope = all). The base forecast stays a pure, regenerable layer
// underneath; an LLM may only PROPOSE one of these (a later phase), never write a
// forecast number.
public sealed record ForecastEvent(
    string Name,
    DateOnly Start,
    DateOnly End,
    double VolumeMultiplier,
    double AhtMultiplier,
    IReadOnlyCollection<string> Skills);

public static class EventOverlay
{
    // Applies every in-range, in-scope event to the forecast, stacking overlaps
    // multiplicatively. Composed inside operating hours -- ApplyOperatingDay(ApplyEvents(..)) --
    // so a closed interval stays 0 regardless of an event multiplier.
    public static IReadOnlyList<ForecastPoint> Apply(
        IReadOnlyList<ForecastPoint> forecast,
        IReadOnlyList<ForecastEvent> events,
        string skillName)
    {
        if (events.Count == 0)
        {
            return forecast;
        }

        var result = new List<ForecastPoint>(forecast.Count);
        foreach (var point in forecast)
        {
            var date = DateOnly.FromDateTime(point.Start.DateTime);
            double volume = 1, aht = 1;
            foreach (var e in events)
            {
                var inRange = date >= e.Start && date <= e.End;
                var inScope = e.Skills.Count == 0 || e.Skills.Contains(skillName);
                if (inRange && inScope)
                {
                    volume *= e.VolumeMultiplier;
                    aht *= e.AhtMultiplier;
                }
            }

            if (volume == 1 && aht == 1)
            {
                result.Add(point);
                continue;
            }

            result.Add(point with
            {
                Contacts = Math.Max(0, (int)Math.Round(point.Contacts * volume, MidpointRounding.AwayFromZero)),
                AhtSeconds = (int)Math.Round(point.AhtSeconds * aht, MidpointRounding.AwayFromZero),
            });
        }

        return result;
    }
}
