using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

// Accuracy-regression gate (ADR-006 layer 3): a data-anchored quality ratchet over
// the frozen corpus. The chosen method's backtest accuracy for each series must not
// fall more than `Margin` below the committed baseline. The margin (~1/3 of the
// fold-to-fold standard deviation) absorbs neutral refactor/float noise but catches
// a real regression. When a change IMPROVES accuracy, raise the baseline in the same
// PR -- the floor only ever ratchets up. The exact-match golden (9g) handles "did it
// change?"; this gate handles "did it get worse?".
public class ForecastAccuracyGateTests
{
    private const double Margin = 0.5;

    [Fact]
    public void Historical_series_accuracy_does_not_regress()
    {
        var floor = Fixtures.LoadAccuracyBaseline().Historical - Margin;
        var mean = ForecastCompetition.Run(Fixtures.LoadHistory()).Mean;

        Assert.True(mean >= floor, $"Historical accuracy {mean} regressed below the floor {floor} (baseline minus {Margin}).");
    }

    [Fact]
    public void Cs_series_accuracy_does_not_regress()
    {
        var floor = Fixtures.LoadAccuracyBaseline().Cs - Margin;
        var mean = ForecastCompetition.Run(Fixtures.LoadHistory("historical-cs.csv")).Mean;

        Assert.True(mean >= floor, $"CS accuracy {mean} regressed below the floor {floor} (baseline minus {Margin}).");
    }
}
