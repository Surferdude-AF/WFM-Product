namespace Wfm.Forecasting.Domain;

// An open period within a day, as wall-clock times. By convention a Close of 00:00
// means end-of-day (midnight). Overnight wrap (e.g. 22:00->06:00) is not supported.
public readonly record struct OpenRange(TimeOnly Open, TimeOnly Close)
{
    // The open span as 15-minute interval indices [Start, End) within 0..96, or null
    // when the range is degenerate (End <= Start).
    public (int Start, int End)? ToIntervalRange()
    {
        var start = (Open.Hour * 4) + (Open.Minute / 15);
        var end = Close == TimeOnly.MinValue ? 96 : (Close.Hour * 4) + (Close.Minute / 15);
        return end > start ? (start, end) : null;
    }
}
