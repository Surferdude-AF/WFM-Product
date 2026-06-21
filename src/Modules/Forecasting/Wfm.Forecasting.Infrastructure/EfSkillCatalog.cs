using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;

namespace Wfm.Forecasting.Infrastructure;

public sealed class EfSkillCatalog(WfmDbContext db) : ISkillCatalog
{
    public async Task<IReadOnlyList<Skill>> ListAsync(CancellationToken cancellationToken = default)
        => await db.Skills.AsNoTracking().OrderBy(s => s.Name).ToListAsync(cancellationToken);
}
