using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

// Golden / characterisation (ADR-006 layer 2): the per-Skill competition must
// reproduce the prototype's chosen method, per-method accuracy/bias, and the
// green/amber thresholds for both frozen series.
public class ForecastCompetitionGoldenTests
{
    [Fact]
    public void Reproduces_the_prototype_competition_for_the_historical_series()
    {
        AssertMatches(Fixtures.LoadCompetition().Historical, ForecastCompetition.Run(Fixtures.LoadHistory()));
    }

    [Fact]
    public void Reproduces_the_prototype_competition_for_the_cs_series()
    {
        AssertMatches(Fixtures.LoadCompetition().Cs, ForecastCompetition.Run(Fixtures.LoadHistory("historical-cs.csv")));
    }

    private static void AssertMatches(GoldenCompResult expected, CompetitionResult actual)
    {
        Assert.Equal(expected.Sufficient, actual.Sufficient);
        Assert.Equal(expected.Chosen, actual.Chosen);
        Assert.Equal(expected.Mean, actual.Mean, precision: 1);
        Assert.Equal(expected.Std, actual.Std, precision: 1);
        Assert.Equal(expected.GreenThreshold, actual.GreenThreshold);
        Assert.Equal(expected.AmberThreshold, actual.AmberThreshold);

        Assert.Equal(expected.Scores.Count, actual.Scores.Count);
        for (var i = 0; i < actual.Scores.Count; i++)
        {
            Assert.Equal(expected.Scores[i], actual.Scores[i], precision: 6);
        }

        Assert.Equal(expected.Methods.Count, actual.Methods.Count);
        for (var i = 0; i < actual.Methods.Count; i++)
        {
            var e = expected.Methods[i];
            var a = actual.Methods[i];
            Assert.Equal(e.Id, a.Id);
            Assert.Equal(e.Label, a.Label);
            Assert.Equal(e.MeanAcc, a.MeanAccuracy, precision: 1);
            Assert.Equal(e.Std, a.Std, precision: 1);
            Assert.Equal(e.Bias, a.Bias, precision: 1);
            Assert.Equal(e.Scores.Count, a.Scores.Count);
            for (var s = 0; s < a.Scores.Count; s++)
            {
                Assert.Equal(e.Scores[s], a.Scores[s], precision: 6);
            }
        }
    }
}
