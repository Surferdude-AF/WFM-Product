using System.Globalization;
using CsCheck;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

public class OperatingScheduleTests
{
    private static readonly DateOnly WeekStart = new(2026, 6, 8);

    // Mon-Fri 08:00-20:00, weekends closed; with three special days. Mirrors the
    // skill the golden was captured from.
    private static OperatingSchedule SampleSchedule()
    {
        var open = new OpenRange(new TimeOnly(8, 0), new TimeOnly(20, 0));
        var weekly = OperatingHours.ForWeek(new Dictionary<DayOfWeek, OpenRange>
        {
            [DayOfWeek.Monday] = open,
            [DayOfWeek.Tuesday] = open,
            [DayOfWeek.Wednesday] = open,
            [DayOfWeek.Thursday] = open,
            [DayOfWeek.Friday] = open,
        });
        var specials = new Dictionary<DateOnly, SpecialDay>
        {
            [new(2026, 6, 10)] = new(SpecialDayHours.Closed),
            [new(2026, 6, 11)] = new(SpecialDayHours.Custom, new OpenRange(new TimeOnly(10, 0), new TimeOnly(16, 0))),
            [new(2026, 6, 12)] = new(SpecialDayHours.Normal, VolumeMultiplier: 0.5, AhtMultiplier: 1.5),
        };
        return new OperatingSchedule(weekly, specials);
    }

    private static List<ForecastPoint> FlatWeek(DateOnly start, int contacts, int aht)
    {
        var list = new List<ForecastPoint>(7 * 96);
        var s = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        for (var day = 0; day < 7; day++)
        {
            for (var i = 0; i < 96; i++)
            {
                list.Add(new ForecastPoint(s.AddDays(day).AddMinutes(15 * i), contacts, aht));
            }
        }

        return list;
    }

    [Fact]
    public void Reproduces_the_prototype_operating_mask()
    {
        var golden = Fixtures.LoadMask();
        var masked = SampleSchedule().Apply(FlatWeek(WeekStart, 100, 300));

        Assert.Equal(golden.Masked.Count, masked.Count);
        for (var i = 0; i < masked.Count; i++)
        {
            var expected = golden.Masked[i];
            var actual = masked[i];
            Assert.Equal(expected.Timestamp, actual.Start.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Assert.Equal(expected.Contacts, actual.Contacts);
            Assert.Equal(expected.AhtSeconds, actual.AhtSeconds);
        }
    }

    [Fact]
    public void Always_open_with_no_special_days_leaves_the_forecast_unchanged()
    {
        var forecast = FlatWeek(WeekStart, 100, 300);

        Assert.Same(forecast, OperatingSchedule.AlwaysOpen.Apply(forecast));
    }

    [Fact]
    public void A_closed_interval_always_forecasts_zero()
    {
        var masked = SampleSchedule().Apply(FlatWeek(WeekStart, 100, 300));

        // Weekend (Sat 2026-06-13) is closed; Monday before 08:00 is closed.
        Assert.All(masked.Where(p => p.Start.DayOfWeek == DayOfWeek.Saturday), p => Assert.Equal(0, p.Contacts));
        Assert.All(
            masked.Where(p => p.Start.DayOfWeek == DayOfWeek.Monday && p.Start.Hour < 8),
            p => Assert.Equal(0, p.Contacts));
    }

    [Fact]
    public void A_unit_haircut_within_open_hours_is_identity()
    {
        Gen.Select(Gen.Int[0, 1000], Gen.Int[60, 3600]).Sample(t =>
        {
            var (contacts, aht) = t;
            var schedule = new OperatingSchedule(
                OperatingHours.AlwaysOpen,
                new Dictionary<DateOnly, SpecialDay>
                {
                    [WeekStart] = new(SpecialDayHours.Normal, VolumeMultiplier: 1.0, AhtMultiplier: 1.0),
                });

            var input = FlatWeek(WeekStart, contacts, aht);
            var output = schedule.Apply(input);

            return output.Zip(input).All(z => z.First.Contacts == z.Second.Contacts && z.First.AhtSeconds == z.Second.AhtSeconds);
        });
    }
}
