namespace Wfm.Forecasting.Domain;

// Forecast error metric. WMAPE (weighted MAPE) divides total absolute error by
// total actual volume, so low-traffic intervals don't dominate the percentage.
// Returns 0 when there is no actual volume (no error to measure).
public static class ForecastAccuracy
{
    public static double Wmape(IReadOnlyList<(int Actual, int Forecast)> pairs)
    {
        double absoluteError = 0, totalActual = 0;
        foreach (var (actual, forecast) in pairs)
        {
            absoluteError += Math.Abs(actual - forecast);
            totalActual += actual;
        }

        return totalActual == 0 ? 0 : absoluteError / totalActual * 100;
    }
}
