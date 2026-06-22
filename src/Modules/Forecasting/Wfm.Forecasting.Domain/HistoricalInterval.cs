namespace Wfm.Forecasting.Domain;

// One observed 15-minute interval of history for a Skill: when it started (UTC),
// how many contacts arrived, and the average handle time. The forecast input.
public readonly record struct HistoricalInterval(DateTimeOffset Start, int Contacts, int AhtSeconds);
