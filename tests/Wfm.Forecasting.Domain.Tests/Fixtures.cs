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

    public static GoldenDetection LoadDetection(string file = "outliers-detection.json")
        => JsonSerializer.Deserialize<GoldenDetection>(File.ReadAllText(Path.Combine(Dir, file)))!;

    public static GoldenMask LoadMask(string file = "operating-mask.json")
        => JsonSerializer.Deserialize<GoldenMask>(File.ReadAllText(Path.Combine(Dir, file)))!;

    public static GoldenHolidays LoadHolidays(string file = "holidays.json")
        => JsonSerializer.Deserialize<GoldenHolidays>(File.ReadAllText(Path.Combine(Dir, file)))!;

    public static GoldenOverlay LoadEventsOverlay(string file = "events-overlay.json")
        => JsonSerializer.Deserialize<GoldenOverlay>(File.ReadAllText(Path.Combine(Dir, file)))!;

    public static IReadOnlyList<ForecastEvent> LoadEvents(string file = "events.json")
        => JsonSerializer.Deserialize<List<EventDto>>(File.ReadAllText(Path.Combine(Dir, file)))!
            .Select(e => new ForecastEvent(
                e.Name,
                DateOnly.ParseExact(e.Start, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateOnly.ParseExact(e.End, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                e.Volume,
                e.Aht,
                e.Skills ?? []))
            .ToList();

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

internal sealed record GoldenDetection(
    [property: JsonPropertyName("outlierDates")] IReadOnlyList<string> OutlierDates,
    [property: JsonPropertyName("anomalies")] IReadOnlyList<GoldenAnomaly> Anomalies);

internal sealed record GoldenAnomaly(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("dir")] string Dir,
    [property: JsonPropertyName("ratio")] double Ratio,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("median")] double Median);

internal sealed record GoldenMask(
    [property: JsonPropertyName("weekStart")] string WeekStart,
    [property: JsonPropertyName("masked")] IReadOnlyList<GoldenRow> Masked);

internal sealed record GoldenHolidays(
    [property: JsonPropertyName("year2026")] IReadOnlyList<GoldenHoliday> Year2026,
    [property: JsonPropertyName("range")] IReadOnlyList<GoldenHoliday> Range);

internal sealed record GoldenHoliday(
    [property: JsonPropertyName("date")] string Date,
    [property: JsonPropertyName("name")] string Name);

internal sealed record GoldenOverlay(
    [property: JsonPropertyName("skillName")] string SkillName,
    [property: JsonPropertyName("spanStart")] string SpanStart,
    [property: JsonPropertyName("days")] int Days,
    [property: JsonPropertyName("applied")] IReadOnlyList<GoldenRow> Applied);

internal sealed record EventDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("start")] string Start,
    [property: JsonPropertyName("end")] string End,
    [property: JsonPropertyName("volume")] double Volume,
    [property: JsonPropertyName("aht")] double Aht,
    [property: JsonPropertyName("skills")] IReadOnlyCollection<string>? Skills);
