using System.Globalization;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

// Golden / characterisation (ADR-006 layer 2): detection must reproduce the
// prototype's outlier set and anomaly list over the frozen history.
public class OutlierDetectionGoldenTests
{
    private static DateOnly Date(string s) => DateOnly.ParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public void Reproduces_the_prototype_outlier_dates()
    {
        var history = Fixtures.LoadHistory();
        var expected = Fixtures.LoadDetection().OutlierDates.Select(Date).ToHashSet();

        var actual = OutlierDetection.DetectOutlierDates(history);

        Assert.Equal(expected, actual.ToHashSet());
    }

    [Fact]
    public void Reproduces_the_prototype_anomaly_list()
    {
        var history = Fixtures.LoadHistory();
        var golden = Fixtures.LoadDetection();

        var anomalies = OutlierDetection.DetectAnomalies(history);

        Assert.Equal(golden.Anomalies.Count, anomalies.Count);
        for (var i = 0; i < anomalies.Count; i++)
        {
            var expected = golden.Anomalies[i];
            var actual = anomalies[i];
            Assert.Equal(Date(expected.Date), actual.Date);
            Assert.Equal(expected.Dir, actual.Direction == AnomalyDirection.High ? "high" : "low");
            Assert.Equal(expected.Ratio, actual.Ratio, precision: 2);
            Assert.Equal(expected.Total, actual.Total);
            Assert.Equal(expected.Median, actual.Median, precision: 6);
        }
    }
}
