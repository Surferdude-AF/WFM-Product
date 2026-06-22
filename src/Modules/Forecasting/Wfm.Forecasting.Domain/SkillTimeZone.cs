using NodaTime;

namespace Wfm.Forecasting.Domain;

// A Skill's IANA time zone (e.g. "Europe/Berlin"), and the UTC<->local conversion
// the forecast pipeline needs: a Skill forecasts in its own wall-clock, but stats
// are stored UTC (9b). Unset => UTC. DST policy (v1): local wall-clock is mapped
// leniently across transitions (ambiguous/gap times resolved, not rejected).
public sealed class SkillTimeZone
{
    private readonly DateTimeZone _zone;

    private SkillTimeZone(string id, DateTimeZone zone)
    {
        Id = id;
        _zone = zone;
    }

    public string Id { get; }

    public static SkillTimeZone Utc { get; } = new("UTC", DateTimeZone.Utc);

    public static SkillTimeZone Of(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
        {
            return Utc;
        }

        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(ianaId)
            ?? throw new ArgumentException($"Unknown IANA time zone '{ianaId}'.", nameof(ianaId));

        return new SkillTimeZone(ianaId, zone);
    }

    // UTC instant -> local wall-clock carrying the zone's offset at that instant.
    public DateTimeOffset ToLocal(DateTimeOffset utcInstant)
        => Instant.FromDateTimeOffset(utcInstant).InZone(_zone).ToDateTimeOffset();

    // Local wall-clock -> UTC instant. Lenient across DST gaps/overlaps (v1 policy).
    public DateTimeOffset ToUtc(DateTime localWallClock)
        => _zone.AtLeniently(LocalDateTime.FromDateTime(DateTime.SpecifyKind(localWallClock, DateTimeKind.Unspecified)))
            .ToInstant()
            .ToDateTimeOffset();
}
