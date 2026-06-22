using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;

namespace Wfm.Forecasting.Infrastructure;

// Parses the prototype's CSV shape (`timestamp,contacts,aht_seconds`, UTC) and
// upserts it into queue_interval_stats for one Queue. Idempotent on the
// (queue_id, interval_start) key; rows are written under the request tenant so RLS
// keeps them scoped (ADR-001).
public sealed class CsvQueueStatsIngestion(WfmDbContext db, ITenantContext tenantContext) : IQueueStatsIngestion
{
    public async Task<int> IngestAsync(QueueId queue, TextReader csv, CancellationToken cancellationToken = default)
    {
        var tenant = tenantContext.TenantId
            ?? throw new InvalidOperationException("Cannot ingest without a tenant in context.");

        var rows = Parse(csv);

        var existing = await db.QueueIntervalStats
            .Where(s => s.QueueId == queue)
            .ToDictionaryAsync(s => s.IntervalStart, cancellationToken);

        foreach (var (start, contacts, aht) in rows)
        {
            if (existing.TryGetValue(start, out var stat))
            {
                stat.Update(contacts, aht);
            }
            else
            {
                db.QueueIntervalStats.Add(new QueueIntervalStat(queue, tenant, start, contacts, aht));
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return rows.Count;
    }

    private static List<(DateTimeOffset Start, int Contacts, int AhtSeconds)> Parse(TextReader csv)
    {
        var rows = new List<(DateTimeOffset, int, int)>();
        var text = csv.ReadToEnd();
        var first = true;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (first)
            {
                first = false; // header
                continue;
            }

            if (line.Length == 0)
            {
                continue;
            }

            var parts = line.Split(',');
            var start = new DateTimeOffset(
                DateTime.ParseExact(parts[0], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None),
                TimeSpan.Zero);
            rows.Add((start, int.Parse(parts[1], CultureInfo.InvariantCulture), int.Parse(parts[2], CultureInfo.InvariantCulture)));
        }

        return rows;
    }
}
