namespace Wfm.Forecasting.Domain;

public enum SpecialDayHours
{
    Closed,
    Normal,
    Custom,
}

// A date-specific override of the weekly pattern (ST-002): closed, normal weekday
// hours, or custom hours -- plus an optional volume/AHT haircut. One entry carries
// both the "are we open?" and "how busy?" dimensions.
public sealed record SpecialDay(
    SpecialDayHours Hours,
    OpenRange? CustomHours = null,
    double VolumeMultiplier = 1.0,
    double AhtMultiplier = 1.0);
