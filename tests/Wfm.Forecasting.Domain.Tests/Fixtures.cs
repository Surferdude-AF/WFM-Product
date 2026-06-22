using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Domain.Tests;

// Loads the frozen golden fixtures captured from the WFM-Take1 prototype.
internal static class Fixtures
{
    private static string Dir => Path.Combine(AppContext.BaseDirectory, "Fixtures");

    public static IReadOnlyList<HistoricalInterval> LoadHistory(string file = "baseline-history.csv")
    {
        var lines = File.ReadAllLines(Path.Combine(Dir, file));
        var result = new List<HistoricalInterval>(lines.Length - 1);
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split(',');
            result.Add(new HistoricalInterval(
                ParseUtc(parts[0]),
                int.Parse(parts[1], CultureInfo.InvariantCulture),
                int.Parse(parts[2], CultureInfo.InvariantCulture)));
        }

        return result;
    }

    public static GoldenForecast LoadGolden(string file = "baseline-forecast.json")
        => JsonSerializer.Deserialize<GoldenForecast>(File.ReadAllText(Path.Combine(Dir, file)))!;

    public static DateTimeOffset ParseUtc(string timestamp)
        => new(DateTime.ParseExact(timestamp, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None), TimeSpan.Zero);

    public static DateTimeOffset ParseUtcDate(string date)
        => new(DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None), TimeSpan.Zero);
}

internal sealed record GoldenForecast(
    [property: JsonPropertyName("weekStart")] string WeekStart,
    [property: JsonPropertyName("forecast")] IReadOnlyList<GoldenRow> Forecast);

internal sealed record GoldenRow(
    [property: JsonPropertyName("timestamp")] string Timestamp,
    [property: JsonPropertyName("contacts")] int Contacts,
    [property: JsonPropertyName("aht_seconds")] int AhtSeconds);
