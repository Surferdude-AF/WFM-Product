using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Infrastructure.Persistence;

// A queued request to forecast a Skill. The trigger API enqueues; the worker
// claims and runs it (step 11b). Tenant-scoped for the trigger (ADR-001); the
// worker is a platform actor that processes across tenants (see the RLS policy).
public sealed class ForecastJob
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Done = "done";
    public const string Failed = "failed";

    public ForecastJob(Guid id, SkillId skillId, TenantId tenantId, string status, DateTimeOffset requestedAt)
    {
        Id = id;
        SkillId = skillId;
        TenantId = tenantId;
        Status = status;
        RequestedAt = requestedAt;
    }

    private ForecastJob()
    {
    }

    public Guid Id { get; private set; }
    public SkillId SkillId { get; private set; }
    public TenantId TenantId { get; private set; }
    public string Status { get; private set; } = null!;
    public DateTimeOffset RequestedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
}
