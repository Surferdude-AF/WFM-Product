namespace Wfm.Forecasting.Domain;

public enum AnomalyDirection
{
    High,
    Low,
}

// A day whose total volume is anomalous for its weekday: the direction, the
// magnitude vs the weekday norm (ratio), the day's total and that norm (median).
public readonly record struct Anomaly(
    DateOnly Date,
    AnomalyDirection Direction,
    double Ratio,
    int Total,
    double Median);
