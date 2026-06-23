using Microsoft.EntityFrameworkCore;
using Wfm.Forecasting.Application;
using Wfm.Forecasting.Domain;
using Wfm.Forecasting.Infrastructure.Persistence;

namespace Wfm.Forecasting.Infrastructure;

public sealed class EfSkillOperatingHoursStore(WfmDbContext db) : ISkillOperatingHoursStore
{
    public async Task<bool> SetAsync(SkillId skill, OperatingHours hours, CancellationToken cancellationToken = default)
    {
        var entity = await db.Skills.FirstOrDefaultAsync(s => s.Id == skill, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        entity.SetOperatingHours(hours);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
