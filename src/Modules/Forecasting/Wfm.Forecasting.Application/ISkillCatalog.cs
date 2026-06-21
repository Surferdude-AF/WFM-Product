using Wfm.Forecasting.Domain;

namespace Wfm.Forecasting.Application;

// Lists the skills visible to the current unit of work. Scoping is the database's
// job: row-level security filters to the tenant in the session variable (ADR-001),
// so the query is unconditional and the platform enforces isolation.
public interface ISkillCatalog
{
    Task<IReadOnlyList<Skill>> ListAsync(CancellationToken cancellationToken = default);
}
