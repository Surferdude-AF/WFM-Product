using Wfm.Forecasting.Domain;
using Wfm.SharedKernel;

namespace Wfm.Forecasting.Application;

public interface IForecastStreamReader
{
    Task<bool> ExistsAsync(TenantId tenant, SkillId skill, CancellationToken cancellationToken = default);
}
