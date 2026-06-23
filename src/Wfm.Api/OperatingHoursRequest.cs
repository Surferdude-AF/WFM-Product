using System.Globalization;
using Wfm.Forecasting.Domain;

namespace Wfm.Api;

// PUT body for a Skill's operating hours. A null/omitted `weekly` means always open;
// otherwise each present weekday is open for its range and absent weekdays are closed.
public sealed record OperatingHoursRequest(IReadOnlyDictionary<string, OpenRangeRequest>? Weekly);

public sealed record OpenRangeRequest(string Open, string Close);

internal static class OperatingHoursRequestMapper
{
    // Validates the request at the boundary, returning the domain value or an error
    // message for a 400. Close of "00:00" means end-of-day (24:00).
    public static bool TryMap(OperatingHoursRequest request, out OperatingHours hours, out string error)
    {
        hours = OperatingHours.AlwaysOpen;
        error = string.Empty;

        if (request.Weekly is null)
        {
            return true;
        }

        var weekly = new Dictionary<DayOfWeek, OpenRange>();
        foreach (var (key, range) in request.Weekly)
        {
            if (!Enum.TryParse<DayOfWeek>(key, ignoreCase: true, out var day))
            {
                error = $"'{key}' is not a valid weekday.";
                return false;
            }

            if (!TryParseTime(range.Open, out var open) || !TryParseTime(range.Close, out var close))
            {
                error = $"{key}: open and close must be times in HH:mm.";
                return false;
            }

            if (close != TimeOnly.MinValue && close <= open)
            {
                error = $"{key}: close must be after open (use 00:00 for end of day; overnight ranges are unsupported).";
                return false;
            }

            weekly[day] = new OpenRange(open, close);
        }

        hours = OperatingHours.ForWeek(weekly);
        return true;
    }

    private static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
