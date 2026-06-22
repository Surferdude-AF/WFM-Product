using CsCheck;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

public class SkillTimeZoneTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_unset_zone_is_utc(string? id)
    {
        Assert.Equal("UTC", SkillTimeZone.Of(id).Id);
    }

    [Fact]
    public void A_known_iana_id_is_accepted()
    {
        Assert.Equal("Europe/Berlin", SkillTimeZone.Of("Europe/Berlin").Id);
    }

    [Fact]
    public void An_unknown_iana_id_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => SkillTimeZone.Of("Mars/Olympus_Mons"));
    }

    [Fact]
    public void Converts_utc_to_local_in_summer_dst()
    {
        // Berlin is CEST (+02:00) in June.
        var local = SkillTimeZone.Of("Europe/Berlin").ToLocal(new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2026, 6, 8, 11, 0, 0), local.DateTime);
        Assert.Equal(TimeSpan.FromHours(2), local.Offset);
    }

    [Fact]
    public void Converts_utc_to_local_in_winter_standard_time()
    {
        // Berlin is CET (+01:00) in January.
        var local = SkillTimeZone.Of("Europe/Berlin").ToLocal(new DateTimeOffset(2026, 1, 12, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateTime(2026, 1, 12, 10, 0, 0), local.DateTime);
        Assert.Equal(TimeSpan.FromHours(1), local.Offset);
    }

    [Fact]
    public void Round_trips_local_back_to_utc_away_from_transitions()
    {
        var berlin = SkillTimeZone.Of("Europe/Berlin");
        var june = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        // Every 15-minute instant across June (no DST transition) round-trips exactly.
        Gen.Int[0, 30 * 96].Sample(n =>
        {
            var utc = june.AddMinutes(15 * n);
            return berlin.ToUtc(berlin.ToLocal(utc).DateTime) == utc;
        });
    }
}
