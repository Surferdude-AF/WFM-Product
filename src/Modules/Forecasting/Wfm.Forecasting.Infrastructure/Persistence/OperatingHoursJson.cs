using System.Globalization;
using System.Text.Json;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// Maps OperatingHours to/from the jsonb storage form. Always open is the JSON null
// literal (the column default), so existing Skills stay always-open with no backfill;
// otherwise an object keyed by weekday name (an absent weekday is closed).
internal static class OperatingHoursJson
{
    private sealed record RangeDto(string Open, string Close);

    public static string ToJson(OperatingHours hours)
    {
        if (hours.Weekly is null)
        {
            return "null";
        }

        var dto = hours.Weekly.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => new RangeDto(
                kvp.Value.Open.ToString("HH:mm", CultureInfo.InvariantCulture),
                kvp.Value.Close.ToString("HH:mm", CultureInfo.InvariantCulture)));
        return JsonSerializer.Serialize(dto);
    }

    public static OperatingHours FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<Dictionary<string, RangeDto>>(json);
        if (dto is null)
        {
            return OperatingHours.AlwaysOpen;
        }

        var weekly = dto.ToDictionary(
            kvp => Enum.Parse<DayOfWeek>(kvp.Key),
            kvp => new OpenRange(
                TimeOnly.Parse(kvp.Value.Open, CultureInfo.InvariantCulture),
                TimeOnly.Parse(kvp.Value.Close, CultureInfo.InvariantCulture)));
        return OperatingHours.ForWeek(weekly);
    }
}
