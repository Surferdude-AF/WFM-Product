using CsCheck;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

public class ForecastAccuracyTests
{
    [Fact]
    public void Wmape_is_total_absolute_error_over_total_actual_as_a_percentage()
    {
        // |10-8| + |0-5| = 7; total actual = 10 -> 70%.
        var wmape = ForecastAccuracy.Wmape([(10, 8), (0, 5)]);

        Assert.Equal(70d, wmape, precision: 10);
    }

    [Fact]
    public void Wmape_is_zero_when_there_is_no_actual_volume()
    {
        Assert.Equal(0d, ForecastAccuracy.Wmape([(0, 4), (0, 0)]));
    }

    [Fact]
    public void Wmape_is_zero_for_a_perfect_forecast()
    {
        Gen.Select(Gen.Int[1, 1000], Gen.Int[1, 1000])
            .List[1, 200]
            .Sample(pairs =>
            {
                var perfect = pairs.Select(p => (p.Item1, p.Item1)).ToList();
                return ForecastAccuracy.Wmape(perfect) == 0d;
            });
    }

    [Fact]
    public void Wmape_is_non_negative_and_scale_invariant()
    {
        Gen.Select(
            Gen.Select(Gen.Int[0, 1000], Gen.Int[0, 1000]).List[1, 200],
            Gen.Int[1, 50])
        .Sample(t =>
        {
            var (pairs, k) = t;
            var scaled = pairs.Select(p => (p.Item1 * k, p.Item2 * k)).ToList();

            var baseline = ForecastAccuracy.Wmape(pairs);
            var scaledWmape = ForecastAccuracy.Wmape(scaled);

            return baseline >= 0d && Math.Abs(scaledWmape - baseline) < 1e-9;
        });
    }
}
