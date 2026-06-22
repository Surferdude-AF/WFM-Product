using System.Globalization;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

// Golden / characterisation (ADR-006 layer 2): the C# port must reproduce the
// prototype's forecast exactly over the frozen history. Any drift fails here and
// has to be a conscious re-bless, which is what makes the later ports safe.
public class BaselineForecasterGoldenTests
{
    [Fact]
    public void Reproduces_the_prototype_forecast_over_the_frozen_history()
    {
        var history = Fixtures.LoadHistory();
        var golden = Fixtures.LoadGolden();
        var weekStart = Fixtures.ParseUtcDate(golden.WeekStart);

        var forecast = BaselineForecaster.Forecast(history, weekStart);

        Assert.Equal(golden.Forecast.Count, forecast.Count);
        for (var i = 0; i < forecast.Count; i++)
        {
            var expected = golden.Forecast[i];
            var actual = forecast[i];
            Assert.Equal(expected.Timestamp, actual.Start.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            Assert.Equal(expected.Contacts, actual.Contacts);
            Assert.Equal(expected.AhtSeconds, actual.AhtSeconds);
        }
    }
}
