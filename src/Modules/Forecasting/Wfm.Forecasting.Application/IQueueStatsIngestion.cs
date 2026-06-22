using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Application;

// Ingests raw interval stats for a Queue from a CSV source (`timestamp,contacts,
// aht_seconds`, UTC) into the store. Idempotent: re-ingesting an interval
// overwrites it. The Queue's tenant is taken from the request context, never the
// payload (ADR-001).
public interface IQueueStatsIngestion
{
    Task<int> IngestAsync(QueueId queue, TextReader csv, CancellationToken cancellationToken = default);
}
