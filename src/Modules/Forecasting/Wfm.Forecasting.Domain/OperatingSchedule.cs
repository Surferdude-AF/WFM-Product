namespace Wfm.Forecasting.Domain;

// A Skill's full operating schedule: the weekly pattern plus date-specific special
// days. Applies to a forecast (in the Skill's local time, 9b): out-of-hours
// intervals are zeroed, and a special day's volume/AHT haircut is applied to the
// surviving in-hours intervals -- so it propagates to staffing downstream.
public sealed class OperatingSchedule
{
    public OperatingSchedule(OperatingHours hours, IReadOnlyDictionary<DateOnly, SpecialDay>? specialDays = null)
    {
        Hours = hours;
        SpecialDays = specialDays ?? new Dictionary<DateOnly, SpecialDay>();
    }

    public static OperatingSchedule AlwaysOpen { get; } = new(OperatingHours.AlwaysOpen);

    public OperatingHours Hours { get; }

    public IReadOnlyDictionary<DateOnly, SpecialDay> SpecialDays { get; }

    public IReadOnlyList<ForecastPoint> Apply(IReadOnlyList<ForecastPoint> forecast)
    {
        if (Hours.IsAlwaysOpen && SpecialDays.Count == 0)
        {
            return forecast;
        }

        var result = new List<ForecastPoint>(forecast.Count);
        var cache = new Dictionary<DateOnly, DayOperating>();
        foreach (var point in forecast)
        {
            var date = DateOnly.FromDateTime(point.Start.DateTime);
            if (!cache.TryGetValue(date, out var op))
            {
                cache[date] = op = ResolveDay(date);
            }

            var index = (point.Start.Hour * 4) + (point.Start.Minute / 15);
            if (op.Range is not var (start, end) || index < start || index >= end)
            {
                result.Add(point with { Contacts = 0 });
                continue;
            }

            var contacts = point.Contacts;
            var aht = point.AhtSeconds;
            if (op.Volume != 1.0)
            {
                contacts = Math.Max(0, (int)Math.Round(contacts * op.Volume, MidpointRounding.AwayFromZero));
            }

            if (op.Aht != 1.0)
            {
                aht = Math.Max(60, (int)Math.Round(aht * op.Aht, MidpointRounding.AwayFromZero));
            }

            result.Add(point with { Contacts = contacts, AhtSeconds = aht });
        }

        return result;
    }

    private DayOperating ResolveDay(DateOnly date)
    {
        if (!SpecialDays.TryGetValue(date, out var special))
        {
            return new DayOperating(Hours.RangeFor(date.DayOfWeek), 1.0, 1.0);
        }

        var range = special.Hours switch
        {
            SpecialDayHours.Closed => null,
            SpecialDayHours.Normal => Hours.RangeFor(date.DayOfWeek),
            SpecialDayHours.Custom => special.CustomHours?.ToIntervalRange(),
            _ => null,
        };

        return new DayOperating(range, special.VolumeMultiplier, special.AhtMultiplier);
    }

    private readonly record struct DayOperating((int Start, int End)? Range, double Volume, double Aht);
}
