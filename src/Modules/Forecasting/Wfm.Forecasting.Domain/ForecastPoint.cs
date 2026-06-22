namespace Wfm.Forecasting.Domain;

// One forecast 15-minute interval: when it starts (UTC), the predicted contacts
// and average handle time. The forecast output.
public readonly record struct ForecastPoint(DateTimeOffset Start, int Contacts, int AhtSeconds);
