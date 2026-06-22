using System.Globalization;
using CsCheck;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

public class EventOverlayTests
{
    private static List<ForecastPoint> FlatSpan(DateOnly start, int days, int contacts, int aht)
    {
        var list = new List<ForecastPoint>(days * 96);
        var s = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        for (var day = 0; day < days; day++)
        {
            for (var i = 0; i < 96; i++)
            {
                list.Add(new ForecastPoint(s.AddDays(day).AddMinutes(15 * i), contacts, aht));
            }
        }

        return list;
    }

    [Fact]
    public void Reproduces_the_prototype_event_overlay()
    {
        var golden = Fixtures.LoadEventsOverlay();
        var events = Fixtures.LoadEvents();
        var start = DateOnly.ParseExact(golden.SpanStart, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        var applied = EventOverlay.Apply(FlatSpan(start, golden.Days, 100, 300), events, golden.SkillName);

        Assert.Equal(golden.Applied.Count, applied.Count);
        for (var i = 0; i < applied.Count; i++)
        {
            Assert.Equal(golden.Applied[i].Timestamp, applied[i].Start.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Assert.Equal(golden.Applied[i].Contacts, applied[i].Contacts);
            Assert.Equal(golden.Applied[i].AhtSeconds, applied[i].AhtSeconds);
        }
    }

    [Fact]
    public void No_events_is_identity()
    {
        var forecast = FlatSpan(new DateOnly(2026, 6, 8), 7, 100, 300);

        Assert.Same(forecast, EventOverlay.Apply(forecast, [], "TS"));
    }

    [Fact]
    public void An_out_of_scope_event_leaves_the_forecast_unchanged()
    {
        var forecast = FlatSpan(new DateOnly(2026, 6, 8), 7, 100, 300);
        var csOnly = new ForecastEvent("CS promo", new(2026, 6, 9), new(2026, 6, 9), 2.0, 1.0, ["CS"]);

        var applied = EventOverlay.Apply(forecast, [csOnly], "TS");

        Assert.True(applied.Zip(forecast).All(z => z.First.Contacts == z.Second.Contacts));
    }

    [Fact]
    public void Overlapping_events_stack_multiplicatively_and_order_independently()
    {
        Gen.Select(Gen.Int[0, 1000], Gen.Double[0.1, 5.0], Gen.Double[0.1, 5.0]).Sample(t =>
        {
            var (contacts, m1, m2) = t;
            var forecast = FlatSpan(new DateOnly(2026, 6, 8), 1, contacts, 300);
            var e1 = new ForecastEvent("A", new(2026, 6, 8), new(2026, 6, 8), m1, 1.0, []);
            var e2 = new ForecastEvent("B", new(2026, 6, 8), new(2026, 6, 8), m2, 1.0, []);

            var forward = EventOverlay.Apply(forecast, [e1, e2], "TS");
            var reverse = EventOverlay.Apply(forecast, [e2, e1], "TS");

            var expected = Math.Max(0, (int)Math.Round(contacts * (m1 * m2), MidpointRounding.AwayFromZero));
            return forward.SequenceEqual(reverse) && forward.All(p => p.Contacts == expected && p.Contacts >= 0);
        });
    }

    [Fact]
    public void An_event_only_changes_dates_in_its_range()
    {
        var forecast = FlatSpan(new DateOnly(2026, 6, 8), 7, 100, 300);
        var midWeek = new ForecastEvent("Spike", new(2026, 6, 10), new(2026, 6, 11), 1.5, 1.0, []);

        var applied = EventOverlay.Apply(forecast, [midWeek], "TS");

        Assert.All(applied, p =>
        {
            var date = DateOnly.FromDateTime(p.Start.DateTime);
            var inRange = date >= new DateOnly(2026, 6, 10) && date <= new DateOnly(2026, 6, 11);
            Assert.Equal(inRange ? 150 : 100, p.Contacts);
        });
    }
}
