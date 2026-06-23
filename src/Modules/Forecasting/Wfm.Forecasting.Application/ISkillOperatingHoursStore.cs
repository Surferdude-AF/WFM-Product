using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Application;

// Persists a Skill's operating hours (ST-002 2a). Scoping is the database's job: RLS
// filters to the tenant in session, so a Skill outside the tenant simply isn't found.
public interface ISkillOperatingHoursStore
{
    // False when the Skill does not exist for the current tenant.
    Task<bool> SetAsync(SkillId skill, OperatingHours hours, CancellationToken cancellationToken = default);
}
