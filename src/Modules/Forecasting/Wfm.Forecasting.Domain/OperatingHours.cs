namespace Wfm.Forecasting.Domain;

// A Skill's weekly operating pattern (ST-002), in the Skill's local time. Unset =>
// always open (24/7, backward-compatible); otherwise a weekday absent from the map
// is closed.
public sealed class OperatingHours
{
    private readonly IReadOnlyDictionary<DayOfWeek, OpenRange>? _byWeekday;

    private OperatingHours(IReadOnlyDictionary<DayOfWeek, OpenRange>? byWeekday) => _byWeekday = byWeekday;

    public static OperatingHours AlwaysOpen { get; } = new(null);

    public static OperatingHours ForWeek(IReadOnlyDictionary<DayOfWeek, OpenRange> byWeekday) => new(byWeekday);

    public bool IsAlwaysOpen => _byWeekday is null;

    // Open interval span [Start, End) for a weekday, or null when closed.
    internal (int Start, int End)? RangeFor(DayOfWeek dayOfWeek)
    {
        if (_byWeekday is null)
        {
            return (0, 96);
        }

        return _byWeekday.TryGetValue(dayOfWeek, out var range) ? range.ToIntervalRange() : null;
    }
}
